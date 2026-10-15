namespace SeeSharp.Core.Tests;

public class ProgressDerivationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(int minutes) => T0.AddMinutes(minutes);

    [Fact]
    public void Held_skills_take_the_highest_state_and_the_latest_cause()
    {
        var held = ProgressDerivation.HeldSkills(
        [
            new("a.one", SkillState.Practiced, At(2), "ex-1"),
            new("a.one", SkillState.Introduced, At(1), "mod-1"),
            new("a.one", SkillState.Applied, At(3), "ex-2"),
            new("a.two", SkillState.Introduced, At(1), "mod-1"),
        ]);

        Assert.Equal((SkillState.Applied, At(3), "ex-2"), (held["a.one"].State, held["a.one"].LastExercised, held["a.one"].Cause));
        Assert.Equal(SkillState.Introduced, held["a.two"].State);
        Assert.Equal(2, held.Count);
    }

    [Fact]
    public void Passed_items_need_one_passing_attempt_for_that_exercise()
    {
        var attempts = new Attempt[]
        {
            new("ex-1", "q1", At(0), TimeSpan.FromSeconds(5), Passed: false),
            new("ex-1", "q1", At(1), TimeSpan.FromSeconds(5), Passed: true),
            new("ex-1", "q2", At(2), TimeSpan.FromSeconds(5), Passed: false),
            new("ex-2", "q2", At(3), TimeSpan.FromSeconds(5), Passed: true),
        };

        Assert.Equal(["q1"], ProgressDerivation.PassedItems(attempts, "ex-1"));
        Assert.Equal(["q2"], ProgressDerivation.PassedItems(attempts, "ex-2"));
        Assert.Empty(ProgressDerivation.PassedItems(attempts, "ex-3"));
    }

    [Fact]
    public void Introduced_does_not_unlock_but_practiced_and_applied_do()
    {
        var held = ProgressDerivation.HeldSkills(
        [
            new("a.one", SkillState.Introduced, At(0), "mod-1"),
            new("a.two", SkillState.Practiced, At(1), "ex-1"),
            new("a.three", SkillState.Applied, At(2), "ex-2"),
        ]);

        Assert.Equal(["a.one", "a.four"], ProgressDerivation.MissingSkills(["a.one", "a.two", "a.three", "a.four"], held));
        Assert.Empty(ProgressDerivation.MissingSkills([], held));
    }

    [Fact]
    public void Transitions_only_for_skills_below_the_target_state()
    {
        var held = ProgressDerivation.HeldSkills(
        [
            new("a.one", SkillState.Practiced, At(0), "ex-1"),
            new("a.two", SkillState.Introduced, At(0), "mod-1"),
        ]);

        var practiced = ProgressDerivation.Transitions(["a.one", "a.two", "a.three"], SkillState.Practiced, "ex-2", At(5), held);
        Assert.Equal(["a.two", "a.three"], practiced.Select(t => t.SkillId));
        Assert.All(practiced, t => Assert.Equal((SkillState.Practiced, At(5), "ex-2"), (t.State, t.Timestamp, t.Cause)));

        Assert.Equal(["a.one"], ProgressDerivation.Transitions(["a.one"], SkillState.Applied, "ex-2", At(5), held).Select(t => t.SkillId));
        Assert.Empty(ProgressDerivation.Transitions(["a.one"], SkillState.Introduced, "mod-2", At(5), held));
    }
}
