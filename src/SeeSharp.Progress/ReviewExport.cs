using System.Globalization;
using System.IO.Compression;
using System.Text;
using SeeSharp.Core;

namespace SeeSharp.Progress;

/// <summary>
/// The bundle the trainee hands the supervisor. Layout:
/// <code>
/// trainee.txt                                    <see cref="Trainee"/>; remarks on the report are filed under it
/// attempts.csv                                   id, exercise, item, timestamp, timeSpentSeconds, passed, answer, tutorVerdict
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
    /// <summary>Who wrote the report: user and machine name. No accounts exist, and this is enough to tell 1–5 trainees apart.</summary>
    public static string Trainee => $"{Environment.UserName}@{Environment.MachineName}";

    public static void Write(ProgressStore store, string zipPath)
    {
        var attempts = store.Attempts();
        var exchanges = store.TutorExchanges();
        var skills = store.HeldSkills().Values.OrderBy(s => s.SkillId, StringComparer.Ordinal);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath))!);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        AddText(zip, "trainee.txt", Trainee);
        AddText(zip, "attempts.csv", AttemptsCsv(attempts, exchanges.Where(t => t.Verdict is not null).ToList()));
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
            AddText(zip, folder + "attempts.csv", AttemptsCsv(runAttempts, [])); // exam answers are never judged
            AddAttempts(zip, folder, runAttempts);
        }

        foreach (var exercise in exchanges.GroupBy(t => t.ExerciseId))
            AddText(zip, $"tutor/{exercise.Key}.md", TutorMarkdown(exercise.Key, exercise));
    }

    /// <summary>One section per exchange, oldest first; the answer is the model's Markdown as it was shown.</summary>
    private static string TutorMarkdown(string exerciseId, IEnumerable<TutorExchange> exchanges)
    {
        var sb = new StringBuilder($"# Tutor: {exerciseId}\n");
        foreach (var t in exchanges)
        {
            var cost = t.CostUsd is { } c ? string.Create(CultureInfo.InvariantCulture, $" · ≈ ${c:0.0000}") : "";
            var judged = t.Verdict is { } v ? $" · judgement of {t.ItemId}: **{v}**" : "";
            sb.Append(CultureInfo.InvariantCulture, $"\n## {Iso(t.Timestamp)} · level {t.Level}{judged} · {t.Provider}/{t.Model} · {t.InputTokens} in + {t.OutputTokens} out tokens{cost}\n\n");
            sb.Append("**Question:** ").Append(t.Question).Append("\n\n**Answer:**\n\n").Append(t.Answer.TrimEnd()).Append('\n');
        }
        return sb.ToString();
    }

    /// <param name="judgements">Tutor exchanges with a verdict.</param>
    private static string AttemptsCsv(IReadOnlyList<Attempt> attempts, IReadOnlyList<TutorExchange> judgements) => Csv(
        ["id", "exercise", "item", "timestamp", "timeSpentSeconds", "passed", "answer", "tutorVerdict"],
        attempts.Select(a => new[]
        {
            a.Id.ToString(CultureInfo.InvariantCulture), a.ExerciseId, a.ItemId, Iso(a.Timestamp),
            a.TimeSpent.TotalSeconds.ToString("0", CultureInfo.InvariantCulture), a.Passed ? "true" : "false", a.Answer ?? "",
            Judgement(a, attempts, judgements)?.Verdict ?? "",
        }));

    /// <summary>
    /// The judgement belonging to <paramref name="attempt"/>: the last exchange with a verdict for its item logged after the item's
    /// previous attempt and not after this one (a judgement is logged right before its attempt). Null when the answer was not judged.
    /// </summary>
    // ponytail: O(attempts²) scan; fine for one trainee's history
    public static TutorExchange? Judgement(Attempt attempt, IEnumerable<Attempt> attempts, IEnumerable<TutorExchange> exchanges)
    {
        var previous = attempts.LastOrDefault(p => p.ExerciseId == attempt.ExerciseId && p.ItemId == attempt.ItemId && p.Id < attempt.Id)?.Timestamp ?? DateTimeOffset.MinValue;
        return exchanges.LastOrDefault(t => t.Verdict is not null && t.ExerciseId == attempt.ExerciseId && t.ItemId == attempt.ItemId
                                            && t.Timestamp > previous && t.Timestamp <= attempt.Timestamp);
    }

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
