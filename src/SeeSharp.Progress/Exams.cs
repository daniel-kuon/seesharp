using SeeSharp.Content;
using SeeSharp.Core;

namespace SeeSharp.Progress;

public enum ExamState
{
    /// <summary>A covered exercise is not passed yet.</summary>
    Locked,
    Available,
    Running,
    /// <summary>The latest run is submitted. Practice exams can be started again from here.</summary>
    Submitted,
}

/// <param name="Run">The open run (Running) or the latest one (Submitted); null otherwise.</param>
public sealed record ExamStatus(ExamDefinition Exam, ExamState State, ExamRun? Run);

/// <summary>Exam mode (task 8) over the store: lock state per exam, and generating a run from the course.</summary>
public static class Exams
{
    public static ExamStatus Status(ProgressStore store, Course course, CourseProgress progress, ExamDefinition exam, DateTimeOffset? now = null)
    {
        var runs = store.ExamRuns(exam.Id, now);
        if (runs.FirstOrDefault(r => r.Active) is { } open)
            return new ExamStatus(exam, ExamState.Running, open);
        if (runs.Count > 0)
            return new ExamStatus(exam, ExamState.Submitted, runs[^1]);
        var covered = ExamScope.CoveredExercises(course, exam);
        var passed = progress.Exercises.Where(e => e.Passed).Select(e => e.Exercise).ToHashSet();
        return new ExamStatus(exam, covered.All(passed.Contains) ? ExamState.Available : ExamState.Locked, null);
    }

    /// <summary>
    /// Picks the items (every covered skill at least once, never-applied skills first — <see cref="ExamGenerator"/>) and opens the run.
    /// Review items are never part of an exam: the supervisor grades them. Throws when the store refuses the start.
    /// </summary>
    public static ExamRun Start(ProgressStore store, Course course, ExamDefinition exam, int? seed = null, DateTimeOffset? now = null)
    {
        var skills = ExamScope.CoveredSkills(course, exam);
        var candidates = ExamScope.CoveredExercises(course, exam)
            .SelectMany(e => e.Manifest.Items.Where(i => i is not ReviewItem)
                .Select(i => new ExamCandidate(e.Manifest.Id, i.Id, e.Manifest.Teaches.Where(skills.Contains).ToHashSet(StringComparer.Ordinal))))
            .ToList();
        var actualSeed = seed ?? Random.Shared.Next();
        var picked = ExamGenerator.Pick(candidates, skills, store.HeldSkills(), exam.MaxItems, actualSeed);
        return store.StartExamRun(exam.Id, exam.IsReal, TimeSpan.FromMinutes(exam.TimeLimitMinutes), actualSeed,
            picked.Select(c => new ExamItemRef(c.ExerciseId, c.ItemId)).ToList(), now);
    }
}
