using System.Text.Json.Serialization;

namespace SeeSharp.Grading;

public enum GradingOutcome
{
    Passed,
    Failed,
    BuildFailed,
    TimedOut,
}

public enum TestOutcome
{
    Passed,
    Failed,
    Skipped,
}

public sealed record TestResult(string Name, TestOutcome Outcome, TimeSpan Duration, string? Message, string? StackTrace);

/// <param name="Duration">Wall-clock time of the whole <c>dotnet test</c> run, build included.</param>
/// <param name="BuildErrors">Distinct compiler/MSBuild error lines when the build failed.</param>
/// <param name="Output">Combined stdout and stderr of the child process.</param>
/// <param name="ArtifactsDirectory">The kept working directory, or null when it was deleted.</param>
public sealed record GradingResult(
    GradingOutcome Outcome,
    TimeSpan Duration,
    IReadOnlyList<TestResult> Tests,
    IReadOnlyList<string> BuildErrors,
    string Output,
    string? ArtifactsDirectory)
{
    public int Passed => Tests.Count(t => t.Outcome == TestOutcome.Passed);
    public int Failed => Tests.Count(t => t.Outcome == TestOutcome.Failed);

    /// <summary>Raw text of <c>results.trx</c>, kept for the review bundle; null when the build failed. Not part of the JSON output.</summary>
    [JsonIgnore]
    public string? Trx { get; init; }
}
