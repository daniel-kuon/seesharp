using SeeSharp.Core;

namespace SeeSharp.Progress.Tests;

/// <summary>Reviewer mode: the report carries trainee and sources, remarks stay private unless shared, and the reply brings only shared ones back.</summary>
public sealed class RemarksTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "seesharp-remarks-tests-" + Guid.NewGuid().ToString("N"));

    public RemarksTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Report_carries_the_trainee_and_the_text_sources_of_each_attempt()
    {
        using var store = new ProgressStore(Path.Combine(_dir, "trainee.db"));
        var source = Path.Combine(_dir, "Schere, Stein, Papier");
        Directory.CreateDirectory(Path.Combine(source, "Spiel"));
        File.WriteAllText(Path.Combine(source, "Spiel", "Program.cs"), "var a = 1;\r\nvar b = 2;\r\n");
        File.WriteAllBytes(Path.Combine(source, "icon.bin"), [1, 0, 2]);
        File.WriteAllText(Path.Combine(source, "huge.txt"), new string('a', SourceFile.MaxBytes + 1)); // compresses to almost nothing
        var attempt = store.RecordAttempt(new Attempt("ex", "code", DateTimeOffset.UtcNow, TimeSpan.Zero, false, Source: Attempts.Zip(source)));
        store.RecordAttempt(new Attempt("ex", "q1", DateTimeOffset.UtcNow, TimeSpan.Zero, true, "Antwort"));
        var zip = Path.Combine(_dir, "report.zip");
        ReviewExport.Write(store, zip);

        using var stream = File.OpenRead(zip);
        var bundle = ReviewBundle.Read(stream);

        Assert.Equal(ReviewExport.Trainee, bundle.Trainee);
        var coding = bundle.Attempts.Single(a => a.Id == attempt.Id);
        var file = Assert.Single(coding.Files); // the binary and the oversized file are left out
        Assert.Equal("Spiel/Program.cs", file.Path);
        Assert.Equal(["var a = 1;", "var b = 2;"], file.Lines);
        Assert.Empty(bundle.Attempts.Single(a => a.Item == "q1").Files);
    }

    [Fact]
    public void Only_shared_remarks_reach_the_trainee_and_a_new_reply_replaces_the_old_one()
    {
        using var supervisor = new ProgressStore(Path.Combine(_dir, "supervisor.db"));
        var now = DateTimeOffset.UtcNow;
        var line = supervisor.AddRemark(new Remark("anna@pc", "ex", "code", 7, "Spiel/Program.cs", 2, "Warum b?", Shared: false, now));
        supervisor.AddRemark(new Remark("anna@pc", "ex", null, null, null, null, "Gut gemacht.", Shared: true, now));
        supervisor.AddRemark(new Remark("anna@pc", "ex", "q1", 8, null, null, "Nur intern.", Shared: false, now));
        supervisor.AddRemark(new Remark("ben@pc", "ex", null, null, null, null, "Anderer Trainee.", Shared: true, now));
        supervisor.SetRemarkShared(line.Id, true);
        Assert.Equal(3, supervisor.Remarks("anna@pc").Count);

        var reply = Path.Combine(_dir, "reply.json");
        Assert.Equal(2, RemarksReply.Write(supervisor, "anna@pc", reply));
        Assert.DoesNotContain("Nur intern.", File.ReadAllText(reply));

        using var trainee = new ProgressStore(Path.Combine(_dir, "trainee.db"));
        trainee.AddRemark(new Remark("anna@pc", "ex", null, null, null, null, "Eigene, nicht empfangen.", Shared: true, now));
        using (var stream = File.OpenRead(reply))
            trainee.ReplaceReceivedRemarks(RemarksReply.Read(stream));
        var received = trainee.ReceivedRemarks("ex");
        Assert.Equal(["Warum b?", "Gut gemacht."], received.Select(r => r.Text));
        Assert.Equal((7L, "Spiel/Program.cs", 2), (received[0].AttemptId, received[0].File, received[0].Line));
        Assert.All(received, r => Assert.True(r.Received && r.Shared));

        supervisor.DeleteRemark(line.Id);
        RemarksReply.Write(supervisor, "anna@pc", reply);
        using (var stream = File.OpenRead(reply))
            trainee.ReplaceReceivedRemarks(RemarksReply.Read(stream));
        Assert.Equal(["Gut gemacht."], trainee.ReceivedRemarks().Select(r => r.Text));
        Assert.Single(trainee.Remarks("anna@pc")); // written remarks are not touched by an import
    }

    [Fact]
    public void Other_json_is_not_a_reply()
    {
        using var stream = new MemoryStream("""{"format":"something-else","trainee":"x","remarks":[]}"""u8.ToArray());
        Assert.Throws<InvalidDataException>(() => RemarksReply.Read(stream));
    }
}
