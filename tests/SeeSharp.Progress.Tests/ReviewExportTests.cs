using System.IO.Compression;
using SeeSharp.Content;
using SeeSharp.Core;
using SeeSharp.Grading;

namespace SeeSharp.Progress.Tests;

public sealed class ReviewExportTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "seesharp-export-tests-" + Guid.NewGuid().ToString("N"));

    /// <summary>The sibling content repo; the tests run from <c>tests/x/bin/Debug/net10.0</c>.</summary>
    private static readonly string Course = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "seesharp-course"));
    private static readonly string Exercise = Path.Combine(Course, "modules", "04-klassen-und-objekte", "01-konstruktoren");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Answers_and_skill_matrix_are_exported()
    {
        using var store = new ProgressStore(Path.Combine(_dir, "progress.db"));
        var manifest = new ExerciseManifest { Id = "ex", Teaches = ["a.one"], Items = [new OpenItem { Type = "open", Id = "q1" }] };
        store.RecordAttempt(Attempts.Open("ex", (OpenItem)manifest.Items[0], "Weil, \"so\".", TimeSpan.FromSeconds(30)), manifest);
        var zipPath = Path.Combine(_dir, "review.zip");

        ReviewExport.Write(store, zipPath);

        using var zip = ZipFile.OpenRead(zipPath);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("attempts.csv", names);
        Assert.Contains("skills.csv", names);
        Assert.Equal("Weil, \"so\".", Read(zip, Assert.Single(names, n => n.EndsWith("/answer.txt"))));
        Assert.Contains("\"Weil, \"\"so\"\".\"", Read(zip, "attempts.csv"));
        var skills = Read(zip, "skills.csv").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("skill,state,lastExercised,exercise", skills[0].TrimEnd());
        Assert.StartsWith("a.one,practiced,", skills[1]);
        Assert.EndsWith(",ex", skills[1].TrimEnd());
    }

    [Fact]
    public async Task Graded_coding_attempt_is_exported_with_source_and_trx()
    {
        if (!Directory.Exists(Exercise))
            return; // CI has no sibling content checkout; locally this is the one real end-to-end run
        // The untouched starter must fail its own tests (that is what `seesharp validate` enforces).
        var starter = Path.Combine(Exercise, "starter");
        var result = await new GradingRunner().RunAsync(Path.Combine(Exercise, "tests"), starter);
        Assert.Equal(GradingOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Trx);

        using var store = new ProgressStore(Path.Combine(_dir, "progress.db"));
        var item = new CodingItem { Type = "coding", Id = "bruch", OpenFile = "b2 Konstruktoren/Bruch.cs" };
        var attempt = store.RecordAttempt(Attempts.Coding("klassen-konstruktoren", item, starter, result, TimeSpan.FromMinutes(12)));
        Assert.False(attempt.Passed);
        var zipPath = Path.Combine(_dir, "Review äöü", "review.zip");

        ReviewExport.Write(store, zipPath);

        using var zip = ZipFile.OpenRead(zipPath);
        var folder = $"klassen-konstruktoren/bruch/{attempt.Id:D4}_{attempt.Timestamp.ToUniversalTime():yyyyMMdd-HHmmss}/";
        Assert.Contains("<TestRun", Read(zip, folder + "results.trx"));
        Assert.Contains("\"outcome\": \"Failed\"".Replace(" ", ""), Read(zip, folder + "grading.json").Replace(" ", ""));
        Assert.Equal(File.ReadAllText(Path.Combine(starter, "b2 Konstruktoren", "Bruch.cs")), Read(zip, folder + "source/b2 Konstruktoren/Bruch.cs"));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("/bin/") || e.FullName.Contains("/obj/"));
        Assert.Contains("klassen-konstruktoren,bruch,", Read(zip, "attempts.csv"));
    }

    private static string Read(ZipArchive zip, string name)
    {
        using var reader = new StreamReader(zip.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }
}
