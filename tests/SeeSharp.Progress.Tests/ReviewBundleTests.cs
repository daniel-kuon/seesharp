using SeeSharp.Content;

namespace SeeSharp.Progress.Tests;

public sealed class ReviewBundleTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "seesharp-bundle-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Exported_bundle_reads_back_with_quotes_commas_and_newlines_intact()
    {
        using var store = new ProgressStore(Path.Combine(_dir, "progress.db"));
        var open = new OpenItem { Type = "open", Id = "q1" };
        var manifest = new ExerciseManifest { Id = "ex", Teaches = ["a.one"], Items = [open] };
        store.RecordAttempt(Attempts.Open("ex", open, "", TimeSpan.FromSeconds(5)), manifest);
        store.RecordAttempt(Attempts.Open("ex", open, "Weil, \"so\".\nZweite Zeile.", TimeSpan.FromSeconds(30)), manifest);
        var zipPath = Path.Combine(_dir, "review.zip");
        ReviewExport.Write(store, zipPath);

        using var stream = File.OpenRead(zipPath);
        var bundle = ReviewBundle.Read(stream);

        Assert.Equal(2, bundle.Attempts.Count);
        Assert.Equal(("ex", "q1", false, ""), (bundle.Attempts[0].Exercise, bundle.Attempts[0].Item, bundle.Attempts[0].Passed, bundle.Attempts[0].Answer));
        Assert.Equal(("ex", "q1", true, 30, "Weil, \"so\".\nZweite Zeile."), (bundle.Attempts[1].Exercise, bundle.Attempts[1].Item, bundle.Attempts[1].Passed, bundle.Attempts[1].TimeSpentSeconds, bundle.Attempts[1].Answer));
        var skill = Assert.Single(bundle.Skills);
        Assert.Equal(("a.one", "practiced", "ex"), (skill.Skill, skill.State, skill.Exercise));
        Assert.EndsWith("Z", skill.LastExercised);
    }

    [Fact]
    public void Csv_parser_handles_the_export_format()
    {
        var rows = ReviewBundle.ParseCsv("a,b\r\n1,\"x,\"\"y\"\"\nz\"\n,\n");

        Assert.Equal(3, rows.Count);
        Assert.Equal(["a", "b"], rows[0]);
        Assert.Equal(["1", "x,\"y\"\nz"], rows[1]);
        Assert.Equal(["", ""], rows[2]);
    }
}
