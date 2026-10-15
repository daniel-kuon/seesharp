namespace SeeSharp.Content.Tests;

/// <summary>The admin editor's file operations against a minimal course in a temp directory.</summary>
public sealed class CourseEditorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "seesharp-editor-tests-" + Guid.NewGuid().ToString("N"));

    public CourseEditorTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "skills.yaml"), """
            skills:
              - id: a.one
                category: a
                label: { de: "Eins" }
              - id: a.two
                category: a
                label: { de: "Zwei" }
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Created_module_and_exercise_validate_including_the_starter_check()
    {
        var module = CourseEditor.CreateModule(_root, "grundlagen", "grundlagen", "Grundlagen");
        var exercise = CourseEditor.CreateExercise(module, "hallo-welt", "hallo-welt", "Hallo Welt", ["a.one"], []);

        Assert.Equal("00-grundlagen", Path.GetFileName(module));
        Assert.Equal("01-hallo-welt", Path.GetFileName(exercise));
        Assert.Equal("HalloWelt", CourseEditor.ProjectName("hallo-welt"));
        Assert.True(File.Exists(Path.Combine(exercise, "starter", "HalloWelt", "HalloWelt.cs")));
        Assert.True(File.Exists(Path.Combine(exercise, "tests", "HalloWelt.Tests", "HalloWelt.Tests.csproj")));

        var report = await CourseValidator.ValidateAsync(_root, new ValidationOptions(RunStarterCheck: true, StarterTimeout: TimeSpan.FromMinutes(5)));

        Assert.False(report.HasErrors, string.Join("\n", report.Findings));
        Assert.DoesNotContain(report.Findings, f => f.Code == FindingCode.StarterBuildFailed); // the template compiles and merely fails its test
        var loaded = Assert.Single(Assert.Single(report.Course.Modules).Exercises);
        Assert.Equal("hallo-welt", loaded.Manifest.Id);
        Assert.Equal(["a.one"], loaded.Manifest.Teaches);
        Assert.Equal("HalloWelt/HalloWelt.cs", Assert.IsType<CodingItem>(loaded.Manifest.Items[0]).OpenFile);
    }

    [Fact]
    public void Second_exercise_gets_the_next_prefix_and_a_bad_slug_is_refused()
    {
        var module = CourseEditor.CreateModule(_root, "grundlagen", "grundlagen", "Grundlagen");
        CourseEditor.CreateExercise(module, "eins", "eins", "Eins", [], []);
        var second = CourseEditor.CreateExercise(module, "zwei", "zwei", "Zwei", [], []);

        Assert.Equal("02-zwei", Path.GetFileName(second));
        Assert.Throws<ArgumentException>(() => CourseEditor.CreateExercise(module, "Drei Vier", "drei", "Drei", [], []));
        Assert.Equal("01-zwei", Path.GetFileName(CourseEditor.CreateModule(_root, "zwei", "zwei-modul", "Zwei")));
    }

    [Fact]
    public void Swap_exchanges_the_folder_prefixes()
    {
        var module = CourseEditor.CreateModule(_root, "grundlagen", "grundlagen", "Grundlagen");
        var a = CourseEditor.CreateExercise(module, "eins", "eins", "Eins", [], []);
        var b = CourseEditor.CreateExercise(module, "zwei", "zwei", "Zwei", [], []);

        CourseEditor.Swap(a, b);

        Assert.Equal(["01-zwei", "02-eins"], Directory.GetDirectories(module).Select(Path.GetFileName).Order());
        Assert.Equal("eins", CourseLoader.Deserialize<ExerciseManifest>(File.ReadAllText(Path.Combine(module, "02-eins", "manifest.yaml"))).Id);
    }

    [Fact]
    public void Manifest_round_trips_through_the_editor_serializer()
    {
        var manifest = new ExerciseManifest
        {
            Id = "quiz",
            Title = { ["de"] = "Quiz", ["en"] = "Quiz" },
            Reading = new Reading { Section = "1.2", Title = { ["de"] = "Start" }, PrintedPage = 5 },
            Teaches = ["a.one"],
            Items =
            [
                new MultipleChoiceItem { Id = "q1", Prompt = { ["de"] = "Was, \"genau\"?" }, Options = [new() { ["de"] = "A" }, new() { ["de"] = "B: ja" }], Correct = [1] },
                new OpenItem { Id = "q2", Prompt = { ["de"] = "Warum?" } },
                new CodingItem { Id = "c", OpenFile = "X/X.cs", TestProject = "X.Tests/X.Tests.csproj", RequiredTests = ["XTests.A"] },
                new ReviewItem { Id = "r", Prompt = { ["de"] = "Zeig her." } },
            ],
        };

        var yaml = CourseEditor.Serialize(manifest);
        var back = CourseLoader.Deserialize<ExerciseManifest>(yaml);

        Assert.Contains("- type: multipleChoice\n  id: q1\n", yaml.ReplaceLineEndings("\n")); // discriminator first, so the file reads like a hand-written one
        Assert.DoesNotContain("physicalPage", yaml);
        Assert.DoesNotContain("explanation", yaml); // null fields are omitted
        Assert.Equal("quiz", back.Id);
        Assert.Equal(5, back.Reading!.PrintedPage);
        Assert.Equal(["multipleChoice", "open", "coding", "review"], back.Items.Select(i => i.Type));
        var choice = Assert.IsType<MultipleChoiceItem>(back.Items[0]);
        Assert.Equal("Was, \"genau\"?", choice.Prompt["de"]);
        Assert.Equal([1], choice.Correct);
        Assert.Null(Assert.IsType<CodingItem>(back.Items[2]).OpenLine);
        Assert.Equal(["XTests.A"], Assert.IsType<CodingItem>(back.Items[2]).RequiredTests);
    }

    [Fact]
    public void Skills_file_is_only_written_when_it_parses()
    {
        var before = File.ReadAllText(Path.Combine(_root, "skills.yaml"));

        Assert.NotNull(CourseEditor.SaveSkills(_root, "skills: [ { id: broken"));
        Assert.Equal(before, File.ReadAllText(Path.Combine(_root, "skills.yaml")));

        Assert.Null(CourseEditor.SaveSkills(_root, "skills:\n  - id: b.one\n    category: b\n    label: { de: \"B\" }\n"));
        Assert.Contains("b.one", File.ReadAllText(Path.Combine(_root, "skills.yaml")));
    }
}
