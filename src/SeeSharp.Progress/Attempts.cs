using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeeSharp.Content;
using SeeSharp.Core;
using SeeSharp.Grading;

namespace SeeSharp.Progress;

/// <summary>Builds an <see cref="Attempt"/> per item type, deciding <see cref="Attempt.Passed"/> the way the item's gate defines it.</summary>
public static class Attempts
{
    public static readonly JsonSerializerOptions GradingJson = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Passed when every option index in <paramref name="selected"/> and nothing else is correct.</summary>
    public static Attempt MultipleChoice(string exerciseId, MultipleChoiceItem item, IEnumerable<int> selected, TimeSpan timeSpent)
    {
        var chosen = selected.Distinct().Order().ToList();
        return new Attempt(exerciseId, item.Id, DateTimeOffset.UtcNow, timeSpent,
            chosen.SequenceEqual(item.Correct.Distinct().Order()), Answer: string.Join(",", chosen));
    }

    /// <summary>Passed when answered at all (AGENT_TASKS.md task 3: gates on answered, not correctness).</summary>
    public static Attempt Open(string exerciseId, OpenItem item, string answer, TimeSpan timeSpent) =>
        new(exerciseId, item.Id, DateTimeOffset.UtcNow, timeSpent, !string.IsNullOrWhiteSpace(answer), Answer: answer);

    /// <summary>The trainee marked the review item done; <paramref name="note"/> is optional.</summary>
    public static Attempt Review(string exerciseId, ReviewItem item, string? note, TimeSpan timeSpent) =>
        new(exerciseId, item.Id, DateTimeOffset.UtcNow, timeSpent, Passed: true, Answer: note);

    /// <summary>
    /// Snapshots <paramref name="sourceDir"/> (without bin/obj) and the grading result. Passed when the run passed, or — with
    /// <see cref="CodingItem.RequiredTests"/> — when every named test passed, whatever the rest did.
    /// </summary>
    public static Attempt Coding(string exerciseId, CodingItem item, string sourceDir, GradingResult result, TimeSpan timeSpent)
    {
        var passed = item.RequiredTests is { Count: > 0 } required
            ? required.All(name => result.Tests.Any(t => t.Name == name && t.Outcome == TestOutcome.Passed))
            : result.Outcome == GradingOutcome.Passed;
        return new Attempt(exerciseId, item.Id, DateTimeOffset.UtcNow, timeSpent, passed,
            Source: Zip(sourceDir), GradingJson: JsonSerializer.Serialize(result, GradingJson), Trx: result.Trx);
    }

    public static byte[] Zip(string sourceDir)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = System.IO.Path.GetRelativePath(sourceDir, file);
                var segments = relative.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                if (GradingRunner.IsArtifact(segments))
                    continue;
                zip.CreateEntryFromFile(file, relative.Replace('\\', '/'));
            }
        return buffer.ToArray();
    }
}
