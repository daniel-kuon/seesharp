using System.Globalization;
using System.IO.Compression;
using System.Text;
using SeeSharp.Core;

namespace SeeSharp.Progress;

/// <summary>
/// The bundle the trainee hands the supervisor. Layout:
/// <code>
/// attempts.csv                                   id, exercise, item, timestamp, timeSpentSeconds, passed, answer
/// skills.csv                                     skill, state, lastExercised, exercise
/// &lt;exercise&gt;/&lt;item&gt;/&lt;id&gt;_&lt;timestamp&gt;/answer.txt   open/review/multipleChoice answers
/// &lt;exercise&gt;/&lt;item&gt;/&lt;id&gt;_&lt;timestamp&gt;/source/...   the submitted source (coding)
/// &lt;exercise&gt;/&lt;item&gt;/&lt;id&gt;_&lt;timestamp&gt;/grading.json
/// &lt;exercise&gt;/&lt;item&gt;/&lt;id&gt;_&lt;timestamp&gt;/results.trx
/// exams/&lt;run id&gt;/run.csv                        id, exam, kind, started, deadline, submitted, seed, items
/// exams/&lt;run id&gt;/attempts.csv                   the run's attempts, same columns as above
/// exams/&lt;run id&gt;/&lt;exercise&gt;/&lt;item&gt;/...     same layout as above
/// tutor/&lt;exercise&gt;.md                          every AI tutor exchange for the exercise
/// </code>
/// </summary>
public static class ReviewExport
{
    public static void Write(ProgressStore store, string zipPath)
    {
        var attempts = store.Attempts();
        var skills = store.HeldSkills().Values.OrderBy(s => s.SkillId, StringComparer.Ordinal);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath))!);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        AddText(zip, "attempts.csv", AttemptsCsv(attempts));
        AddText(zip, "skills.csv", Csv(
            ["skill", "state", "lastExercised", "exercise"],
            skills.Select(s => new[] { s.SkillId, s.State.ToString().ToLowerInvariant(), Iso(s.LastExercised), s.Cause })));
        AddAttempts(zip, "", attempts);

        foreach (var run in store.ExamRuns())
        {
            var folder = $"exams/{run.Id:D4}/";
            AddText(zip, folder + "run.csv", Csv(
                ["id", "exam", "kind", "started", "deadline", "submitted", "seed", "items"],
                [[run.Id.ToString(CultureInfo.InvariantCulture), run.ExamId, run.Real ? "real" : "practice", Iso(run.StartedAt), Iso(run.Deadline),
                  run.SubmittedAt is { } s ? Iso(s) : "", run.Seed.ToString(CultureInfo.InvariantCulture), string.Join(" ", run.Items.Select(i => i.ExerciseId + "/" + i.ItemId))]]));
            var runAttempts = store.ExamAttempts(run.Id);
            AddText(zip, folder + "attempts.csv", AttemptsCsv(runAttempts));
            AddAttempts(zip, folder, runAttempts);
        }

        foreach (var exercise in store.TutorExchanges().GroupBy(t => t.ExerciseId))
            AddText(zip, $"tutor/{exercise.Key}.md", TutorMarkdown(exercise.Key, exercise));
    }

    /// <summary>One section per exchange, oldest first; the answer is the model's Markdown as it was shown.</summary>
    private static string TutorMarkdown(string exerciseId, IEnumerable<TutorExchange> exchanges)
    {
        var sb = new StringBuilder($"# Tutor: {exerciseId}\n");
        foreach (var t in exchanges)
        {
            var cost = t.CostUsd is { } c ? string.Create(CultureInfo.InvariantCulture, $" · ≈ ${c:0.0000}") : "";
            sb.Append(CultureInfo.InvariantCulture, $"\n## {Iso(t.Timestamp)} · level {t.Level} · {t.Provider}/{t.Model} · {t.InputTokens} in + {t.OutputTokens} out tokens{cost}\n\n");
            sb.Append("**Question:** ").Append(t.Question).Append("\n\n**Answer:**\n\n").Append(t.Answer.TrimEnd()).Append('\n');
        }
        return sb.ToString();
    }

    private static string AttemptsCsv(IEnumerable<Attempt> attempts) => Csv(
        ["id", "exercise", "item", "timestamp", "timeSpentSeconds", "passed", "answer"],
        attempts.Select(a => new[]
        {
            a.Id.ToString(CultureInfo.InvariantCulture), a.ExerciseId, a.ItemId, Iso(a.Timestamp),
            a.TimeSpent.TotalSeconds.ToString("0", CultureInfo.InvariantCulture), a.Passed ? "true" : "false", a.Answer ?? "",
        }));

    private static void AddAttempts(ZipArchive zip, string prefix, IEnumerable<Attempt> attempts)
    {
        foreach (var a in attempts)
        {
            var folder = $"{prefix}{a.ExerciseId}/{a.ItemId}/{a.Id:D4}_{a.Timestamp.ToUniversalTime():yyyyMMdd-HHmmss}/";
            if (a.Answer is not null) AddText(zip, folder + "answer.txt", a.Answer);
            if (a.GradingJson is not null) AddText(zip, folder + "grading.json", a.GradingJson);
            if (a.Trx is not null) AddText(zip, folder + "results.trx", a.Trx);
            if (a.Source is not null)
                using (var source = new ZipArchive(new MemoryStream(a.Source), ZipArchiveMode.Read))
                    foreach (var entry in source.Entries)
                    {
                        using var from = entry.Open();
                        using var to = zip.CreateEntry(folder + "source/" + entry.FullName).Open();
                        from.CopyTo(to);
                    }
        }
    }

    private static void AddText(ZipArchive zip, string name, string text)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(Encoding.UTF8.GetBytes(text));
    }

    private static string Csv(string[] header, IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows.Prepend(header))
            sb.AppendLine(string.Join(",", row.Select(Quote)));
        return sb.ToString();
    }

    private static string Quote(string field) =>
        field.AsSpan().IndexOfAny(",\"\r\n") < 0 ? field : "\"" + field.Replace("\"", "\"\"") + "\"";

    private static string Iso(DateTimeOffset t) => t.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
