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

    /// <summary>Language names stay in their own language, so the switcher reads the same whichever is active.</summary>
    public static string LanguageName(string locale) => locale == "en" ? LanguageEn : LanguageDe;

    public static string LanguageLabel => Resources.GetString(nameof(LanguageLabel))!;
    public static string LanguageDe => Resources.GetString(nameof(LanguageDe))!;
    public static string LanguageEn => Resources.GetString(nameof(LanguageEn))!;
    public static string TranslationFallback => Resources.GetString(nameof(TranslationFallback))!;
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
    public static string NavAdmin => Resources.GetString(nameof(NavAdmin))!;
    public static string NavAdminCourse => Resources.GetString(nameof(NavAdminCourse))!;
    public static string NavAdminSkills => Resources.GetString(nameof(NavAdminSkills))!;
    public static string NavAdminGit => Resources.GetString(nameof(NavAdminGit))!;
    public static string NavAdminReview => Resources.GetString(nameof(NavAdminReview))!;
    public static string AdminTreeTitle => Resources.GetString(nameof(AdminTreeTitle))!;
    public static string ReorderHint => Resources.GetString(nameof(ReorderHint))!;
    public static string MoveUp => Resources.GetString(nameof(MoveUp))!;
    public static string MoveDown => Resources.GetString(nameof(MoveDown))!;
    public static string Delete => Resources.GetString(nameof(Delete))!;
    public static string ConfirmDelete => Resources.GetString(nameof(ConfirmDelete))!;
    public static string NewModule => Resources.GetString(nameof(NewModule))!;
    public static string NewExercise => Resources.GetString(nameof(NewExercise))!;
    public static string SlugLabel => Resources.GetString(nameof(SlugLabel))!;
    public static string IdLabel => Resources.GetString(nameof(IdLabel))!;
    public static string TitleLabel => Resources.GetString(nameof(TitleLabel))!;
    public static string ModuleLabel => Resources.GetString(nameof(ModuleLabel))!;
    public static string Create => Resources.GetString(nameof(Create))!;
    public static string CreateError => Resources.GetString(nameof(CreateError))!;
    public static string Teaches => Resources.GetString(nameof(Teaches))!;
    public static string Requires => Resources.GetString(nameof(Requires))!;
    public static string AddSkill => Resources.GetString(nameof(AddSkill))!;
    public static string SkillPickerPlaceholder => Resources.GetString(nameof(SkillPickerPlaceholder))!;
    public static string SkillUnknown => Resources.GetString(nameof(SkillUnknown))!;
    public static string Remove => Resources.GetString(nameof(Remove))!;
    public static string AdminExerciseTitle => Resources.GetString(nameof(AdminExerciseTitle))!;
    public static string Folder => Resources.GetString(nameof(Folder))!;
    public static string ReadingLabel => Resources.GetString(nameof(ReadingLabel))!;
    public static string ReadingSection => Resources.GetString(nameof(ReadingSection))!;
    public static string ReadingTitle => Resources.GetString(nameof(ReadingTitle))!;
    public static string ReadingPage => Resources.GetString(nameof(ReadingPage))!;
    public static string AddReading => Resources.GetString(nameof(AddReading))!;
    public static string RemoveReading => Resources.GetString(nameof(RemoveReading))!;
    public static string InstructionsLabel => Resources.GetString(nameof(InstructionsLabel))!;
    public static string RubricLabel => Resources.GetString(nameof(RubricLabel))!;
    public static string AddItem => Resources.GetString(nameof(AddItem))!;
    public static string PromptLabel => Resources.GetString(nameof(PromptLabel))!;
    public static string OptionsLabel => Resources.GetString(nameof(OptionsLabel))!;
    public static string AddOption => Resources.GetString(nameof(AddOption))!;
    public static string ExplanationLabel => Resources.GetString(nameof(ExplanationLabel))!;
    public static string OpenFileField => Resources.GetString(nameof(OpenFileField))!;
    public static string OpenLineField => Resources.GetString(nameof(OpenLineField))!;
    public static string TestProjectField => Resources.GetString(nameof(TestProjectField))!;
    public static string RequiredTestsField => Resources.GetString(nameof(RequiredTestsField))!;
    public static string Saved => Resources.GetString(nameof(Saved))!;
    public static string FindingsForExercise => Resources.GetString(nameof(FindingsForExercise))!;
    public static string NoFindings => Resources.GetString(nameof(NoFindings))!;
    public static string AdminSkillsTitle => Resources.GetString(nameof(AdminSkillsTitle))!;
    public static string EditSkillsLink => Resources.GetString(nameof(EditSkillsLink))!;
    public static string SkillsCoverage => Resources.GetString(nameof(SkillsCoverage))!;
    public static string ColumnLevel => Resources.GetString(nameof(ColumnLevel))!;
    public static string ColumnModule => Resources.GetString(nameof(ColumnModule))!;
    public static string ColumnExercises => Resources.GetString(nameof(ColumnExercises))!;
    public static string ColumnTaught => Resources.GetString(nameof(ColumnTaught))!;
    public static string ColumnNew => Resources.GetString(nameof(ColumnNew))!;
    public static string ColumnRequired => Resources.GetString(nameof(ColumnRequired))!;
    public static string DanglingLabel => Resources.GetString(nameof(DanglingLabel))!;
    public static string DeadEndsLabel => Resources.GetString(nameof(DeadEndsLabel))!;
    public static string NotTaughtLabel => Resources.GetString(nameof(NotTaughtLabel))!;
    public static string None => Resources.GetString(nameof(None))!;
    public static string SkillFindings => Resources.GetString(nameof(SkillFindings))!;
    public static string AdminSkillsEditTitle => Resources.GetString(nameof(AdminSkillsEditTitle))!;
    public static string SkillsEditHint => Resources.GetString(nameof(SkillsEditHint))!;
    public static string SaveError => Resources.GetString(nameof(SaveError))!;
    public static string AdminGitTitle => Resources.GetString(nameof(AdminGitTitle))!;
    public static string GitNotRepo => Resources.GetString(nameof(GitNotRepo))!;
    public static string GitBranch => Resources.GetString(nameof(GitBranch))!;
    public static string GitRemote => Resources.GetString(nameof(GitRemote))!;
    public static string GitNoRemote => Resources.GetString(nameof(GitNoRemote))!;
    public static string GitNoUpstream => Resources.GetString(nameof(GitNoUpstream))!;
    public static string GitChanges => Resources.GetString(nameof(GitChanges))!;
    public static string GitClean => Resources.GetString(nameof(GitClean))!;
    public static string GitDiff => Resources.GetString(nameof(GitDiff))!;
    public static string Validate => Resources.GetString(nameof(Validate))!;
    public static string Working => Resources.GetString(nameof(Working))!;
    public static string ValidationResult => Resources.GetString(nameof(ValidationResult))!;
    public static string CommitMessage => Resources.GetString(nameof(CommitMessage))!;
    public static string CommitButton => Resources.GetString(nameof(CommitButton))!;
    public static string CommitBlocked => Resources.GetString(nameof(CommitBlocked))!;
    public static string Committed => Resources.GetString(nameof(Committed))!;
    public static string Push => Resources.GetString(nameof(Push))!;
    public static string Pushed => Resources.GetString(nameof(Pushed))!;
    public static string Pull => Resources.GetString(nameof(Pull))!;
    public static string PullResult => Resources.GetString(nameof(PullResult))!;
    public static string GitUpToDate => Resources.GetString(nameof(GitUpToDate))!;
    public static string GitFastForward => Resources.GetString(nameof(GitFastForward))!;
    public static string GitMerged => Resources.GetString(nameof(GitMerged))!;
    public static string PullConflict => Resources.GetString(nameof(PullConflict))!;
    public static string GitError => Resources.GetString(nameof(GitError))!;
    public static string AdminReviewTitle => Resources.GetString(nameof(AdminReviewTitle))!;
    public static string ImportBundle => Resources.GetString(nameof(ImportBundle))!;
    public static string BundleLoaded => Resources.GetString(nameof(BundleLoaded))!;
    public static string BundleError => Resources.GetString(nameof(BundleError))!;
    public static string UnknownExercises => Resources.GetString(nameof(UnknownExercises))!;
    public static string OpenAnswers => Resources.GetString(nameof(OpenAnswers))!;
    public static string NoAnswers => Resources.GetString(nameof(NoAnswers))!;
    public static string RubricMissingNote => Resources.GetString(nameof(RubricMissingNote))!;
    public static string SkillMatrix => Resources.GetString(nameof(SkillMatrix))!;
    public static string ColumnSkill => Resources.GetString(nameof(ColumnSkill))!;
    public static string ColumnState => Resources.GetString(nameof(ColumnState))!;
    public static string ColumnLastExercised => Resources.GetString(nameof(ColumnLastExercised))!;
    public static string ColumnExercise => Resources.GetString(nameof(ColumnExercise))!;
}
