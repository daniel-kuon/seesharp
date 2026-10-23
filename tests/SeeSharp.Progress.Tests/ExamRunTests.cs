using System.IO.Compression;
using SeeSharp.Content;
using SeeSharp.Core;

namespace SeeSharp.Progress.Tests;

/// <summary>Exam mode (task 8): one open run at a time, deadline in the DB, real exams once, exam attempts apart from progress.</summary>
public sealed class ExamRunTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "seesharp-exam-tests-" + Guid.NewGuid().ToString("N"));
    private string DbPath => Path.Combine(_dir, "progress.db");

    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
    private static readonly ExamItemRef[] Items = [new("first", "q1"), new("second", "q1")];

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private static ExerciseManifest Manifest(string id, string[] teaches, string[] requires, params string[] items) => new()
    {
        Id = id,
        Teaches = [.. teaches],
        Requires = [.. requires],
        Items = items.Select(i => (ExerciseItem)new OpenItem { Type = "open", Id = i }).ToList(),
    };

    /// <summary>Two levels; the exam covers level 1 (first, second), level 2 (third) stays outside.</summary>
    private static Course Course(params ExamDefinition[] exams)
    {
        var first = Manifest("first", ["a.one"], [], "q1");
        var second = Manifest("second", ["a.two"], ["a.one"], "q1", "r1");
        second.Items[1] = new ReviewItem { Type = "review", Id = "r1" };
        var third = Manifest("third", ["c.three"], ["a.one", "a.two"], "q1");
        return new Course("/course", new SkillCatalog(),
        [
            new Module("/course/modules/01-basics", 1, new ModuleManifest { Id = "basics" },
                [new Exercise("/course/modules/01-basics/01-first", 1, first), new Exercise("/course/modules/01-basics/02-second", 2, second)]),
            new Module("/course/modules/02-later", 2, new ModuleManifest { Id = "later" }, [new Exercise("/course/modules/02-later/01-third", 1, third)]),
        ]) { Exams = exams };
    }

    private static ExamDefinition Practice => new() { Id = "probe", Kind = "practice", Coverage = new ExamCoverage { Levels = [1] }, TimeLimitMinutes = 30 };
    private static ExamDefinition Real => new() { Id = "ernst", Kind = "real", Coverage = new ExamCoverage { Levels = [1] }, TimeLimitMinutes = 45 };

    private static void PassLevelOne(ProgressStore store, Course course)
    {
        store.RecordAttempt(new Attempt("first", "q1", T0, TimeSpan.Zero, true, "x"), course.Modules[0].Exercises[0].Manifest);
        store.RecordAttempt(new Attempt("second", "q1", T0, TimeSpan.Zero, true, "x"), course.Modules[0].Exercises[1].Manifest);
        store.RecordAttempt(new Attempt("second", "r1", T0, TimeSpan.Zero, true), course.Modules[0].Exercises[1].Manifest);
    }

    [Fact]
    public void Deadline_is_persisted_at_start_and_survives_a_reopen()
    {
        long id;
        using (var store = new ProgressStore(DbPath))
        {
            var run = store.StartExamRun("probe", real: false, TimeSpan.FromMinutes(30), seed: 7, Items, T0);
            id = run.Id;
            Assert.Equal(T0.AddMinutes(30), run.Deadline);
            Assert.True(run.Active);
        }
        using (var store = new ProgressStore(DbPath))
        {
            var run = store.ActiveRun(T0.AddMinutes(29));
            Assert.NotNull(run);
            Assert.Equal((id, T0.AddMinutes(30), 7), (run.Id, run.Deadline, run.Seed));
            Assert.Equal(Items, run.Items);
            Assert.Equal(TimeSpan.FromMinutes(1), run.Remaining(T0.AddMinutes(29)));
        }
    }

    [Fact]
    public void Expired_run_is_submitted_on_the_next_request_and_takes_no_more_attempts()
    {
        using var store = new ProgressStore(DbPath);
        var run = store.StartExamRun("probe", false, TimeSpan.FromMinutes(30), 1, Items, T0);
        store.RecordAttempt(new Attempt("first", "q1", T0.AddMinutes(5), TimeSpan.Zero, true, "early") { ExamRunId = run.Id });

        Assert.Null(store.ActiveRun(T0.AddMinutes(30)));
        var expired = store.ExamRun(run.Id)!;
        Assert.Equal(run.Deadline, expired.SubmittedAt);
        Assert.Throws<InvalidOperationException>(() =>
            store.RecordAttempt(new Attempt("first", "q1", T0.AddMinutes(31), TimeSpan.Zero, true, "late") { ExamRunId = run.Id }));
        Assert.Equal(["early"], store.ExamAttempts(run.Id).Select(a => a.Answer));
    }

    [Fact]
    public void Real_exam_starts_once_and_only_one_run_is_open_at_a_time()
    {
        using var store = new ProgressStore(DbPath);
        var real = store.StartExamRun("ernst", true, TimeSpan.FromMinutes(45), 1, Items, T0);
        Assert.Throws<InvalidOperationException>(() => store.StartExamRun("probe", false, TimeSpan.FromMinutes(30), 1, Items, T0.AddMinutes(1)));
        store.SubmitExamRun(real.Id, T0.AddMinutes(10));
        Assert.Equal(T0.AddMinutes(10), store.ExamRun(real.Id)!.SubmittedAt);

        Assert.Throws<InvalidOperationException>(() => store.StartExamRun("ernst", true, TimeSpan.FromMinutes(45), 2, Items, T0.AddMinutes(11)));
        var practice = store.StartExamRun("probe", false, TimeSpan.FromMinutes(30), 1, Items, T0.AddMinutes(11));
        store.SubmitExamRun(practice.Id, T0.AddMinutes(12));
        store.StartExamRun("probe", false, TimeSpan.FromMinutes(30), 3, Items, T0.AddMinutes(13)); // practice: retake freely
        Assert.Equal(3, store.ExamRuns().Count);
    }

    [Fact]
    public void Exam_attempts_stay_out_of_regular_progress()
    {
        using var store = new ProgressStore(DbPath);
        var course = Course(Practice);
        var run = store.StartExamRun("probe", false, TimeSpan.FromMinutes(30), 1, Items, T0);
        store.RecordAttempt(new Attempt("first", "q1", T0.AddMinutes(1), TimeSpan.Zero, true, "in exam") { ExamRunId = run.Id });

        Assert.Empty(store.Attempts());
        Assert.Empty(store.PassedItems("first"));
        Assert.Empty(store.HeldSkills());
        Assert.False(store.Query(course).Modules[0].Exercises[0].Passed);
        Assert.Equal(run.Id, Assert.Single(store.ExamAttempts(run.Id)).ExamRunId);
    }

    [Fact]
    public void Status_locks_until_every_covered_exercise_is_passed_then_follows_the_runs()
    {
        using var store = new ProgressStore(DbPath);
        var course = Course(Practice, Real);

        Assert.Equal(ExamState.Locked, Exams.Status(store, course, store.Query(course), Practice, T0).State);
        PassLevelOne(store, course);
        Assert.Equal(ExamState.Available, Exams.Status(store, course, store.Query(course), Practice, T0).State);
        Assert.Equal(ExamState.Available, Exams.Status(store, course, store.Query(course), Real, T0).State);

        var run = Exams.Start(store, course, Practice, seed: 5, now: T0);
        // Every covered skill (a.one, a.two) is hit; the review item is never picked.
        Assert.Equal([new ExamItemRef("first", "q1"), new ExamItemRef("second", "q1")], run.Items);
        Assert.Equal(T0.AddMinutes(30), run.Deadline);
        var status = Exams.Status(store, course, store.Query(course), Practice, T0.AddMinutes(1));
        Assert.Equal((ExamState.Running, run.Id), (status.State, status.Run!.Id));

        store.SubmitExamRun(run.Id, T0.AddMinutes(2));
        status = Exams.Status(store, course, store.Query(course), Practice, T0.AddMinutes(3));
        Assert.Equal((ExamState.Submitted, run.Id), (status.State, status.Run!.Id));
        Assert.Equal(ExamState.Available, Exams.Status(store, course, store.Query(course), Real, T0.AddMinutes(3)).State);
    }

    [Fact]
    public void Export_bundle_contains_the_run_apart_from_regular_attempts()
    {
        using var store = new ProgressStore(DbPath);
        store.RecordAttempt(new Attempt("first", "q1", T0, TimeSpan.Zero, true, "regular"));
        var run = store.StartExamRun("ernst", true, TimeSpan.FromMinutes(45), 9, Items, T0.AddMinutes(1));
        var attempt = store.RecordAttempt(new Attempt("second", "q1", T0.AddMinutes(2), TimeSpan.FromSeconds(30), false, "in exam") { ExamRunId = run.Id });
        store.SubmitExamRun(run.Id, T0.AddMinutes(3));
        var zipPath = Path.Combine(_dir, "review.zip");

        ReviewExport.Write(store, zipPath);

        using var zip = ZipFile.OpenRead(zipPath);
        var names = zip.Entries.Select(e => e.FullName).ToList();
        var folder = $"exams/{run.Id:D4}/";
        Assert.Contains(folder + "run.csv", names);
        Assert.Contains(folder + "attempts.csv", names);
        Assert.Contains(folder + $"second/q1/{attempt.Id:D4}_20261015-090200/answer.txt", names);
        var runCsv = Read(zip, folder + "run.csv").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("id,exam,kind,started,deadline,submitted,seed,items", runCsv[0].TrimEnd());
        Assert.Equal($"{run.Id},ernst,real,2026-10-15T09:01:00Z,2026-10-15T09:46:00Z,2026-10-15T09:03:00Z,9,first/q1 second/q1", runCsv[1].TrimEnd());
        Assert.DoesNotContain("in exam", Read(zip, "attempts.csv"));
        Assert.Contains("in exam", Read(zip, folder + "attempts.csv"));
        Assert.Contains("regular", Read(zip, "attempts.csv"));
    }

    private static string Read(ZipArchive zip, string name)
    {
        using var reader = new StreamReader(zip.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void Optional_exercises_neither_lock_an_exam_nor_appear_in_it()
    {
        using var store = new ProgressStore(DbPath);
        var course = Course(Practice);
        var extra = Manifest("extra", ["a.two"], ["a.one"], "q1");
        extra.Optional = true;
        var basics = course.Modules[0];
        course = course with { Modules = [basics with { Exercises = [.. basics.Exercises, new Exercise("/course/modules/01-basics/10-extra", 10, extra)] }, course.Modules[1]] };
        PassLevelOne(store, course);

        Assert.Equal(ExamState.Available, Exams.Status(store, course, store.Query(course), Practice, T0).State);
        Assert.DoesNotContain(ExamScope.CoveredExercises(course, Practice), e => e.Manifest.Optional);
        for (var seed = 0; seed < 10; seed++) // extra ties with second on a.two; without the filter some seed would pick it
        {
            var run = Exams.Start(store, course, Practice, seed, T0);
            Assert.DoesNotContain(run.Items, i => i.ExerciseId == "extra");
            store.SubmitExamRun(run.Id, T0);
        }
    }
}
