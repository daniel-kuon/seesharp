using System.Resources;
using SeeSharp.Core;
using SeeSharp.Grading;
using SeeSharp.Ide;

namespace SeeSharp.App;

internal static class Strings
{
    private static readonly ResourceManager Resources = new("SeeSharp.App.Strings", typeof(Strings).Assembly);

    public static string ForSkill(SkillState? state) => state switch
    {
        SkillState.Introduced => SkillStateIntroduced,
        SkillState.Practiced => SkillStatePracticed,
        SkillState.Applied => SkillStateApplied,
        _ => SkillStateNone,
    };

    public static string ForOutcome(GradingOutcome outcome) => outcome switch
    {
        GradingOutcome.Passed => OutcomePassed,
        GradingOutcome.Failed => OutcomeFailed,
        GradingOutcome.BuildFailed => OutcomeBuildFailed,
        _ => OutcomeTimedOut,
    };

    public static string ForTest(TestOutcome outcome) => outcome switch
    {
        TestOutcome.Passed => TestPassed,
        TestOutcome.Failed => TestFailed,
        _ => TestSkipped,
    };

    public static string ForIde(IdeKind ide) => ide switch
    {
        IdeKind.Rider => IdeRider,
        IdeKind.VsCode => IdeVsCode,
        _ => IdeVisualStudio,
    };

    public static string OpenIn(IdeKind ide) => ide switch
    {
        IdeKind.Rider => OpenInRider,
        IdeKind.VsCode => OpenInVsCode,
        _ => OpenInVisualStudio,
    };

