using System.Text.Encodings.Web;
using System.Text.Json;

namespace SeeSharp.Progress;

/// <summary>
/// The file the supervisor sends back: every shared remark written on one trainee's reports, never the private ones.
/// <code>
/// { "format": "seesharp-remarks/1", "trainee": "…", "exported": "…",
///   "remarks": [ { "exercise", "item", "attempt", "file", "line", "text", "timestamp" } ] }
/// </code>
/// </summary>
public static class RemarksReply
{
    public const string Format = "seesharp-remarks/1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private sealed record Reply(string Format, string Trainee, DateTimeOffset Exported, IReadOnlyList<Entry> Remarks);

    private sealed record Entry(string Exercise, string? Item, long? Attempt, string? File, int? Line, string Text, DateTimeOffset Timestamp);

    /// <summary>Writes the shared remarks on <paramref name="trainee"/>'s reports to <paramref name="path"/>; returns how many.</summary>
    public static int Write(ProgressStore store, string trainee, string path)
    {
        var shared = store.Remarks(trainee).Where(r => r.Shared)
            .Select(r => new Entry(r.ExerciseId, r.ItemId, r.AttemptId, r.File, r.Line, r.Text, r.Timestamp)).ToList();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(new Reply(Format, trainee, DateTimeOffset.UtcNow, shared), Json));
        return shared.Count;
    }

    /// <summary>The remarks of a reply file, ready for <see cref="ProgressStore.ReplaceReceivedRemarks"/>.</summary>
    /// <exception cref="InvalidDataException">Not a reply file.</exception>
    /// <exception cref="JsonException">Not JSON.</exception>
    public static IReadOnlyList<Remark> Read(Stream json)
    {
        var reply = JsonSerializer.Deserialize<Reply>(json, Json);
        if (reply is not { Format: Format, Remarks: not null })
            throw new InvalidDataException(Format);
        return reply.Remarks.Select(e => new Remark(reply.Trainee, e.Exercise, e.Item, e.Attempt, e.File, e.Line, e.Text, Shared: true, e.Timestamp)
            { Received = true }).ToList();
    }
}
