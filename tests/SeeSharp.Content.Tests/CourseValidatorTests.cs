namespace SeeSharp.Content.Tests;

/// <summary>Every validator failure mode, each against a minimal valid course written to a temp directory and broken in one place.</summary>
public sealed class CourseValidatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "seesharp-content-tests-" + Guid.NewGuid().ToString("N"));

    private const string Skills = """
        skills:
          - id: a.one
            category: a
            label: { de: "Eins" }
          - id: a.two
            category: a
            label: { de: "Zwei", en: "Two" }
          - id: b.three
            category: b
            label: { de: "Drei" }
        """;

    private const string ModuleYaml = """
        id: basics
        title: { de: "Grundlagen" }
        """;

    private const string First = """
        id: first
        title: { de: "Erste" }
        reading: { section: "1.1", title: { de: "Einstieg" }, printedPage: 3 }
        teaches: [a.one, a.two]
        requires: []
        items:
          - type: multipleChoice
            id: q1
            prompt: { de: "Was?" }
            options: [{ de: "A" }, { de: "B" }]
            correct: [1]
            explanation: { de: "Weil." }
          - type: open
            id: q2
            prompt: { de: "Warum?" }
        """;

    private const string Second = """
        id: second
        title: { de: "Zweite" }
        teaches: [b.three]
        requires: [a.one, a.two]
        items:
          - type: review
            id: r1
            prompt: { de: "Zeig her." }
        """;

    public CourseValidatorTests()
    {
        Write("skills.yaml", Skills);
        Write("modules/01-basics/module.yaml", ModuleYaml);
        Write("modules/01-basics/summary.de.md", "# Grundlagen");
        WriteExercise("01-basics/01-first", First);
        WriteExercise("01-basics/02-second", Second);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void WriteExercise(string folder, string manifest)
    {
        Write($"modules/{folder}/manifest.yaml", manifest);
        Write($"modules/{folder}/instructions.de.md", "# Aufgabe");
        Write($"modules/{folder}/rubric.md", "# Rubrik");
    }

    private void Delete(string relative)
    {
        var path = Path.Combine(_root, relative);
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        else File.Delete(path);
    }

    private Task<ValidationReport> Validate(bool starterCheck = false) =>
        CourseValidator.ValidateAsync(_root, new ValidationOptions(RunStarterCheck: starterCheck));

    private static Finding AssertFinding(ValidationReport report, FindingCode code, Severity severity = Severity.Error)
    {
        var finding = Assert.Single(report.Findings, f => f.Code == code);
        Assert.Equal(severity, finding.Severity);
        Assert.False(string.IsNullOrWhiteSpace(finding.Message));
        Assert.DoesNotContain(code.ToString(), finding.Message); // resx entry exists
        return finding;
    }

    private static void AssertClean(ValidationReport report) =>
        // b.three is taught last and never required, which is the only expected finding of the baseline course
        Assert.All(report.Findings, f => Assert.Equal((FindingCode.SkillDeadEnd, "b.three"), (f.Code, f.Message.Contains("b.three") ? "b.three" : f.Message)));

    // ----- exams (task 8) ----------------------------------------------------------------------

    private const string Exam = """
        id: probe
        title: { de: "Probeprüfung" }
        kind: practice
        coverage: { levels: [1] }
        timeLimitMinutes: 45
        maxItems: 10
        """;

    [Fact]
    public async Task Exam_definition_is_loaded_and_summarised()
    {
        Write("exams/probe.yaml", Exam);
        Write("exams/ernst.yaml", Exam.Replace("probe", "ernst").Replace("practice", "real").Replace("coverage: { levels: [1] }", "skills: [a.one]"));

        var report = await Validate();

        AssertClean(report);
        Assert.Equal(["ernst", "probe"], report.Course.Exams.Select(e => e.Id)); // file order
        Assert.True(report.Course.Exams[0].IsReal);
        Assert.Equal(10, report.Course.Exams[1].MaxItems);
        var probe = report.Exams.Single(e => e.Id == "probe");
        // level 1: first (q1, q2) and second (r1) teach a.one, a.two, b.three; 3 non-review items
        Assert.Equal(("practice", 3, 2, 2, 45), (probe.Kind, probe.Skills, probe.Exercises, probe.Items, probe.TimeLimitMinutes));
        Assert.Equal([1], probe.Levels);
        var ernst = report.Exams.Single(e => e.Id == "ernst");
        Assert.Equal((1, 1), (ernst.Skills, ernst.Exercises)); // only exercises teaching a.one
        Assert.Equal(["first"], ExamScope.CoveredExercises(report.Course, report.Course.Exams[0]).Select(e => e.Manifest.Id));
    }

    [Fact]
    public async Task Exam_with_unknown_level_or_skill_or_kind_is_an_error()
    {
        Write("exams/probe.yaml", Exam.Replace("levels: [1]", "levels: [1, 7]").Replace("practice", "mock") + "\nskills: [a.one, z.unknown]\n");

        var report = await Validate();

        Assert.Contains("7", AssertFinding(report, FindingCode.ExamLevelUnknown).Message);
        Assert.Contains("z.unknown", AssertFinding(report, FindingCode.ExamSkillUnknown).Message);
        Assert.Contains("mock", AssertFinding(report, FindingCode.ExamKindInvalid).Message);
        Assert.Equal("exams/probe", report.Findings.First(f => f.Code == FindingCode.ExamLevelUnknown).Location);
    }

    [Fact]
    public async Task Exam_without_coverage_title_or_time_limit_is_an_error()
    {
        Write("exams/leer.yaml", "id: leer\nkind: real\ntimeLimitMinutes: 0\n");
        Write("exams/kaputt.yaml", "id: [\n");

        var report = await Validate();

        AssertFinding(report, FindingCode.ExamCoverageEmpty);
        AssertFinding(report, FindingCode.ExamTitleMissingDefaultLocale);
        AssertFinding(report, FindingCode.ExamTimeLimitInvalid);
        Assert.Equal("exams/kaputt.yaml", AssertFinding(report, FindingCode.ExamManifestInvalid).Location);
        Assert.Equal(["leer"], report.Course.Exams.Select(e => e.Id));
    }

    // ----- baseline ----------------------------------------------------------------------------

    [Fact]
    public async Task Valid_course_has_no_errors_and_a_summary()
    {
        var report = await Validate();

        AssertClean(report);
        Assert.False(report.HasErrors);
        var module = Assert.Single(report.Summary.Modules);
        Assert.Equal((1, "basics", 2, 3, 2, 3), (module.Order, module.Id, module.Exercises, module.SkillsTaught, module.SkillsRequired, module.SkillsNew));
        Assert.Equal(3, report.Summary.SkillsInCatalog);
        Assert.Equal(3, report.Summary.SkillsTaught);
        Assert.Empty(report.Summary.Dangling);
        Assert.Equal(["b.three"], report.Summary.DeadEnds);
    }

    [Fact]
    public async Task Loader_sorts_by_folder_prefix_and_resolves_item_types()
    {
        var report = await Validate();

        var module = Assert.Single(report.Course.Modules);
        Assert.Equal(["first", "second"], module.Exercises.Select(e => e.Manifest.Id));
        var first = module.Exercises[0].Manifest;
        Assert.Equal(3 + Reading.PrintedToPhysicalOffset, first.Reading!.PhysicalPage);
        Assert.IsType<MultipleChoiceItem>(first.Items[0]);
        Assert.Equal([1], ((MultipleChoiceItem)first.Items[0]).Correct);
        Assert.IsType<OpenItem>(first.Items[1]);
        Assert.IsType<ReviewItem>(module.Exercises[1].Manifest.Items[0]);
    }

    [Fact]
    public async Task Alias_in_manifest_resolves_to_the_canonical_skill()
    {
        Write("skills.yaml", Skills + "\n    aliases: [b.old]\n");
        Write("modules/01-basics/02-second/manifest.yaml", Second.Replace("teaches: [b.three]", "teaches: [b.old]"));

        var report = await Validate();

        AssertClean(report);
    }

    // ----- skills.yaml -------------------------------------------------------------------------

    [Fact]
    public async Task Missing_skills_file()
    {
        Delete("skills.yaml");
        var report = await Validate();
        AssertFinding(report, FindingCode.SkillsFileMissing);
        Assert.DoesNotContain(report.Findings, f => f.Code == FindingCode.SkillUnknown); // no noise without a catalog
    }

    public static TheoryData<string, FindingCode> BrokenSkills => new()
    {
        { "skills: [", FindingCode.SkillsFileInvalid },
        { Skills.Replace("category: b", "colour: b"), FindingCode.SkillsFileInvalid },
        { Skills.Replace("id: b.three", "id: B.Three"), FindingCode.SkillIdInvalid },
        { Skills.Replace("id: b.three", "id: three"), FindingCode.SkillIdInvalid },
        { Skills.Replace("id: b.three", "id: a.one"), FindingCode.SkillIdDuplicate },
        { Skills.Replace("category: b", "category: \"\""), FindingCode.SkillCategoryMissing },
        { Skills.Replace("label: { de: \"Drei\" }", "label: { en: \"Three\" }"), FindingCode.SkillLabelMissingDefaultLocale },
        { Skills + "\n    supersedes: b.gone\n", FindingCode.SkillSupersedesUnknown },
        { Skills + "\n    aliases: [a.one]\n", FindingCode.SkillAliasConflict },
    };

    [Theory, MemberData(nameof(BrokenSkills))]
    public async Task Broken_skills_file(string yaml, FindingCode expected)
    {
        Write("skills.yaml", yaml);
        AssertFinding(await Validate(), expected);
    }

    // ----- modules -----------------------------------------------------------------------------

    [Fact]
    public async Task Missing_modules_folder()
    {
        Delete("modules");
        AssertFinding(await Validate(), FindingCode.ModulesFolderMissing);
    }

    [Fact]
    public async Task Misnamed_module_folder_is_reported_and_skipped()
    {
        Write("modules/Basics/module.yaml", ModuleYaml);
        var report = await Validate();
        AssertFinding(report, FindingCode.ModuleFolderNameInvalid);
        Assert.Single(report.Course.Modules);
    }

    [Fact]
    public async Task Missing_module_manifest()
    {
        Delete("modules/01-basics/module.yaml");
        AssertFinding(await Validate(), FindingCode.ModuleManifestMissing);
    }

    [Fact]
    public async Task Missing_summary()
    {
        Delete("modules/01-basics/summary.de.md");
        AssertFinding(await Validate(), FindingCode.ModuleSummaryMissing);
    }

    [Fact]
    public async Task Module_without_exercises_warns()
    {
        Write("modules/02-empty/module.yaml", ModuleYaml.Replace("basics", "empty"));
        Write("modules/02-empty/summary.de.md", "#");
        AssertFinding(await Validate(), FindingCode.ModuleWithoutExercises, Severity.Warning);
    }

    [Fact]
    public async Task Duplicate_module_id()
    {
        Write("modules/02-again/module.yaml", ModuleYaml);
        Write("modules/02-again/summary.de.md", "#");
        AssertFinding(await Validate(), FindingCode.ModuleIdDuplicate);
    }

    public static TheoryData<string, FindingCode> BrokenModule => new()
    {
        { "id: [", FindingCode.ModuleManifestInvalid },
        { ModuleYaml.Replace("id: basics", "id: Basics"), FindingCode.ModuleIdInvalid },
        { ModuleYaml.Replace("{ de: \"Grundlagen\" }", "{ en: \"Basics\" }"), FindingCode.ModuleTitleMissingDefaultLocale },
        { ModuleYaml + "\nreading: { section: \"1\", title: { de: \"x\" }, printedPage: 0 }\n", FindingCode.ReadingInvalid },
    };

    [Theory, MemberData(nameof(BrokenModule))]
    public async Task Broken_module_manifest(string yaml, FindingCode expected)
    {
        Write("modules/01-basics/module.yaml", yaml);
        AssertFinding(await Validate(), expected);
    }

    // ----- exercises ---------------------------------------------------------------------------

    [Fact]
    public async Task Misnamed_exercise_folder_is_reported_and_skipped()
    {
        WriteExercise("01-basics/third", Second.Replace("id: second", "id: third"));
        var report = await Validate();
        AssertFinding(report, FindingCode.ExerciseFolderNameInvalid);
        Assert.Equal(2, report.Course.Modules[0].Exercises.Count);
    }

    [Fact]
    public async Task Missing_exercise_manifest()
    {
        Delete("modules/01-basics/02-second/manifest.yaml");
        AssertFinding(await Validate(), FindingCode.ExerciseManifestMissing);
    }

    [Fact]
    public async Task Missing_instructions()
    {
        Delete("modules/01-basics/02-second/instructions.de.md");
        AssertFinding(await Validate(), FindingCode.InstructionsMissing);
    }

    // A course without any translation gets no translation warnings: Valid_course_has_no_errors_and_a_summary covers that.

    [Fact]
    public async Task One_english_file_makes_every_other_missing_translation_a_warning()
    {
        Write("modules/01-basics/01-first/instructions.en.md", "# Task");
        var report = await Validate();

        Assert.False(report.HasErrors);
        var warnings = report.Findings.Where(f => f.Code == FindingCode.TranslationMissing).ToList();
        Assert.All(warnings, f => Assert.Equal(Severity.Warning, f.Severity));
        Assert.Equal(
        [
            ("modules/01-basics", "summary.en.md"),
            ("modules/01-basics", "title"),
            ("modules/01-basics/01-first", "title"),
            ("modules/01-basics/02-second", "instructions.en.md"),
            ("modules/01-basics/02-second", "title"),
        ], warnings.Select(f => (f.Location, new[] { "summary.en.md", "instructions.en.md", "title" }.First(f.Message.Contains))));
    }

    [Fact]
    public async Task One_english_title_counts_as_a_translation_too()
    {
        Write("modules/01-basics/module.yaml", ModuleYaml.Replace("{ de: \"Grundlagen\" }", "{ de: \"Grundlagen\", en: \"Basics\" }"));
        var report = await Validate();

        Assert.False(report.HasErrors);
        Assert.Equal(5, report.Findings.Count(f => f.Code == FindingCode.TranslationMissing));
        Assert.DoesNotContain(report.Findings, f => f.Code == FindingCode.TranslationMissing && f.Location == "modules/01-basics" && f.Message.Contains("title"));
    }

    [Fact]
    public async Task Missing_rubric_warns()
    {
        Delete("modules/01-basics/02-second/rubric.md");
        AssertFinding(await Validate(), FindingCode.RubricMissing, Severity.Warning);
    }

    [Fact]
    public async Task Duplicate_exercise_id_across_modules()
    {
        Write("modules/02-more/module.yaml", ModuleYaml.Replace("basics", "more"));
        Write("modules/02-more/summary.de.md", "#");
        WriteExercise("02-more/01-copy", Second);
        AssertFinding(await Validate(), FindingCode.ExerciseIdDuplicate);
    }

    public static TheoryData<string, FindingCode> BrokenExercise => new()
    {
        { "id: [", FindingCode.ExerciseManifestInvalid },
        { Second.Replace("type: review", "type: essay"), FindingCode.ExerciseManifestInvalid },
        { Second.Replace("prompt:", "promt:"), FindingCode.ExerciseManifestInvalid },
        { Second.Replace("id: second", "id: Second!"), FindingCode.ExerciseIdInvalid },
        { Second.Replace("title: { de: \"Zweite\" }", "title: { en: \"Second\" }"), FindingCode.ExerciseTitleMissingDefaultLocale },
        { Second + "\nreading: { section: \"\", title: { de: \"x\" }, printedPage: 3 }\n", FindingCode.ReadingInvalid },
        { Second.Replace("teaches: [b.three]", "teaches: [b.three, b.nope]"), FindingCode.SkillUnknown },
        { Second.Replace("requires: [a.one, a.two]", "requires: [a.one, a.two, b.three]"), FindingCode.SkillRequiredAndTaught },
        { Second.Replace("requires: [a.one, a.two]", "requires: [a.one, a.two, b.extra]"), FindingCode.SkillUnknown },
    };

    [Theory, MemberData(nameof(BrokenExercise))]
    public async Task Broken_exercise_manifest(string yaml, FindingCode expected)
    {
        Write("modules/01-basics/02-second/manifest.yaml", yaml);
        AssertFinding(await Validate(), expected);
    }

    [Fact]
    public async Task Teaches_empty_is_a_warning()
    {
        Write("modules/01-basics/02-second/manifest.yaml", Second.Replace("teaches: [b.three]", "teaches: []"));
        AssertFinding(await Validate(), FindingCode.TeachesEmpty, Severity.Warning);
    }

    // ----- items -------------------------------------------------------------------------------

    public static TheoryData<string, FindingCode> BrokenItems => new()
    {
        { First.Replace("items:", "items: []").Split("items: []")[0] + "items: []\n", FindingCode.ItemsEmpty },
        { First.Replace("id: q2", "id: Q 2"), FindingCode.ItemIdInvalid },
        { First.Replace("id: q2", "id: q1"), FindingCode.ItemIdDuplicate },
        { First.Replace("prompt: { de: \"Warum?\" }", "prompt: { en: \"Why?\" }"), FindingCode.ItemTextMissingDefaultLocale },
        { First.Replace("options: [{ de: \"A\" }, { de: \"B\" }]", "options: [{ de: \"A\" }, { en: \"B\" }]"), FindingCode.ItemTextMissingDefaultLocale },
        { First.Replace("explanation: { de: \"Weil.\" }", "explanation: { en: \"Because.\" }"), FindingCode.ItemTextMissingDefaultLocale },
        { First.Replace("options: [{ de: \"A\" }, { de: \"B\" }]", "options: [{ de: \"A\" }]").Replace("correct: [1]", "correct: [0]"), FindingCode.ChoiceOptionsTooFew },
        { First.Replace("correct: [1]", "correct: [2]"), FindingCode.ChoiceCorrectInvalid },
        { First.Replace("correct: [1]", "correct: []"), FindingCode.ChoiceCorrectInvalid },
    };

    [Theory, MemberData(nameof(BrokenItems))]
    public async Task Broken_items(string yaml, FindingCode expected)
    {
        Write("modules/01-basics/01-first/manifest.yaml", yaml);
        AssertFinding(await Validate(), expected);
    }

    [Fact]
    public async Task Review_item_open_file_must_exist()
    {
        Write("modules/01-basics/02-second/manifest.yaml", Second + "\n    openFile: Nope.cs\n");
        AssertFinding(await Validate(), FindingCode.CodingOpenFileMissing);
    }

    // ----- coding items and the starter check --------------------------------------------------

    private const string Coding = """
        id: coding
        title: { de: "Rechner" }
        teaches: [b.three]
        requires: [a.one, a.two]
        items:
          - type: coding
            id: code
            openFile: "Rechner/Rechner.cs"
            openLine: 3
            testProject: "Rechner Tests/Rechner.Tests.csproj"
        """;

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "starter-check");

    private void WriteCodingExercise(string starterFixture)
    {
        WriteExercise("01-basics/02-second", Coding);
        CopyTree(Path.Combine(Fixtures, starterFixture), Path.Combine(_root, "modules/01-basics/02-second/starter"));
        CopyTree(Path.Combine(Fixtures, "tests"), Path.Combine(_root, "modules/01-basics/02-second/tests"));
    }

    private static void CopyTree(string from, string to)
    {
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    [Fact]
    public async Task Coding_exercise_with_starter_and_tests_is_clean()
    {
        WriteCodingExercise("starter-open");
        var report = await Validate();
        AssertClean(report);
        Assert.Equal([3], report.Course.Modules[0].Exercises[1].Manifest.Items.OfType<CodingItem>().Select(c => c.OpenLine));
    }

    [Fact]
    public async Task Coding_starter_missing()
    {
        WriteCodingExercise("starter-open");
        Delete("modules/01-basics/02-second/starter");
        AssertFinding(await Validate(), FindingCode.CodingStarterMissing);
    }

    [Fact]
    public async Task Coding_tests_missing()
    {
        WriteCodingExercise("starter-open");
        Delete("modules/01-basics/02-second/tests");
        AssertFinding(await Validate(), FindingCode.CodingTestsMissing);
    }

    [Fact]
    public async Task Coding_tests_must_hold_exactly_one_project()
    {
        WriteCodingExercise("starter-open");
        Write("modules/01-basics/02-second/tests/Extra/Extra.csproj", "<Project />");
        AssertFinding(await Validate(), FindingCode.CodingTestsNotSingleProject);
    }

    [Fact]
    public async Task Coding_open_file_missing_or_escaping_starter()
    {
        WriteCodingExercise("starter-open");
        Write("modules/01-basics/02-second/manifest.yaml", Coding.Replace("Rechner/Rechner.cs", "../tests/Rechner Tests/RechnerTests.cs"));
        AssertFinding(await Validate(), FindingCode.CodingOpenFileMissing);
    }

    [Fact]
    public async Task Coding_test_project_missing()
    {
        WriteCodingExercise("starter-open");
        Write("modules/01-basics/02-second/manifest.yaml", Coding.Replace("Rechner.Tests.csproj", "Other.csproj"));
        AssertFinding(await Validate(), FindingCode.CodingTestProjectMissing);
    }

    [Fact]
    public async Task Starter_that_already_passes_the_tests_is_an_error()
    {
        WriteCodingExercise("starter-solved");
        AssertFinding(await Validate(starterCheck: true), FindingCode.StarterPassesTests);
    }

    [Fact]
    public async Task Starter_that_fails_the_tests_is_fine()
    {
        WriteCodingExercise("starter-open");
        AssertClean(await Validate(starterCheck: true));
    }

    [Fact]
    public async Task Starter_that_does_not_compile_warns()
    {
        WriteCodingExercise("starter-broken");
        AssertFinding(await Validate(starterCheck: true), FindingCode.StarterBuildFailed, Severity.Warning);
    }

    // ----- skill graph -------------------------------------------------------------------------

    [Fact]
    public async Task Dangling_skill_required_but_never_taught()
    {
        Write("modules/01-basics/02-second/manifest.yaml", Second.Replace("teaches: [b.three]", "teaches: []"));
        Write("modules/01-basics/01-first/manifest.yaml", First.Replace("requires: []", "requires: [b.three]"));

        var report = await Validate();

        AssertFinding(report, FindingCode.SkillDangling);
        Assert.Equal(["b.three"], report.Summary.Dangling);
    }

    [Fact]
    public async Task Forward_reference_to_a_later_exercise()
    {
        Write("modules/01-basics/01-first/manifest.yaml", First.Replace("requires: []", "requires: [b.three]"));

        var report = await Validate();

        var finding = AssertFinding(report, FindingCode.SkillForwardReference);
        Assert.Contains("second", finding.Message);
        Assert.DoesNotContain(report.Findings, f => f.Code == FindingCode.SkillDangling);
    }

    [Fact]
    public async Task Cycle_in_the_skill_graph()
    {
        // first teaches a.two requiring a.one; third teaches a.one requiring a.two (a.one is also taught earlier, so no forward reference)
        Write("modules/01-basics/01-first/manifest.yaml", First.Replace("teaches: [a.one, a.two]", "teaches: [a.one]"));
        Write("modules/01-basics/02-second/manifest.yaml", Second.Replace("teaches: [b.three]", "teaches: [a.two]").Replace("requires: [a.one, a.two]", "requires: [a.one]"));
        WriteExercise("01-basics/03-third", Second.Replace("id: second", "id: third").Replace("teaches: [b.three]", "teaches: [a.one, b.three]").Replace("requires: [a.one, a.two]", "requires: [a.two]"));

        var report = await Validate();

        var finding = AssertFinding(report, FindingCode.SkillCycle);
        Assert.Contains("a.one -> a.two -> a.one", finding.Message);
    }

    [Fact]
    public async Task Skill_taught_by_more_than_three_exercises_warns()
    {
        for (var i = 3; i <= 5; i++)
            WriteExercise($"01-basics/0{i}-more{i}", Second.Replace("id: second", $"id: more{i}"));

        var finding = AssertFinding(await Validate(), FindingCode.SkillTaughtTooOften, Severity.Warning);
        Assert.Contains("b.three", finding.Message);
    }

    [Fact]
    public async Task Skill_taught_but_never_required_warns()
    {
        var finding = AssertFinding(await Validate(), FindingCode.SkillDeadEnd, Severity.Warning);
        Assert.Contains("b.three", finding.Message);
    }
}
