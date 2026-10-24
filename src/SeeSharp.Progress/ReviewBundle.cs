using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace SeeSharp.Progress;

/// <summary>The two CSV files of a bundle written by <see cref="ReviewExport"/>, read back for the admin review view.</summary>
public sealed record ReviewBundle(IReadOnlyList<ReviewAttempt> Attempts, IReadOnlyList<ReviewSkill> Skills)
{
    /// <param name="zip">A seekable stream of the bundle.</param>
    public static ReviewBundle Read(Stream zip)
    {
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
        var attempts = Rows(archive, "attempts.csv").Select(r => new ReviewAttempt(
            int.Parse(r[0], CultureInfo.InvariantCulture), r[1], r[2], r[3], int.Parse(r[4], CultureInfo.InvariantCulture), r[5] == "true", r[6],
            r.Length > 7 && r[7] != "" ? r[7] : null)).ToList(); // bundles before 2026-10-25 have no tutorVerdict column
        var skills = Rows(archive, "skills.csv").Select(r => new ReviewSkill(r[0], r[1], r[2], r[3])).ToList();
        return new ReviewBundle(attempts, skills);
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
public sealed record ReviewAttempt(int Id, string Exercise, string Item, string Timestamp, int TimeSpentSeconds, bool Passed, string Answer, string? TutorVerdict = null);

public sealed record ReviewSkill(string Skill, string State, string LastExercised, string Exercise);
