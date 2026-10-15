using System.Resources;

namespace SeeSharp.Cli;

internal static class Strings
{
    private static readonly ResourceManager Resources = new("SeeSharp.Cli.Strings", typeof(Strings).Assembly);

    public static string Usage => Resources.GetString(nameof(Usage))!;
    public static string GradeUsage => Resources.GetString(nameof(GradeUsage))!;
    public static string ResultLine => Resources.GetString(nameof(ResultLine))!;
    public static string TestsLine => Resources.GetString(nameof(TestsLine))!;
    public static string ArtifactsLine => Resources.GetString(nameof(ArtifactsLine))!;
    public static string OutcomePassed => Resources.GetString(nameof(OutcomePassed))!;
    public static string OutcomeFailed => Resources.GetString(nameof(OutcomeFailed))!;
    public static string OutcomeBuildFailed => Resources.GetString(nameof(OutcomeBuildFailed))!;
    public static string OutcomeTimedOut => Resources.GetString(nameof(OutcomeTimedOut))!;
    public static string TestPassed => Resources.GetString(nameof(TestPassed))!;
    public static string TestFailed => Resources.GetString(nameof(TestFailed))!;
    public static string TestSkipped => Resources.GetString(nameof(TestSkipped))!;
}
