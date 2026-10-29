using System.Resources;

namespace SeeSharp.Cli;

internal static class Strings
{
    private static readonly ResourceManager Resources = new("SeeSharp.Cli.Strings", typeof(Strings).Assembly);

    public static string Usage => Resources.GetString(nameof(Usage))!;
    public static string StartUsage => Resources.GetString(nameof(StartUsage))!;
    public static string Updating => Resources.GetString(nameof(Updating))!;
    public static string GradeUsage => Resources.GetString(nameof(GradeUsage))!;
    public static string ValidateUsage => Resources.GetString(nameof(ValidateUsage))!;
    public static string ExportReviewUsage => Resources.GetString(nameof(ExportReviewUsage))!;
    public static string ExportReviewWritten => Resources.GetString(nameof(ExportReviewWritten))!;
    public static string SeverityError => Resources.GetString(nameof(SeverityError))!;
    public static string SeverityWarning => Resources.GetString(nameof(SeverityWarning))!;
    public static string SummaryHeader => Resources.GetString(nameof(SummaryHeader))!;
    public static string SummaryModuleLine => Resources.GetString(nameof(SummaryModuleLine))!;
    public static string SummaryTotals => Resources.GetString(nameof(SummaryTotals))!;
    public static string SummaryDangling => Resources.GetString(nameof(SummaryDangling))!;
    public static string SummaryDeadEnds => Resources.GetString(nameof(SummaryDeadEnds))!;
    public static string ValidateResultLine => Resources.GetString(nameof(ValidateResultLine))!;
    public static string SummaryExamsHeader => Resources.GetString(nameof(SummaryExamsHeader))!;
    public static string SummaryNoExams => Resources.GetString(nameof(SummaryNoExams))!;
    public static string SummaryExamLine => Resources.GetString(nameof(SummaryExamLine))!;
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
    public static string InitUsage => Resources.GetString(nameof(InitUsage))!;
    public static string InitCloning => Resources.GetString(nameof(InitCloning))!;
    public static string InitUpdating => Resources.GetString(nameof(InitUpdating))!;
    public static string InitUpToDate => Resources.GetString(nameof(InitUpToDate))!;
    public static string InitUpdated => Resources.GetString(nameof(InitUpdated))!;
    public static string InitUpdateFailed => Resources.GetString(nameof(InitUpdateFailed))!;
    public static string InitForeignFolder => Resources.GetString(nameof(InitForeignFolder))!;
    public static string InitBookPresent => Resources.GetString(nameof(InitBookPresent))!;
    public static string InitBookDownloading => Resources.GetString(nameof(InitBookDownloading))!;
    public static string InitBookFailed => Resources.GetString(nameof(InitBookFailed))!;
    public static string InitSettingsWritten => Resources.GetString(nameof(InitSettingsWritten))!;
    public static string InitSummary => Resources.GetString(nameof(InitSummary))!;
    public static string InitDone => Resources.GetString(nameof(InitDone))!;
}