    public static string Title => Resources.GetString(nameof(Title))!;
    public static string Listening => Resources.GetString(nameof(Listening))!;
    public static string NavHome => Resources.GetString(nameof(NavHome))!;
    public static string NavModules => Resources.GetString(nameof(NavModules))!;
    public static string NavSettings => Resources.GetString(nameof(NavSettings))!;
    public static string NoCourse => Resources.GetString(nameof(NoCourse))!;
    public static string GoToSettings => Resources.GetString(nameof(GoToSettings))!;
    public static string NotFound => Resources.GetString(nameof(NotFound))!;
    public static string DashboardTitle => Resources.GetString(nameof(DashboardTitle))!;
    public static string ModulesDone => Resources.GetString(nameof(ModulesDone))!;
    public static string ExercisesDone => Resources.GetString(nameof(ExercisesDone))!;
    public static string SkillsHeld => Resources.GetString(nameof(SkillsHeld))!;
    public static string NextUp => Resources.GetString(nameof(NextUp))!;
    public static string NothingNext => Resources.GetString(nameof(NothingNext))!;
    public static string Done => Resources.GetString(nameof(Done))!;
    public static string NothingDone => Resources.GetString(nameof(NothingDone))!;
    public static string ModulesTitle => Resources.GetString(nameof(ModulesTitle))!;
    public static string LevelTitle => Resources.GetString(nameof(LevelTitle))!;
    public static string Locked => Resources.GetString(nameof(Locked))!;
    public static string Passed => Resources.GetString(nameof(Passed))!;
    public static string ExerciseProgress => Resources.GetString(nameof(ExerciseProgress))!;
    public static string MissingSkillsLabel => Resources.GetString(nameof(MissingSkillsLabel))!;
    public static string TaughtIn => Resources.GetString(nameof(TaughtIn))!;
    public static string TaughtNowhere => Resources.GetString(nameof(TaughtNowhere))!;
    public static string ValidationErrors => Resources.GetString(nameof(ValidationErrors))!;
    public static string BookLabel => Resources.GetString(nameof(BookLabel))!;
    public static string BookReference => Resources.GetString(nameof(BookReference))!;
    public static string BookMissing => Resources.GetString(nameof(BookMissing))!;
    public static string BookFound => Resources.GetString(nameof(BookFound))!;
    public static string SkillsTaught => Resources.GetString(nameof(SkillsTaught))!;
    public static string SkillStateNone => Resources.GetString(nameof(SkillStateNone))!;
    public static string SkillStateIntroduced => Resources.GetString(nameof(SkillStateIntroduced))!;
    public static string SkillStatePracticed => Resources.GetString(nameof(SkillStatePracticed))!;
    public static string SkillStateApplied => Resources.GetString(nameof(SkillStateApplied))!;
    public static string SummaryRead => Resources.GetString(nameof(SummaryRead))!;
    public static string SummaryReadDone => Resources.GetString(nameof(SummaryReadDone))!;
    public static string Exercises => Resources.GetString(nameof(Exercises))!;
    public static string ItemProgress => Resources.GetString(nameof(ItemProgress))!;
    public static string ExerciseLocked => Resources.GetString(nameof(ExerciseLocked))!;
    public static string BuildsOn => Resources.GetString(nameof(BuildsOn))!;
    public static string Items => Resources.GetString(nameof(Items))!;
    public static string ItemPassed => Resources.GetString(nameof(ItemPassed))!;
    public static string ExercisePassed => Resources.GetString(nameof(ExercisePassed))!;
    public static string Submit => Resources.GetString(nameof(Submit))!;
    public static string AnswerCorrect => Resources.GetString(nameof(AnswerCorrect))!;
    public static string AnswerWrong => Resources.GetString(nameof(AnswerWrong))!;
    public static string Answered => Resources.GetString(nameof(Answered))!;
    public static string AnswerEmpty => Resources.GetString(nameof(AnswerEmpty))!;
    public static string ReviewDone => Resources.GetString(nameof(ReviewDone))!;
    public static string ReviewDoneAlready => Resources.GetString(nameof(ReviewDoneAlready))!;
    public static string NoteLabel => Resources.GetString(nameof(NoteLabel))!;
    public static string OpenFileLabel => Resources.GetString(nameof(OpenFileLabel))!;
    public static string LineLabel => Resources.GetString(nameof(LineLabel))!;
    public static string OpenInRider => Resources.GetString(nameof(OpenInRider))!;
    public static string OpenInVsCode => Resources.GetString(nameof(OpenInVsCode))!;
    public static string OpenInVisualStudio => Resources.GetString(nameof(OpenInVisualStudio))!;
    public static string IdeLaunchError => Resources.GetString(nameof(IdeLaunchError))!;
    public static string IdeNoneFoundHint => Resources.GetString(nameof(IdeNoneFoundHint))!;
    public static string IdeRider => Resources.GetString(nameof(IdeRider))!;
    public static string IdeVsCode => Resources.GetString(nameof(IdeVsCode))!;
    public static string IdeVisualStudio => Resources.GetString(nameof(IdeVisualStudio))!;
    public static string IdesTitle => Resources.GetString(nameof(IdesTitle))!;
    public static string IdeNotFound => Resources.GetString(nameof(IdeNotFound))!;
    public static string RedetectIdes => Resources.GetString(nameof(RedetectIdes))!;
    public static string Copy => Resources.GetString(nameof(Copy))!;
    public static string Copied => Resources.GetString(nameof(Copied))!;
    public static string Grade => Resources.GetString(nameof(Grade))!;
    public static string GradingRunning => Resources.GetString(nameof(GradingRunning))!;
    public static string GradingError => Resources.GetString(nameof(GradingError))!;
    public static string OutcomePassed => Resources.GetString(nameof(OutcomePassed))!;
    public static string OutcomeFailed => Resources.GetString(nameof(OutcomeFailed))!;
    public static string OutcomeBuildFailed => Resources.GetString(nameof(OutcomeBuildFailed))!;
    public static string OutcomeTimedOut => Resources.GetString(nameof(OutcomeTimedOut))!;
    public static string DurationSeconds => Resources.GetString(nameof(DurationSeconds))!;
    public static string TestsHeader => Resources.GetString(nameof(TestsHeader))!;
    public static string TestPassed => Resources.GetString(nameof(TestPassed))!;
    public static string TestFailed => Resources.GetString(nameof(TestFailed))!;
    public static string TestSkipped => Resources.GetString(nameof(TestSkipped))!;
    public static string ColumnTest => Resources.GetString(nameof(ColumnTest))!;
    public static string ColumnOutcome => Resources.GetString(nameof(ColumnOutcome))!;
    public static string ColumnMessage => Resources.GetString(nameof(ColumnMessage))!;
    public static string SettingsTitle => Resources.GetString(nameof(SettingsTitle))!;
    public static string NoCourseFound => Resources.GetString(nameof(NoCourseFound))!;
    public static string CourseNotFoundAt => Resources.GetString(nameof(CourseNotFoundAt))!;
    public static string CourseLoadedFrom => Resources.GetString(nameof(CourseLoadedFrom))!;
    public static string CoursePathLabel => Resources.GetString(nameof(CoursePathLabel))!;
    public static string BookPdfLabel => Resources.GetString(nameof(BookPdfLabel))!;
    public static string SettingsFileHint => Resources.GetString(nameof(SettingsFileHint))!;
    public static string Save => Resources.GetString(nameof(Save))!;
    public static string Reload => Resources.GetString(nameof(Reload))!;
}
