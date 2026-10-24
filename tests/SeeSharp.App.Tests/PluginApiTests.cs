using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using SeeSharp.Core;
using SeeSharp.Progress;

namespace SeeSharp.App.Tests;

/// <summary>The local plugin API (docs/IDE-PLUGINS.md) over a course whose coding exercise is the grading fixture, in a path with spaces, umlauts and commas.</summary>
public sealed class PluginApiTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "seesharp-api-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ProgressStore _store;

    public PluginApiTests()
    {
        Directory.CreateDirectory(_dir);
        _store = new ProgressStore(Path.Combine(_dir, "progress.db"));
        Locale.Apply("de");
    }

    public void Dispose()
    {
        _store.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Every_request_needs_the_token_and_no_foreign_origin()
    {
        await using var host = await Host.StartAsync(WriteCourse(), _store);
        using var bare = new HttpClient { BaseAddress = host.Http.BaseAddress };

        Assert.Equal(HttpStatusCode.Forbidden, (await bare.GetAsync("/api/course")).StatusCode);
        bare.DefaultRequestHeaders.Add(PluginApi.TokenHeader, "0" + host.Token[1..]);
        Assert.Equal(HttpStatusCode.Forbidden, (await bare.GetAsync("/api/course")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bare.PostAsync("/api/exercise/rechner/grade", null)).StatusCode);

        using var foreign = new HttpRequestMessage(HttpMethod.Get, "/api/course");
        foreign.Headers.Add("Origin", "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Http.SendAsync(foreign)).StatusCode);

        using var own = new HttpRequestMessage(HttpMethod.Get, "/api/course");
        own.Headers.Add("Origin", host.Http.BaseAddress!.GetLeftPart(UriPartial.Authority));
        Assert.Equal(HttpStatusCode.OK, (await host.Http.SendAsync(own)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bare.GetAsync("/")).StatusCode); // the UI itself needs no token
    }

    [Fact]
    public async Task Reload_picks_up_a_course_that_appeared_after_the_start()
    {
        var course = Path.Combine(_dir, "Kurs mit Ümlaut, Komma"); // WriteCourse's folder, not there yet
        await using var host = await Host.StartAsync(course, _store);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await host.Http.GetAsync("/api/course")).StatusCode);

        WriteCourse();
        var reload = await host.Http.PostAsync("/api/reload", null);

        Assert.Equal(HttpStatusCode.OK, reload.StatusCode);
        Assert.True((bool)JsonNode.Parse(await reload.Content.ReadAsStringAsync())!["loaded"]!);
        Assert.Equal(HttpStatusCode.OK, (await host.Http.GetAsync("/api/course")).StatusCode);
    }

    [Fact]
    public async Task Review_mode_opens_every_exercise_regardless_of_progress()
    {
        var course = WriteCourse();
        await using var normal = await AppHost.BuildAsync(course, port: 0, admin: false, _store);
        Assert.Contains(normal.Services.GetRequiredService<CourseState>().Progress().Exercises, e => !e.Unlocked);

        await using var review = await AppHost.BuildAsync(course, port: 0, admin: false, _store, review: true);
        var progress = review.Services.GetRequiredService<CourseState>().Progress();
        Assert.All(progress.Exercises, e => Assert.True(e.Unlocked, e.Exercise.Manifest.Id));
        Assert.All(progress.Modules, m => Assert.True(m.Unlocked));
    }

    [Fact]
    public async Task Course_lists_modules_and_exercises_with_paths_and_state()
    {
        var course = WriteCourse();
        await using var host = await Host.StartAsync(course, _store);

        var json = JsonNode.Parse(await host.Http.GetStringAsync("/api/course"))!;

        Assert.Equal("de", (string?)json["locale"]);
        var module = json["modules"]![0]!;
        Assert.Equal(("basics", 1, "Grundlagen", true, false),
            ((string?)module["id"], (int)module["level"]!, (string?)module["title"], (bool)module["unlocked"]!, (bool)module["passed"]!));
        var (quiz, rechner, extra) = (module["exercises"]![0]!, module["exercises"]![1]!, module["exercises"]![2]!);

        Assert.Equal("Rechner bauen", (string?)rechner["title"]);
        Assert.False((bool)rechner["unlocked"]!);
        Assert.Equal("a.one", (string?)rechner["missingSkills"]![0]!["id"]);
        Assert.Equal("Eins", (string?)rechner["missingSkills"]![0]!["label"]);
        Assert.Equal(1, (int)rechner["missingSkills"]![0]!["level"]!);
        var exercise = Path.Combine(course, "modules", "01-basics", "02-rechner");
        Assert.Equal(Path.Combine(exercise, "starter"), (string?)rechner["starterDirectory"]);
        Assert.Equal(Path.Combine(exercise, "starter", "Rechner (C# Übung, Teil 1)"), (string?)rechner["workspaceDirectory"]);
        Assert.Equal(Path.Combine(exercise, "starter", "Rechner (C# Übung, Teil 1)", "Rechner.cs"), (string?)rechner["openFile"]);
        Assert.Equal(3, (int)rechner["openLine"]!);
        Assert.Equal(Path.Combine(exercise, "instructions.de.md"), (string?)rechner["instructionsPath"]);
        Assert.Equal((1, 0, true), ((int)rechner["items"]!["total"]!, (int)rechner["items"]!["passed"]!, (bool)rechner["items"]!["coding"]!));

        Assert.Null(quiz["openFile"]);
        Assert.False((bool)quiz["items"]!["coding"]!);
        Assert.True((bool)extra["optional"]!);

        var locked = JsonNode.Parse(await host.Http.GetStringAsync("/api/exercise/rechner"))!;
        Assert.Null(locked["instructionsHtml"]);
        Assert.Equal(HttpStatusCode.Conflict, (await host.Http.PostAsync("/api/exercise/rechner/grade", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Http.GetAsync("/api/exercise/nope")).StatusCode);
        var open = JsonNode.Parse(await host.Http.GetStringAsync("/api/exercise/erste"))!;
        Assert.Contains("<h1", (string?)open["instructionsHtml"]);
    }

    [Fact]
    public async Task Grade_records_the_attempt_and_credits_the_skills()
    {
        await using var host = await Host.StartAsync(WriteCourse(), _store);
        Unlock();

        var response = await host.Http.PostAsync("/api/exercise/rechner/grade", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.True((bool)json["passed"]!);
        Assert.Equal("Passed", (string?)json["result"]!["outcome"]);
        Assert.Equal(4, json["result"]!["tests"]!.AsArray().Count);
        var attempt = Assert.Single(_store.Attempts("rechner"));
        Assert.True(attempt.Passed);
        Assert.NotNull(attempt.Source);
        Assert.Equal(SkillState.Practiced, _store.HeldSkills()["b.two"].State);
    }

    [Fact]
    public async Task Workspace_writes_editor_and_assistant_files_with_the_no_solution_rule()
    {
        var course = WriteCourse();
        var project = Path.Combine(course, "modules", "01-basics", "02-rechner", "starter", "Rechner (C# Übung, Teil 1)");
        var csproj = Path.Combine(project, "Rechner.csproj");
        File.WriteAllText(csproj, File.ReadAllText(csproj).Replace("<PropertyGroup>", "<PropertyGroup><OutputType>Exe</OutputType>"));
        await using var host = await Host.StartAsync(course, _store);
        Unlock();

        var json = JsonNode.Parse(await (await host.Http.PostAsync("/api/exercise/rechner/workspace", null)).Content.ReadAsStringAsync())!;

        Assert.Equal(project, (string?)json["directory"]);
        string[] expected = [".aiassistant/rules/seesharp.md", ".github/copilot-instructions.md", ".junie/guidelines.md", ".vscode/extensions.json",
            ".vscode/launch.json", ".vscode/settings.json", ".vscode/tasks.json", "AGENTS.md", "CLAUDE.md"];
        Assert.Equal(expected, json["files"]!.AsArray().Select(f => (string?)f));
        foreach (var file in Workspace.AiFiles)
        {
            var text = File.ReadAllText(Path.Combine(project, file));
            Assert.Contains("Never write the solution code for this exercise", text);
            Assert.Contains("# SeeSharp: Rechner bauen", text);
            Assert.Contains("- a.one (Eins)", text); // requires, with label
            Assert.Contains("- b.two (Zwei)", text); // teaches
            Assert.Contains("Level 2 \"Später\": c.three", text);
            Assert.Contains("German", text);
            Assert.Contains("02-rechner/instructions.de.md", text);
        }

        var tasks = JsonNode.Parse(File.ReadAllText(Path.Combine(project, ".vscode", "tasks.json")))!["tasks"]!;
        Assert.Equal("SeeSharp: Prüfen", (string?)tasks[1]!["label"]);
        Assert.Equal(["grade", Path.Combine(course, "modules", "01-basics", "02-rechner", "tests"), Path.Combine(course, "modules", "01-basics", "02-rechner", "starter")],
            tasks[1]!["args"]!.AsArray().Select(a => (string?)a));
        var launch = JsonNode.Parse(File.ReadAllText(Path.Combine(project, ".vscode", "launch.json")))!["configurations"]![0]!;
        Assert.Equal(("coreclr", "build", "${workspaceFolder}/bin/Debug/net10.0/Rechner.dll", "integratedTerminal"),
            ((string?)launch["type"], (string?)launch["preLaunchTask"], (string?)launch["program"], (string?)launch["console"]));
        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(project, ".vscode", "settings.json")))!.AsObject();
        Assert.Equal(["github.copilot.enable", "github.copilot.nextEditSuggestions.enabled", "editor.inlineSuggest.enabled"], settings.Select(p => p.Key));
        Assert.Contains("seesharp.seesharp", File.ReadAllText(Path.Combine(project, ".vscode", "extensions.json")));
    }

    [Fact]
    public void Server_file_is_deleted_only_by_its_own_process()
    {
        var path = Path.Combine(_dir, "server.json");
        PluginApi.WriteServerFile("http://127.0.0.1:5555", "abc", path);
        Assert.Equal(Environment.ProcessId, JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("pid").GetInt32());
        PluginApi.DeleteServerFile(path);
        Assert.False(File.Exists(path));

        File.WriteAllText(path, """{"url":"http://127.0.0.1:1","token":"x","pid":-1}""");
        PluginApi.DeleteServerFile(path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Server_file_is_owner_only_on_unix_even_when_an_old_file_was_world_readable()
    {
        if (OperatingSystem.IsWindows())
            return; // %LOCALAPPDATA% is user-private; CI on Linux covers this.
        var path = Path.Combine(_dir, "private", "server.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        PluginApi.WriteServerFile("http://127.0.0.1:5555", "abc", path);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    private void Unlock() =>
        _store.RecordAttempt(new Attempt("erste", "q1", DateTimeOffset.UtcNow, TimeSpan.Zero, true, "0"),
            new Content.ExerciseManifest { Id = "erste", Teaches = ["a.one"] });

    /// <summary>The app on a free port with a client that sends the token.</summary>
    private sealed class Host(WebApplication app, HttpClient http, string token) : IAsyncDisposable
    {
        public HttpClient Http { get; } = http;
        public string Token { get; } = token;

        public static async Task<Host> StartAsync(string course, ProgressStore store)
        {
            var app = await AppHost.BuildAsync(course, port: 0, admin: false, store);
            var state = app.Services.GetRequiredService<CourseState>();
            state.Settings = new Settings();
            await app.StartAsync();
            var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromMinutes(3) };
            http.DefaultRequestHeaders.Add(PluginApi.TokenHeader, state.ApiToken);
            return new Host(app, http, state.ApiToken);
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private static string Fixture([CallerFilePath] string self = "") =>
        Path.Combine(Path.GetDirectoryName(self)!, "..", "SeeSharp.Grading.Tests", "Fixtures", "Übung #1, Rechner");

    /// <summary>Level 1: a quiz teaching a.one, the Rechner fixture (requires a.one, teaches b.two), an optional open question; level 2 teaches c.three.</summary>
    private string WriteCourse()
    {
        var root = Path.Combine(_dir, "Kurs mit Ümlaut, Komma");
        Write("skills.yaml", "skills:\n  - id: a.one\n    category: a\n    label: { de: \"Eins\" }\n  - id: b.two\n    category: b\n    label: { de: \"Zwei\" }\n  - id: c.three\n    category: c\n    label: { de: \"Drei\" }\n");
        Write("modules/01-basics/module.yaml", "id: basics\ntitle: { de: \"Grundlagen\" }\n");
        Write("modules/01-basics/summary.de.md", "# Grundlagen");
        Write("modules/01-basics/01-erste/manifest.yaml", """
            id: erste
            title: { de: "Erste" }
            teaches: [a.one]
            requires: []
            items:
              - type: multipleChoice
                id: q1
                prompt: { de: "Was?" }
                options: [{ de: "A" }, { de: "B" }]
                correct: [0]
            """);
        Write("modules/01-basics/01-erste/instructions.de.md", "# Erste");
        Write("modules/01-basics/02-rechner/manifest.yaml", """
            id: rechner
            title: { de: "Rechner bauen" }
            teaches: [b.two]
            requires: [a.one]
            items:
              - type: coding
                id: code
                openFile: "Rechner (C# Übung, Teil 1)/Rechner.cs"
                openLine: 3
                testProject: "Rechner Tests (C#)/Rechner.Tests.csproj"
            """);
        Write("modules/01-basics/02-rechner/instructions.de.md", "# Rechner");
        Copy(Path.Combine(Fixture(), "source-pass"), Path.Combine(root, "modules/01-basics/02-rechner/starter"));
        Copy(Path.Combine(Fixture(), "exercise"), Path.Combine(root, "modules/01-basics/02-rechner/tests"));
        Write("modules/01-basics/03-extra/manifest.yaml", """
            id: extra
            title: { de: "Extra" }
            optional: true
            teaches: []
            requires: []
            items:
              - type: open
                id: o1
                prompt: { de: "Warum?" }
            """);
        Write("modules/01-basics/03-extra/instructions.de.md", "# Extra");
        Write("modules/02-later/module.yaml", "id: later\ntitle: { de: \"Später\" }\n");
        Write("modules/02-later/summary.de.md", "# Später");
        Write("modules/02-later/01-dritte/manifest.yaml", """
            id: dritte
            title: { de: "Dritte" }
            teaches: [c.three]
            requires: [b.two]
            items:
              - type: open
                id: o1
                prompt: { de: "Warum?" }
            """);
        Write("modules/02-later/01-dritte/instructions.de.md", "# Dritte");
        return root;

        void Write(string relative, string content)
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        static void Copy(string from, string to)
        {
            foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target);
            }
        }
    }
}
