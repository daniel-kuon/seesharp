using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace SeeSharp.Progress;

/// <summary>A bundle written by <see cref="ReviewExport"/>, read back for the admin review view: the CSV files, who wrote it and the submitted sources.</summary>
/// <param name="Trainee">Empty for bundles written before trainee.txt existed (2026-10-29).</param>
public sealed record ReviewBundle(IReadOnlyList<ReviewAttempt> Attempts, IReadOnlyList<ReviewSkill> Skills, string Trainee = "")
{
    /// <param name="zip">A seekable stream of the bundle.</param>
    public static ReviewBundle Read(Stream zip)
    {
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
        var attempts = Rows(archive, "attempts.csv").Select(r => new ReviewAttempt(
            int.Parse(r[0], CultureInfo.InvariantCulture), r[1], r[2], r[3], int.Parse(r[4], CultureInfo.InvariantCulture), r[5] == "true", r[6],
            r.Length > 7 && r[7] != "" ? r[7] : null)).ToList(); // bundles before 2026-10-25 have no tutorVerdict column
        var skills = Rows(archive, "skills.csv").Select(r => new ReviewSkill(r[0], r[1], r[2], r[3])).ToList();
        var trainee = archive.GetEntry("trainee.txt") is { } t ? new StreamReader(t.Open(), Encoding.UTF8).ReadToEnd().Trim() : "";
        // ponytail: every submitted source is held in memory; a trainee's text sources are small
        var files = Sources(archive).ToLookup(f => f.AttemptId, f => f.File);
        return new ReviewBundle(attempts.Select(a => a with { Files = files[a.Id].OrderBy(f => f.Path, StringComparer.Ordinal).ToList() }).ToList(), skills, trainee);
    }

    /// <summary>Text files under <c>&lt;exercise&gt;/&lt;item&gt;/&lt;id&gt;_&lt;timestamp&gt;/source/</c>; exam attempts are left out.</summary>
    private static IEnumerable<(int AttemptId, SourceFile File)> Sources(ZipArchive archive)
    {
        const long maxTotalChars = 64L * 1024 * 1024; // all sources together; the rest of an oversized bundle shows no code
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var segments = entry.FullName.Split('/');
            if (segments.Length < 5 || segments[0] is "exams" or "tutor" || segments[3] != "source"
                || !int.TryParse(segments[2].Split('_')[0], CultureInfo.InvariantCulture, out var id))
                continue;
            if (SourceFile.Read(entry, string.Join('/', segments[4..])) is not { } file)
                continue;
            if ((total += file.Text.Length) > maxTotalChars)
                yield break;
            yield return (id, file);
        }
    }

    private static IEnumerable<string[]> Rows(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidDataException(name);
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return ParseCsv(reader.ReadToEnd()).Skip(1).Where(r => r.Length > 1).ToList();
    }

    /// <summary>RFC 4180 as <see cref="ReviewExport"/> writes it: comma separated, quotes doubled inside quoted fields, newlines allowed there.</summary>
    public static List<string[]> ParseCsv(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\r') { }
            else if (c == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row.ToArray()); row.Clear(); }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }
}

/// <param name="TutorVerdict"><c>pass</c> or <c>revise</c> when the tutor judged this open answer; null otherwise.</param>
public sealed record ReviewAttempt(int Id, string Exercise, string Item, string Timestamp, int TimeSpentSeconds, bool Passed, string Answer, string? TutorVerdict = null)
{
    /// <summary>The submitted source of a coding attempt, sorted by path; empty for other items.</summary>
    public IReadOnlyList<SourceFile> Files { get; init; } = [];
}

/// <summary>One text file of a submitted source tree, <paramref name="Path"/> relative with '/'.</summary>
public sealed record SourceFile(string Path, string Text)
{
    /// <summary>Larger files are skipped: no source file of an exercise comes near it, and a bundle comes from another machine.</summary>
    public const int MaxBytes = 1024 * 1024;

    /// <summary>The text of <paramref name="entry"/>, or null for a folder, a binary file (one with a NUL byte) or one over <see cref="MaxBytes"/>.</summary>
    public static SourceFile? Read(ZipArchiveEntry entry, string path)
    {
        if (entry.Name == "" || entry.Length > MaxBytes)
            return null;
        using var buffer = new MemoryStream();
        using (var stream = entry.Open())
        {
            // The header's Length can lie; stop reading one byte past the cap.
            var chunk = new byte[81920];
            int read;
            while ((read = stream.Read(chunk, 0, (int)Math.Min(chunk.Length, MaxBytes + 1 - buffer.Length))) > 0)
                buffer.Write(chunk, 0, read);
        }
        if (buffer.Length > MaxBytes || buffer.GetBuffer().AsSpan(0, (int)buffer.Length).Contains((byte)0))
            return null;
        buffer.Position = 0;
        return new SourceFile(path, new StreamReader(buffer, Encoding.UTF8).ReadToEnd());
    }

    /// <summary>The text files of an <see cref="SeeSharp.Core.Attempt.Source"/> snapshot.</summary>
    public static IReadOnlyList<SourceFile> FromZip(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return archive.Entries.Select(e => Read(e, e.FullName)).OfType<SourceFile>().OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
    }

    /// <summary>Lines as the code view numbers them (1-based by index + 1); a trailing newline adds no empty last line.</summary>
    public IReadOnlyList<string> Lines => Text.TrimEnd('\n', '\r').Split('\n').Select(l => l.TrimEnd('\r')).ToList();
}

public sealed record ReviewSkill(string Skill, string State, string LastExercised, string Exercise);
