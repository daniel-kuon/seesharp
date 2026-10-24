using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using SeeSharp.Content;
using SeeSharp.Ide;

namespace SeeSharp.App;

/// <summary>
/// Editor and AI-assistant files written into the starter project folder (the folder of the <c>.csproj</c> that holds
/// <c>openFile</c>, which is what Rider and VS Code open). Regenerated on every IDE launch and "Arbeitsbereich vorbereiten";
/// the course repo ignores them and grading skips the folders (<see cref="Grading.GradingRunner.IsArtifact"/>).
/// </summary>
public static class Workspace
{
    /// <summary>The marketplace id of <c>ide/vscode</c>; recommended in <c>.vscode/extensions.json</c>.</summary>
    public const string VsCodeExtensionId = "seesharp.seesharp";

    /// <summary>Instruction files that IDE assistants read: Copilot agent mode, Junie, Codex and Cursor read AGENTS.md; the rest are tool-specific.</summary>
    public static readonly IReadOnlyList<string> AiFiles =
        ["AGENTS.md", ".github/copilot-instructions.md", "CLAUDE.md", ".junie/guidelines.md", ".aiassistant/rules/seesharp.md"];

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string OpenFile(Exercise exercise, CodingItem item) => Path.GetFullPath(Path.Combine(exercise.StarterDirectory, item.OpenFile));

    /// <summary>The folder the IDE opens: the nearest <c>.csproj</c>'s folder, else the file's folder (as <see cref="IdeLauncher"/> does).</summary>
    public static string Directory(Exercise exercise, CodingItem item)
    {
        var file = OpenFile(exercise, item);
        return Path.GetDirectoryName(IdeLauncher.NearestProject(file) ?? file)!;
    }

