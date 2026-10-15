namespace SeeSharp.Content;

public enum Severity
{
    Warning,
    Error,
}

/// <summary>One code per failure mode; the message text lives in Strings.resx under the same name.</summary>
public enum FindingCode
{
    SkillsFileMissing,
    SkillsFileInvalid,
    SkillIdInvalid,
    SkillIdDuplicate,
    SkillCategoryMissing,
    SkillLabelMissingDefaultLocale,
    SkillSupersedesUnknown,
    SkillAliasConflict,

    ModulesFolderMissing,
    ModuleFolderNameInvalid,
    ModuleManifestMissing,
    ModuleManifestInvalid,
    ModuleIdInvalid,
    ModuleIdDuplicate,
    ModuleTitleMissingDefaultLocale,
    ModuleSummaryMissing,
    ModuleWithoutExercises,

    ExerciseFolderNameInvalid,
    ExerciseManifestMissing,
    ExerciseManifestInvalid,
    ExerciseIdInvalid,
    ExerciseIdDuplicate,
    ExerciseTitleMissingDefaultLocale,
    InstructionsMissing,
    RubricMissing,
    ReadingInvalid,

    ItemsEmpty,
    ItemIdInvalid,
    ItemIdDuplicate,
    ItemTextMissingDefaultLocale,
    ChoiceOptionsTooFew,
    ChoiceCorrectInvalid,
    CodingStarterMissing,
    CodingTestsMissing,
    CodingTestsNotSingleProject,
    CodingOpenFileMissing,
    CodingTestProjectMissing,
    StarterPassesTests,
    StarterBuildFailed,
    StarterTimedOut,

    SkillUnknown,
    TeachesEmpty,
    SkillRequiredAndTaught,
    SkillDangling,
    SkillForwardReference,
    SkillCycle,
    SkillTaughtTooOften,
    SkillDeadEnd,
}

/// <param name="Location">Path relative to the course root, optionally with <c>#id</c>.</param>
public sealed record Finding(Severity Severity, FindingCode Code, string Location, string Message);
