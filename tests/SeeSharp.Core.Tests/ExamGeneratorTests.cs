namespace SeeSharp.Core.Tests;

public sealed class ExamGeneratorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    private static ExamCandidate Candidate(string exercise, string item, params string[] skills) =>
        new(exercise, item, skills.ToHashSet(StringComparer.Ordinal));

    private static Dictionary<string, SkillStatus> Held(params (string Skill, SkillState State)[] skills) =>
        skills.ToDictionary(s => s.Skill, s => new SkillStatus(s.Skill, s.State, T0, "x"), StringComparer.Ordinal);

    private static readonly ExamCandidate[] Candidates =
    [
        Candidate("first", "q1", "a.one"),
        Candidate("first", "q2", "a.one"),
        Candidate("second", "q1", "a.two", "a.three"),
        Candidate("third", "code", "b.four"),
        Candidate("third", "q1", "b.four"),
    ];

    private static readonly HashSet<string> Skills = ["a.one", "a.two", "a.three", "b.four"];

    [Fact]
    public void Every_covered_skill_is_hit_at_least_once_and_picks_come_in_candidate_order()
    {
        var picked = ExamGenerator.Pick(Candidates, Skills, Held(), maxItems: null, seed: 1);

        Assert.Equal(Skills, picked.SelectMany(c => c.Skills).ToHashSet());
        Assert.Equal(3, picked.Count); // one per exercise is enough here
        Assert.Equal(picked, picked.OrderBy(c => Array.IndexOf(Candidates, c)));
    }

    [Fact]
    public void Practiced_but_never_applied_skills_are_picked_first()
    {
        var held = Held(("a.one", SkillState.Applied), ("a.two", SkillState.Applied), ("a.three", SkillState.Applied), ("b.four", SkillState.Practiced));

        for (var seed = 0; seed < 20; seed++)
        {
            var only = Assert.Single(ExamGenerator.Pick(Candidates, Skills, held, maxItems: 1, seed));
            Assert.Equal("third", only.ExerciseId);
        }
    }

    [Fact]
    public void Same_seed_same_exam()
    {
        var a = ExamGenerator.Pick(Candidates, Skills, Held(), null, seed: 42);
        var b = ExamGenerator.Pick(Candidates, Skills, Held(), null, seed: 42);
        Assert.Equal(a, b);

        // Ties (first/q1 vs first/q2, third/code vs third/q1) are broken by the seed, so some other seed picks differently.
        Assert.Contains(Enumerable.Range(0, 50), seed => !ExamGenerator.Pick(Candidates, Skills, Held(), null, seed).SequenceEqual(a));
    }

    [Fact]
    public void Skills_nobody_teaches_do_not_loop_forever()
    {
        var picked = ExamGenerator.Pick(Candidates, new HashSet<string> { "a.one", "z.nowhere" }, Held(), null, seed: 3);
        Assert.Equal(["a.one"], Assert.Single(picked).Skills);
    }
}