    /// <summary>Writes every file and returns the folder plus the written paths relative to it. Throws on I/O failure.</summary>
    public static (string Directory, IReadOnlyList<string> Files) Write(CourseState state, Exercise exercise, CodingItem item)
    {
        var dir = Directory(exercise, item);
        var project = IdeLauncher.NearestProject(OpenFile(exercise, item));
        var files = new Dictionary<string, string>
        {
            [".vscode/tasks.json"] = Tasks(exercise),
            [".vscode/extensions.json"] = new JsonObject
            {
                ["recommendations"] = new JsonArray("ms-dotnettools.csdevkit", "ms-dotnettools.csharp", VsCodeExtensionId),
            }.ToJsonString(Json),
            // Inline completion off; Copilot chat stays usable. The trainee can switch it back on: see docs/UI.md.
            [".vscode/settings.json"] = new JsonObject
            {
                ["github.copilot.enable"] = new JsonObject { ["*"] = false },
                ["github.copilot.nextEditSuggestions.enabled"] = false,
                ["editor.inlineSuggest.enabled"] = false,
            }.ToJsonString(Json),
        };
        if (project is not null && Launch(project) is { } launch)
            files[".vscode/launch.json"] = launch;
        var instructions = AiInstructions(state, exercise);
        foreach (var name in AiFiles)
            files[name] = instructions;

        foreach (var (relative, text) in files)
        {
            var path = Path.Combine(dir, relative);
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        return (dir, files.Keys.Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// <c>build</c> (the launch config's preLaunchTask) and the grade task. Process tasks get their arguments unparsed by a shell,
    /// so spaces, umlauts and commas in the paths need no quoting beyond JSON's.
    /// </summary>
    private static string Tasks(Exercise exercise) => new JsonObject
    {
        ["version"] = "2.0.0",
        ["tasks"] = new JsonArray(
            new JsonObject
            {
                ["label"] = "build",
                ["type"] = "process",
                ["command"] = "dotnet",
                ["args"] = new JsonArray("build", "${workspaceFolder}", "/property:GenerateFullPaths=true", "/consoleloggerparameters:NoSummary"),
                ["group"] = new JsonObject { ["kind"] = "build", ["isDefault"] = true },
                ["problemMatcher"] = "$msCompile",
            },
            new JsonObject
            {
                ["label"] = Strings.WorkspaceGradeTask,
                ["type"] = "process",
                ["command"] = "seesharp",
                ["args"] = new JsonArray("grade", Path.GetFullPath(exercise.TestsDirectory), Path.GetFullPath(exercise.StarterDirectory)),
                ["group"] = "test",
                ["problemMatcher"] = "$msCompile",
                ["presentation"] = new JsonObject { ["reveal"] = "always" },
            }),
    }.ToJsonString(Json);

    /// <summary>Runs the built program; null for a class library (nothing to start). The exercise's tests live outside the starter, so there is no test config.</summary>
    private static string? Launch(string csproj)
    {
        var properties = XDocument.Load(csproj).Descendants().Where(e => e.Parent?.Name.LocalName == "PropertyGroup")
            .GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value.Trim());
        if (!properties.TryGetValue("OutputType", out var type) || !type.EndsWith("exe", StringComparison.OrdinalIgnoreCase)
            || !properties.TryGetValue("TargetFramework", out var tfm))
            return null;
        var assembly = properties.GetValueOrDefault("AssemblyName") ?? Path.GetFileNameWithoutExtension(csproj);
        return new JsonObject
        {
            ["version"] = "0.2.0",
            ["configurations"] = new JsonArray(new JsonObject
            {
                ["name"] = string.Format(Strings.WorkspaceRunConfig, assembly),
                ["type"] = "coreclr",
                ["request"] = "launch",
                ["preLaunchTask"] = "build",
                ["program"] = $"${{workspaceFolder}}/bin/Debug/{tfm}/{assembly}.dll",
                ["cwd"] = "${workspaceFolder}",
                ["console"] = "integratedTerminal",
                ["stopAtEntry"] = false,
            }),
        }.ToJsonString(Json);
    }

    /// <summary>The SeeSharp tutor's rules (<see cref="Tutor.Rules"/>) for IDE assistants, with the exercise's skill set instead of the trainee's ledger.</summary>
    public static string AiInstructions(CourseState state, Exercise exercise)
    {
        var module = state.ModuleOf(exercise);
        var known = exercise.Manifest.Requires.Concat(exercise.Manifest.Teaches).ToHashSet(StringComparer.Ordinal);
        var later = state.Course!.ExercisesInOrder.SelectMany(e => e.Manifest.Teaches).Distinct(StringComparer.Ordinal)
            .Where(s => !known.Contains(s) && state.TaughtIn(s) is { } m && m.Order > module.Order)
            .GroupBy(s => state.TaughtIn(s)!)
            .Select(g => $"- Level {g.Key.Order} \"{CourseState.Text(g.Key.Manifest.Title)}\": {string.Join(", ", g)}");
        var title = CourseState.Text(exercise.Manifest.Title);
        var instructions = exercise.Instructions(Locale.Current).Path;
        return $"""
            # SeeSharp: {title}

            This folder is the starter project of the SeeSharp exercise "{title}" (`{exercise.Manifest.Id}`), level {module.Order} "{CourseState.Text(module.Manifest.Title)}".
            Instructions: [{Path.GetFileName(instructions)}](<{instructions.Replace('\\', '/')}>)

            You are supporting a trainee in a C# course for beginners. You give hints; the trainee writes the code.

            {Tutor.Rules(Locale.Current, "Give hints in escalating steps, one per answer, and go up only while the trainee is still stuck: (a) ONE guiding question that points at where the problem is, no code; (b) a conceptual pointer: the concept or rule that matters, in two to four sentences, no code from the exercise; (c) the concept with a short worked example that solves a DIFFERENT problem (different names, different domain), never the exercise's own solution. Keep answers short.")}
            6. Do not create, edit or complete code in this project, and do not apply changes in agent or edit mode: the trainee types every line. Explaining a compiler error or a failing test in words is fine.

            ## Skills the trainee holds (this exercise's prerequisites)

            {List(exercise.Manifest.Requires, "(none)")}

            ## Skills this exercise teaches

            {List(exercise.Manifest.Teaches, "(none)")}

            ## Later levels (not learned yet)

            {(later.Any() ? string.Join("\n", later) : "(none)")}

            """;

        string List(IEnumerable<string> skills, string empty) =>
            skills.Any() ? string.Join("\n", skills.Select(s => state.SkillLabel(s) is var label && label != s ? $"- {s} ({label})" : $"- {s}")) : empty;
    }
}
