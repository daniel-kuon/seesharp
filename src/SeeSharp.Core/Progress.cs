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
    /// <summary>Set when the attempt was made inside an exam run; such attempts never feed regular progress.</summary>
    public long? ExamRunId { get; init; }
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
    /// <summary>A quiz question gets this many tries; after that it is locked with the correct answer shown.</summary>
    public const int QuizAttempts = 2;

    /// <summary>
    /// Items that are done: any passing attempt, or a question (attempt without source) that used up its
    /// <see cref="QuizAttempts"/> — a locked wrong answer must not block the trainee forever.
    /// </summary>
    public static IReadOnlySet<string> PassedItems(IEnumerable<Attempt> attempts, string exerciseId) =>
        attempts.Where(a => a.ExerciseId == exerciseId).GroupBy(a => a.ItemId)
            .Where(g => g.Any(a => a.Passed) || g.Count(a => a.Source is null && !a.Passed) >= QuizAttempts)
            .Select(g => g.Key).ToHashSet(StringComparer.Ordinal);

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

// ----- exams (task 8) ------------------------------------------------------------------------

/// <summary>One item of an exam run: the exercise and the item inside it.</summary>
public sealed record ExamItemRef(string ExerciseId, string ItemId);

/// <summary>An item the generator may pick, with the covered skills its exercise teaches.</summary>
public sealed record ExamCandidate(string ExerciseId, string ItemId, IReadOnlySet<string> Skills);

/// <summary>
/// One sitting of an exam. The deadline is fixed the moment the run starts and persisted, so closing the app
/// neither resets nor extends it. <see cref="Active"/> is the flag the UI honours: tutor off, test output withheld.
/// </summary>
/// <param name="Real">A real exam: one run per exam id, nothing revealed after submission.</param>
/// <param name="SubmittedAt">Null while the run is open; the deadline itself when it expired unsubmitted.</param>
public sealed record ExamRun(
    long Id,
    string ExamId,
    bool Real,
    DateTimeOffset StartedAt,
    DateTimeOffset Deadline,
    int Seed,
    IReadOnlyList<ExamItemRef> Items,
    DateTimeOffset? SubmittedAt)
{
    public bool Active => SubmittedAt is null;
    public TimeSpan Remaining(DateTimeOffset now) => Deadline > now ? Deadline - now : TimeSpan.Zero;
}

/// <summary>SKILL-MODEL.md §5: an exam covers skills, not a hand-picked item list.</summary>
public static class ExamGenerator
{
    /// <summary>
    /// Greedy set cover over <paramref name="candidates"/>: each pick is the item hitting the most still-uncovered skills
    /// that are <see cref="SkillState.Practiced"/> but never applied, then the most uncovered skills overall; ties go
    /// by a shuffle seeded with <paramref name="seed"/>, so the same seed yields the same exam. Stops when every skill
    /// in <paramref name="skills"/> is hit or <paramref name="maxItems"/> is reached. Returns picks in candidate order.
    /// </summary>
    public static IReadOnlyList<ExamCandidate> Pick(
        IReadOnlyList<ExamCandidate> candidates, IReadOnlySet<string> skills, IReadOnlyDictionary<string, SkillStatus> held, int? maxItems, int seed)
    {
        var priority = skills.Where(s => held.TryGetValue(s, out var status) && status.State == SkillState.Practiced).ToHashSet(StringComparer.Ordinal);
        var uncovered = skills.ToHashSet(StringComparer.Ordinal);
        var order = Enumerable.Range(0, candidates.Count).ToArray();
        new Random(seed).Shuffle(order);
        var picked = new HashSet<int>();
        while (uncovered.Count > 0 && (maxItems is null || picked.Count < maxItems))
        {
            var best = order.Where(i => !picked.Contains(i))
                .Select(i => (Index: i, Priority: candidates[i].Skills.Count(s => uncovered.Contains(s) && priority.Contains(s)), Hits: candidates[i].Skills.Count(uncovered.Contains)))
                .Where(c => c.Hits > 0)
                .OrderByDescending(c => c.Priority).ThenByDescending(c => c.Hits) // stable: the shuffle breaks ties
                .FirstOrDefault();
            if (best.Hits == 0)
                break; // the remaining skills are taught by no candidate
            picked.Add(best.Index);
            uncovered.ExceptWith(candidates[best.Index].Skills);
        }
        return picked.Order().Select(i => candidates[i]).ToList();
    }
}
