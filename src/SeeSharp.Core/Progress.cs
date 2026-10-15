namespace SeeSharp.Core;

/// <summary>SKILL-MODEL.md §2. Ordered: a skill only ever moves up.</summary>
public enum SkillState
{
    Introduced = 1,
    Practiced = 2,
    Applied = 3,
}

/// <summary>
/// One submission of one item. Stored as-is, never overwritten; everything else is derived.
/// Exactly one of <paramref name="Answer"/> (multipleChoice/open/review) or <paramref name="Source"/> (coding, zip) is set.
/// </summary>
/// <param name="Passed">Decided by the caller when the attempt is made (tests passed, answer correct, question answered, review marked done).</param>
/// <param name="Source">Zip of the trainee's source directory at submission time.</param>
/// <param name="GradingJson">The serialised grading result for coding items.</param>
/// <param name="Trx">Raw TRX text of the grading run, when the run produced one.</param>
public sealed record Attempt(
    string ExerciseId,
    string ItemId,
    DateTimeOffset Timestamp,
    TimeSpan TimeSpent,
    bool Passed,
    string? Answer = null,
    byte[]? Source = null,
    string? GradingJson = null,
    string? Trx = null)
{
    /// <summary>Assigned by the store; 0 until recorded.</summary>
    public long Id { get; init; }
}

/// <summary>One row of the skill ledger: a skill entered <paramref name="State"/> because of <paramref name="Cause"/>.</summary>
/// <param name="Cause">The exercise id (practiced/applied) or module id (introduced) that caused the transition.</param>
public sealed record SkillTransition(string SkillId, SkillState State, DateTimeOffset Timestamp, string Cause);

/// <summary>Current state of a held skill, derived from its ledger rows.</summary>
/// <param name="LastExercised">Timestamp of the latest transition.</param>
/// <param name="Cause">Cause of the latest transition.</param>
public sealed record SkillStatus(string SkillId, SkillState State, DateTimeOffset LastExercised, string Cause);

/// <summary>Pure derivation of current state from history. The store feeds it rows; the UI reads the result.</summary>
public static class ProgressDerivation
{
    /// <summary>Highest state per skill; the latest transition supplies the timestamp and cause.</summary>
    public static IReadOnlyDictionary<string, SkillStatus> HeldSkills(IEnumerable<SkillTransition> ledger)
    {
        var held = new Dictionary<string, SkillStatus>(StringComparer.Ordinal);
        foreach (var t in ledger.OrderBy(t => t.Timestamp))
        {
            var state = held.TryGetValue(t.SkillId, out var current) && current.State > t.State ? current.State : t.State;
            held[t.SkillId] = new SkillStatus(t.SkillId, state, t.Timestamp, t.Cause);
        }
        return held;
    }

    /// <summary>Item ids with at least one passing attempt, for one exercise.</summary>
    public static IReadOnlySet<string> PassedItems(IEnumerable<Attempt> attempts, string exerciseId) =>
        attempts.Where(a => a.Passed && a.ExerciseId == exerciseId).Select(a => a.ItemId).ToHashSet(StringComparer.Ordinal);

    /// <summary>Skills in <paramref name="requires"/> not held at least <see cref="SkillState.Practiced"/>. Empty means unlocked.</summary>
    public static IReadOnlyList<string> MissingSkills(IEnumerable<string> requires, IReadOnlyDictionary<string, SkillStatus> held) =>
        requires.Where(s => !held.TryGetValue(s, out var status) || status.State < SkillState.Practiced).ToList();

    /// <summary>
    /// Ledger rows to append when a skill set reaches <paramref name="state"/> because of <paramref name="cause"/>:
    /// one per skill whose current state is below it. Nothing for skills already there, so re-passing adds no rows.
    /// </summary>
    public static IReadOnlyList<SkillTransition> Transitions(
        IEnumerable<string> skills, SkillState state, string cause, DateTimeOffset timestamp, IReadOnlyDictionary<string, SkillStatus> held) =>
        skills.Where(s => !held.TryGetValue(s, out var status) || status.State < state)
            .Select(s => new SkillTransition(s, state, timestamp, cause))
            .ToList();
}
