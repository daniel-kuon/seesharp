using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using SeeSharp.Content;
using SeeSharp.Core;
using SeeSharp.Progress;

namespace SeeSharp.App.Tests;

/// <summary>AI tutor (task 9) against a stub <see cref="HttpMessageHandler"/>: both wire formats, failures, logging, export, cost, exam gate.</summary>
public sealed class TutorTests : IDisposable
{
    private const string Key = "sk-secret-test-key-123";
    private readonly string _dir = Directory.CreateTempSubdirectory("seesharp-tutor-tests-").FullName;
    private readonly ProgressStore _store;

    private static readonly TutorContext Context = new("bruch", "# Bruch\nSchreibe einen Konstruktor.", [("Bruch/Bruch.cs", "class Bruch { }")],
        "Outcome: Failed\nFAILED Kuerzt: expected 1/2", ["types.int (Ganzzahlen)"], ["oop.constructor-explicit (Konstruktoren)"],
        ["Level 7 \"Vererbung\": oop.inheritance"], "de");

    public TutorTests() => _store = new ProgressStore(Path.Combine(_dir, "progress.db"));

    public void Dispose()
    {
        _store.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Anthropic_request_carries_key_version_model_and_the_rules_and_the_reply_is_logged_and_exported()
    {
        var stub = new Stub(_ => Json("""
            {"content":[{"type":"thinking","thinking":""},{"type":"text","text":"Was passiert, wenn **Nenner** 0 ist?"}],
             "stop_reason":"end_turn","usage":{"input_tokens":1200,"output_tokens":80}}
            """));
        var settings = new Settings { TutorProvider = Tutor.Anthropic, TutorApiKey = Key };

        var (exchange, error) = await Tutor.AskAsync(new HttpClient(stub), settings, _store, Context, 1, "Gib mir einen Hinweis.");

        Assert.Null(error);
        var request = stub.Requests.Single();
        Assert.Equal("https://api.anthropic.com/v1/messages", request.Uri);
        Assert.Equal(Key, request.Headers["x-api-key"]);
        Assert.Equal(Tutor.AnthropicVersion, request.Headers["anthropic-version"]);
        Assert.False(request.Headers.ContainsKey("Authorization"));
        var body = JsonNode.Parse(request.Body)!;
        Assert.Equal("claude-haiku-5-5", (string?)body["model"]);
        AssertRules((string)body["system"]!);
        var last = body["messages"]!.AsArray().Last()!;
        Assert.Equal("user", (string?)last["role"]);
        Assert.Contains(Tutor.LevelInstruction(1), (string)last["content"]!);

        Assert.NotNull(exchange);
        Assert.Equal("Was passiert, wenn **Nenner** 0 ist?", exchange.Answer);
        Assert.Equal((1200, 80), (exchange.InputTokens, exchange.OutputTokens));
        Assert.Equal((1200 * 0.10m + 80 * 0.50m) / 1_000_000m, exchange.CostUsd);
        var logged = Assert.Single(_store.TutorExchanges("bruch"));
        Assert.Equal((1, "anthropic", "claude-haiku-5-5", "Gib mir einen Hinweis."), (logged.Level, logged.Provider, logged.Model, logged.Question));

        var zip = Path.Combine(_dir, "review.zip");
        ReviewExport.Write(_store, zip);
        using (var archive = ZipFile.OpenRead(zip))
        {
            using var reader = new StreamReader(archive.GetEntry("tutor/bruch.md")!.Open());
            var markdown = await reader.ReadToEndAsync();
            Assert.Contains("level 1 · anthropic/claude-haiku-5-5 · 1200 in + 80 out tokens", markdown);
            Assert.Contains("Was passiert, wenn **Nenner** 0 ist?", markdown);
            Assert.Contains("Gib mir einen Hinweis.", markdown);
        }
        _store.Dispose();
        Assert.DoesNotContain(Key, Encoding.UTF8.GetString(await File.ReadAllBytesAsync(Path.Combine(_dir, "progress.db"))));
        Assert.DoesNotContain(Key, Encoding.UTF8.GetString(await File.ReadAllBytesAsync(zip)));
    }

    [Fact]
    public async Task Chat_completions_for_ollama_openai_and_compatible_services()
    {
        var stub = new Stub(_ => Json("""{"choices":[{"message":{"role":"assistant","content":"Denk an den Konstruktor."}}],"usage":{"prompt_tokens":900,"completion_tokens":40}}"""));
        var http = new HttpClient(stub);

        var (local, _) = await Tutor.AskAsync(http, new Settings { TutorProvider = Tutor.Ollama }, _store, Context, 2, "Warum?");
        var ollama = stub.Requests[0];
        Assert.Equal("http://localhost:11434/v1/chat/completions", ollama.Uri);
        Assert.False(ollama.Headers.ContainsKey("Authorization"));
        var body = JsonNode.Parse(ollama.Body)!;
        Assert.Equal("qwen2.5-coder:7b", (string?)body["model"]);
        var messages = body["messages"]!.AsArray();
        Assert.Equal("system", (string?)messages[0]!["role"]);
        AssertRules((string)messages[0]!["content"]!);
        Assert.Contains(Tutor.LevelInstruction(2), (string)messages[^1]!["content"]!);
        Assert.Equal((900, 40, 0m), (local!.InputTokens, local.OutputTokens, local.CostUsd));

        var (paid, _) = await Tutor.AskAsync(http, new Settings { TutorProvider = Tutor.OpenAi, TutorApiKey = Key }, _store, Context, 3, "Und jetzt?");
        Assert.Equal("https://api.openai.com/v1/chat/completions", stub.Requests[1].Uri);
        Assert.Equal("Bearer " + Key, stub.Requests[1].Headers["Authorization"]);
        Assert.Equal((900 * 0.75m + 40 * 4.50m) / 1_000_000m, paid!.CostUsd);
        // The earlier exchange of this exercise goes along as history.
        var history = JsonNode.Parse(stub.Requests[1].Body)!["messages"]!.AsArray();
        Assert.Equal(["system", "user", "assistant", "user"], history.Select(m => (string)m!["role"]!));
        Assert.Equal("Denk an den Konstruktor.", (string?)history[2]!["content"]);

        var (free, _) = await Tutor.AskAsync(http, new Settings
        {
            TutorProvider = Tutor.OpenAiCompatible, TutorBaseUrl = "https://api.groq.com/openai/v1/", TutorModel = "some-free-model", TutorApiKey = Key,
        }, _store, Context, 1, "?");
        Assert.Equal("https://api.groq.com/openai/v1/chat/completions", stub.Requests[2].Uri);
        Assert.Null(free!.CostUsd); // unknown price: tokens only
    }

    [Fact]
    public async Task Failures_come_back_as_a_quiet_message_and_log_nothing()
    {
        var settings = new Settings { TutorProvider = Tutor.Ollama };
        async Task<string?> Fails(HttpMessageHandler handler, Settings? s = null, TimeSpan? timeout = null)
        {
            var (exchange, error) = await Tutor.AskAsync(new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromSeconds(30) }, s ?? settings, _store, Context, 1, "?");
            Assert.Null(exchange);
            Assert.False(string.IsNullOrWhiteSpace(error));
            return error;
        }

        await Fails(new Stub(_ => throw new HttpRequestException("No connection could be made")));
        Assert.Contains("500", await Fails(new Stub(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)))));
        await Fails(new Stub(_ => Json("<html>not json</html>")));
        await Fails(new Stub(_ => Json("""{"choices":[{"message":{"content":""}}]}""")));
        await Fails(new Stub(async _ => { await Task.Delay(TimeSpan.FromSeconds(10)); return new HttpResponseMessage(HttpStatusCode.OK); }), timeout: TimeSpan.FromMilliseconds(200));
        var never = new Stub(_ => throw new InvalidOperationException("must not be called"));
        await Fails(never, new Settings { TutorProvider = Tutor.Anthropic }); // no key: refused before any request
        await Fails(never, new Settings { TutorProvider = Tutor.OpenAiCompatible, TutorModel = "m" }); // no base URL
        Assert.Empty(never.Requests);
        Assert.Empty(_store.TutorExchanges());
    }

    [Fact]
    public void Cost_estimates_use_the_price_table_and_ollama_is_free()
    {
        Assert.Equal(0.10m * 0.1m + 0.50m, Tutor.Cost(Tutor.Anthropic, "claude-haiku-5-5", 100_000, 1_000_000));
        Assert.Equal(0.50m * 0.2m + 2.50m * 0.01m, Tutor.Cost(Tutor.Anthropic, "claude-haiku-5-5", 200_000, 10_000)); // long-prompt tier above 100K
        Assert.Equal(4m + 20m, Tutor.Cost(Tutor.Anthropic, "claude-opus-5-5", 1_000_000, 1_000_000));
        Assert.Equal(0.25m + 2m, Tutor.Cost(Tutor.OpenAi, "gpt-5-mini", 1_000_000, 1_000_000));
        Assert.Equal(0m, Tutor.Cost(Tutor.Ollama, "anything", 5000, 5000));
        Assert.Null(Tutor.Cost(Tutor.OpenAiCompatible, "gpt-5-mini", 1, 1));
        Assert.Null(Tutor.Cost(Tutor.Anthropic, "claude-unknown", 1, 1));
    }

    [Fact]
    public async Task Panel_shows_on_coding_exercises_only_when_a_provider_is_set_and_never_during_an_exam()
    {
        Locale.Apply("de");
        var app = await AppHost.BuildAsync(WriteCourse(), port: 0, admin: false, _store);
        var state = app.Services.GetRequiredService<CourseState>();
        var course = state.Course!;
        _store.RecordAttempt(new Attempt("first", "q1", DateTimeOffset.UtcNow, TimeSpan.Zero, true, "0"), course.Modules[0].Exercises[0].Manifest);
        var grading = """{"outcome":"Failed","duration":"00:00:01","tests":[{"name":"Kuerzt","outcome":"Failed","duration":"00:00:00","message":"expected 1/2","stackTrace":null}],"buildErrors":[],"output":"","artifactsDirectory":null}""";
        _store.RecordAttempt(new Attempt("second", "code", DateTimeOffset.UtcNow, TimeSpan.Zero, false, GradingJson: grading));
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            const string panel = "Tutor um Hilfe fragen";

            state.Settings = new Settings();
            Assert.DoesNotContain(panel, await http.GetStringAsync("/exercise/second")); // off by default

            state.Settings = new Settings { TutorProvider = Tutor.Ollama };
            Assert.Contains(panel, await http.GetStringAsync("/exercise/second"));
            Assert.DoesNotContain(panel, await http.GetStringAsync("/exercise/first")); // no coding item

            var context = Tutor.Context(state, course.Modules[0].Exercises[1]);
            Assert.Equal(["X/X.cs"], context.Sources.Select(s => s.Path)); // bin/obj skipped
            Assert.Contains("FAILED Kuerzt: expected 1/2", context.Grading);
            Assert.Equal(["a.one (Eins)"], context.HeldSkills);
            Assert.Equal(["b.two (Zwei)"], context.Teaches);
            Assert.Equal(["Level 2 \"Später\": c.three"], context.LaterLevels);

            var run = Exams.Start(_store, course, course.Exams.Single(), seed: 1);
            Assert.DoesNotContain(panel, await http.GetStringAsync("/exercise/second"));
            Assert.DoesNotContain(panel, await http.GetStringAsync($"/exam/{run.Id}"));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private static void AssertRules(string system)
    {
        Assert.Contains("Never write the solution code for this exercise", system);
        Assert.Contains("Do not introduce concepts outside that set", system);
        Assert.Contains("name the level that covers it", system);
        Assert.Contains("types.int (Ganzzahlen)", system);
        Assert.Contains("oop.constructor-explicit (Konstruktoren)", system);
        Assert.Contains("Level 7 \"Vererbung\": oop.inheritance", system);
        Assert.Contains("class Bruch { }", system);
        Assert.Contains("FAILED Kuerzt", system);
        Assert.DoesNotContain(Key, system);
    }

    private static Task<HttpResponseMessage> Json(string json) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    private sealed record Captured(string Uri, Dictionary<string, string> Headers, string Body);

    private sealed class Stub(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<Captured> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Captured(request.RequestUri!.ToString(), request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return await respond(request).WaitAsync(cancellationToken);
        }
    }

    /// <summary>Level 1: a quiz and a coding exercise with a starter (plus obj/ junk); level 2 teaches a skill not held yet; one practice exam.</summary>
    private string WriteCourse()
    {
        var root = Path.Combine(_dir, "course");
        Write("skills.yaml", "skills:\n  - id: a.one\n    category: a\n    label: { de: \"Eins\" }\n  - id: b.two\n    category: b\n    label: { de: \"Zwei\" }\n  - id: c.three\n    category: c\n    label: { de: \"Drei\" }\n");
        Write("modules/01-basics/module.yaml", "id: basics\ntitle: { de: \"Grundlagen\" }\n");
        Write("modules/01-basics/summary.de.md", "# Grundlagen");
        Write("modules/01-basics/01-first/manifest.yaml", """
            id: first
            title: { de: "Erste" }
            teaches: [a.one]
            requires: []
            items:
              - type: multipleChoice
                id: q1
                prompt: { de: "Was?" }
                options: [{ de: "A" }, { de: "B" }]
                correct: [0]
                explanation: { de: "Weil." }
            """);
        Write("modules/01-basics/01-first/instructions.de.md", "# Erste");
        Write("modules/01-basics/02-second/manifest.yaml", """
            id: second
            title: { de: "Zweite" }
            teaches: [b.two]
            requires: [a.one]
            items:
              - type: coding
                id: code
                openFile: "X/X.cs"
                testProject: "T/T.csproj"
            """);
        Write("modules/01-basics/02-second/instructions.de.md", "# Zweite");
        Write("modules/01-basics/02-second/starter/X/X.cs", "class X { }");
        Write("modules/01-basics/02-second/starter/X/obj/Debug/Junk.cs", "// generated");
        Write("modules/02-later/module.yaml", "id: later\ntitle: { de: \"Später\" }\n");
        Write("modules/02-later/summary.de.md", "# Später");
        Write("modules/02-later/01-third/manifest.yaml", """
            id: third
            title: { de: "Dritte" }
            teaches: [c.three]
            requires: [b.two]
            items:
              - type: open
                id: o1
                prompt: { de: "Warum?" }
            """);
        Write("modules/02-later/01-third/instructions.de.md", "# Dritte");
        Write("exams/probe.yaml", "id: probe\ntitle: { de: \"Probe\" }\nkind: practice\ncoverage: { levels: [1] }\ntimeLimitMinutes: 30\n");
        return root;

        void Write(string relative, string content)
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
