namespace SeeSharp.Grading.Tests;

// Each test spawns a real `dotnet test` (restore + build + run), so they are intentionally
// in one class to run sequentially; the timeout test must not compete for CPU with the others.
public class GradingRunnerTests
{
    private static readonly string Fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Übung #1, Rechner");
    private static readonly string Exercise = Path.Combine(Fixture, "exercise");

    private static Task<GradingResult> Grade(string source, TimeSpan? timeout = null, bool keepArtifacts = false) =>
        new GradingRunner().RunAsync(Exercise, Path.Combine(Fixture, source), timeout, keepArtifacts);

    [Fact]
    public async Task All_tests_pass()
    {
        var result = await Grade("source-pass");

        Assert.Equal(GradingOutcome.Passed, result.Outcome);
        Assert.Equal(4, result.Passed);
        Assert.Equal(0, result.Failed);
        Assert.All(result.Tests, t => Assert.Equal(TestOutcome.Passed, t.Outcome));
        Assert.Null(result.ArtifactsDirectory);
    }

    [Fact]
    public async Task Failing_tests_surface_assertion_messages()
    {
        var result = await Grade("source-fail");

        Assert.Equal(GradingOutcome.Failed, result.Outcome);
        Assert.Equal(2, result.Passed);
        Assert.Equal(2, result.Failed);
        var subtrahiere = Assert.Single(result.Tests, t => t.Name == "RechnerTests.Subtrahiere_zieht_ab");
        Assert.Equal(TestOutcome.Failed, subtrahiere.Outcome);
        Assert.Contains("Expected: -1", subtrahiere.Message);
        Assert.Contains("Actual:   5", subtrahiere.Message);
        Assert.Contains("RechnerTests.Subtrahiere_zieht_ab", subtrahiere.StackTrace);
    }

    [Fact]
    public async Task Source_that_does_not_compile_is_a_build_failure()
    {
        var result = await Grade("source-broken");

        Assert.Equal(GradingOutcome.BuildFailed, result.Outcome);
        Assert.Empty(result.Tests);
        Assert.Contains(result.BuildErrors, e => e.Contains("error CS1002"));
    }

    [Fact]
    public async Task Hanging_test_run_is_killed_on_timeout()
    {
        var timeout = TimeSpan.FromSeconds(40);
        var result = await Grade("source-hang", timeout);

        Assert.Equal(GradingOutcome.TimedOut, result.Outcome);
        Assert.True(result.Duration >= timeout, $"finished after {result.Duration}");
        Assert.True(result.Duration < timeout + TimeSpan.FromSeconds(10), $"kill took too long: {result.Duration}");
    }

    [Fact]
    public async Task Pristine_tests_win_over_the_trainees_edited_copy()
    {
        // source-tampered: trivial test file, a correct Rechner.cs smuggled into the test project,
        // and a Directory.Build.props that removes the real tests from the build.
        var result = await Grade("source-tampered");

        Assert.Equal(GradingOutcome.Failed, result.Outcome);
        Assert.DoesNotContain(result.Tests, t => t.Name.Contains("Trivial"));
        Assert.Contains(result.Tests, t => t.Name == "RechnerTests.Subtrahiere_zieht_ab" && t.Outcome == TestOutcome.Failed);
    }

    [Fact]
    public async Task Keep_artifacts_leaves_the_working_directory_with_the_trx()
    {
        var result = await Grade("source-pass", keepArtifacts: true);
        try
        {
            Assert.NotNull(result.ArtifactsDirectory);
            Assert.True(File.Exists(Path.Combine(result.ArtifactsDirectory, "TestResults", "results.trx")));
            Assert.True(File.Exists(Path.Combine(result.ArtifactsDirectory, "Rechner Tests (C#)", "RechnerTests.cs")));
        }
        finally
        {
            Directory.Delete(result.ArtifactsDirectory!, recursive: true);
        }
    }
}
