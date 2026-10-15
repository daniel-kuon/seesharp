using SeeSharp.Content;
using SeeSharp.Core;

namespace SeeSharp.Progress.Tests;

/// <summary>Every test gets its own temp database file; the real %LOCALAPPDATA% is never touched.</summary>
public sealed class ProgressStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "seesharp-progress-tests-" + Guid.NewGuid().ToString("N"));
    private string DbPath => Path.Combine(_dir, "Fortschritt äöü", "progress.db");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    private static Attempt Attempt(string exercise, string item, bool passed, int minute = 0, string? answer = null) =>
        new(exercise, item, T0.AddMinutes(minute), TimeSpan.FromSeconds(42), passed, answer);

    private static ExerciseManifest Manifest(string id, string[] teaches, string[] requires, params string[] items) => new()
    {
        Id = id,
        Teaches = [.. teaches],
        Requires = [.. requires],
        Items = items.Select(i => (ExerciseItem)new OpenItem { Type = "open", Id = i }).ToList(),
    };

    private static Course Course(params (string Module, ExerciseManifest[] Exercises)[] modules) =>
        new("/course", new SkillCatalog(), modules.Select((m, i) => new Module($"/course/modules/{i + 1:00}-{m.Module}", i + 1,
            new ModuleManifest { Id = m.Module },
            m.Exercises.Select((e, j) => new Exercise($"/course/modules/{i + 1:00}-{m.Module}/{j + 1:00}-{e.Id}", j + 1, e)).ToList())).ToList());

    [Fact]
    public void Creates_the_file_and_migrates_idempotently()
    {
        using (var first = new ProgressStore(DbPath))
            first.RecordAttempt(Attempt("ex", "q1", true));
        Assert.True(File.Exists(DbPath));

        using var second = new ProgressStore(DbPath);
        Assert.Single(second.Attempts());
    }

    [Fact]
    public void Deleting_the_file_loses_progress_but_the_store_comes_back()
    {
        using (var store = new ProgressStore(DbPath))
            store.RecordAttempt(Attempt("ex", "q1", true));
        File.Delete(DbPath);

        using var fresh = new ProgressStore(DbPath);
        Assert.Empty(fresh.Attempts());
        Assert.Empty(fresh.SkillLedger());
    }

    [Fact]
    public void Attempts_round_trip_every_column()
    {
        using var store = new ProgressStore(DbPath);
        var source = new byte[] { 1, 2, 3 };
        var attempt = new Attempt("ex", "code", T0, TimeSpan.FromMinutes(3), false, null, source, "{\"outcome\":\"Failed\"}", "<TestRun/>");

        var recorded = store.RecordAttempt(attempt);
        store.RecordAttempt(Attempt("ex", "q1", true, 1, "Weil."));

        Assert.True(recorded.Id > 0);
        var stored = store.Attempts();
        Assert.Equal(2, stored.Count);
        Assert.Equal(source, stored[0].Source); // byte[] compares by reference in record equality, so check it apart
        Assert.Equal(attempt with { Id = recorded.Id, Source = null }, stored[0] with { Source = null });
        Assert.Equal("Weil.", stored[1].Answer);
        Assert.Equal(["code"], store.Attempts("ex").Select(a => a.ItemId).Take(1));
        Assert.Empty(store.Attempts("other"));
    }

    [Fact]
    public void Passing_every_item_credits_teaches_as_practiced_and_requires_as_applied()
    {
        using var store = new ProgressStore(DbPath);
        var first = Manifest("first", ["a.one"], [], "q1", "q2");
        var second = Manifest("second", ["b.two"], ["a.one"], "q1");

        store.RecordAttempt(Attempt("first", "q1", true, 1), first);
        Assert.Empty(store.SkillLedger()); // q2 still open
        store.RecordAttempt(Attempt("first", "q2", false, 2), first);
        Assert.Empty(store.SkillLedger());
        store.RecordAttempt(Attempt("first", "q2", true, 3), first);
        store.RecordAttempt(Attempt("first", "q2", true, 4), first); // re-pass adds nothing
        store.RecordAttempt(Attempt("second", "q1", true, 5), second);

        var ledger = store.SkillLedger();
        Assert.Equal(
        [
            ("a.one", SkillState.Practiced, "first"),
            ("b.two", SkillState.Practiced, "second"),
            ("a.one", SkillState.Applied, "second"),
        ], ledger.Select(t => (t.SkillId, t.State, t.Cause)));
        Assert.Equal(T0.AddMinutes(3), ledger[0].Timestamp);

        var held = store.HeldSkills();
        Assert.Equal((SkillState.Applied, "second"), (held["a.one"].State, held["a.one"].Cause));
        Assert.Equal(["q1", "q2"], store.PassedItems("first").Order());
    }

    [Fact]
    public void Record_with_the_wrong_manifest_is_rejected()
    {
        using var store = new ProgressStore(DbPath);
        Assert.Throws<ArgumentException>(() => store.RecordAttempt(Attempt("first", "q1", true), Manifest("second", [], [], "q1")));
        Assert.Empty(store.Attempts());
    }

    [Fact]
    public void Reading_a_summary_introduces_its_skills_once()
    {
        using var store = new ProgressStore(DbPath);
        var course = Course(("basics", [Manifest("first", ["a.one", "a.two"], [], "q1"), Manifest("second", ["a.two", "b.three"], ["a.one"], "q1")]));
        store.RecordAttempt(Attempt("first", "q1", true), course.Modules[0].Exercises[0].Manifest);

        store.MarkSummaryRead(course.Modules[0], T0.AddMinutes(1));
        store.MarkSummaryRead(course.Modules[0], T0.AddMinutes(2));

        var ledger = store.SkillLedger();
        Assert.Equal([("a.one", SkillState.Practiced), ("a.two", SkillState.Practiced), ("b.three", SkillState.Introduced)],
            ledger.Select(t => (t.SkillId, t.State)));
        Assert.Equal("basics", ledger[2].Cause);
    }

    [Fact]
    public void Query_derives_lock_state_from_held_skills_and_passed_items()
    {
        using var store = new ProgressStore(DbPath);
        var course = Course(
            ("basics", [Manifest("first", ["a.one"], [], "q1"), Manifest("second", ["a.two"], ["a.one"], "q1", "q2")]),
            ("later", [Manifest("third", ["c.three"], ["a.one", "a.two"], "q1")]));

        var before = store.Query(course);
        Assert.Equal([true, false], before.Modules[0].Exercises.Select(e => e.Unlocked));
        Assert.Equal(["a.one"], before.Modules[0].Exercises[1].MissingSkills);
        Assert.True(before.Modules[0].Unlocked);
        Assert.False(before.Modules[1].Unlocked);
        Assert.Empty(before.HeldSkills);

        store.RecordAttempt(Attempt("first", "q1", true, 1), course.Modules[0].Exercises[0].Manifest);
        store.RecordAttempt(Attempt("second", "q1", true, 2), course.Modules[0].Exercises[1].Manifest);

        var after = store.Query(course);
        var second = after.Modules[0].Exercises[1];
        Assert.True(after.Modules[0].Exercises[0].Passed);
        Assert.Equal((true, false), (second.Unlocked, second.Passed));
        Assert.Equal(["q1"], second.PassedItems);
        Assert.False(after.Modules[1].Unlocked);
        Assert.Equal(["a.two"], after.Modules[1].Exercises[0].MissingSkills);
        Assert.Equal(SkillState.Practiced, after.HeldSkills["a.one"].State);
    }
}
