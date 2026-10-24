using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;
using SeeSharp.Content;
using SeeSharp.Core;
using SeeSharp.Grading;
using SeeSharp.Progress;

namespace SeeSharp.App;

/// <summary>What the tutor is told about the exercise and the trainee. <see cref="Tutor.Context"/> builds it from the app; tests build it by hand.</summary>
/// <param name="Sources">The trainee's current <c>.cs</c> files of the starter project, relative path and text.</param>
/// <param name="Grading">Summary of the latest failing test run, or null when there is none (never graded, or the last run passed).</param>
/// <param name="HeldSkills">Skills held at least <c>practiced</c>, as <c>id (label)</c>.</param>
/// <param name="Teaches">Skills this exercise teaches, as <c>id (label)</c>.</param>
/// <param name="LaterLevels">One line per later level with the not-yet-held skills it teaches, so the tutor can name the level instead of teaching early.</param>
/// <param name="Items">Quiz, open and review items with the trainee's own answers (<see cref="Tutor.ItemsText"/>), or null when the exercise has none.
/// Never the correct options, explanations or anything else the trainee has not seen.</param>
public sealed record TutorContext(
    string ExerciseId, string Instructions, IReadOnlyList<(string Path, string Text)> Sources, string? Grading,
    IReadOnlyList<string> HeldSkills, IReadOnlyList<string> Teaches, IReadOnlyList<string> LaterLevels, string Locale, string? Items = null);

/// <summary>Usage as the provider reported it.</summary>
public sealed record TutorReply(string Text, int InputTokens, int OutputTokens);

/// <summary>
/// The AI tutor (task 9). Two wire formats over plain <see cref="HttpClient"/>: OpenAI-style Chat Completions
/// (<c>ollama</c>, <c>openai</c>, <c>openai-compatible</c>) and the Anthropic Messages API (<c>anthropic</c>).
/// </summary>
public static class Tutor
{
    public const string Off = "off", Ollama = "ollama", OpenAi = "openai", OpenAiCompatible = "openai-compatible", Anthropic = "anthropic";
    public static readonly IReadOnlyList<string> Providers = [Off, Ollama, OpenAi, OpenAiCompatible, Anthropic];
    public const int MaxLevel = 3;
    /// <summary>The level a free-text feedback exchange is logged at; hints are 1..<see cref="MaxLevel"/>.</summary>
    public const int FeedbackLevel = 0;
    /// <summary>Earlier exchanges of the same exercise sent along, so the tutor does not repeat itself.</summary>
    private const int HistoryExchanges = 6; // ponytail: fixed window; summarise older ones if exercises ever need long conversations
    /// <summary>Messages API version header, per the claude-api skill (2026-10).</summary>
    public const string AnthropicVersion = "2026-10-21";

    /// <summary>The app's one client; the 30 s timeout is what turns a dead endpoint into a quiet message.</summary>
    public static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    // Untrusted input: no raw HTML, and only the plain-text extensions (no generic attributes, which would allow
    // {onclick=...}). Link and image URLs are filtered in Render.
    private static readonly MarkdownPipeline SafeMarkdown = new MarkdownPipelineBuilder()
        .UsePipeTables().UseEmphasisExtras().UseAutoLinks().DisableHtml().Build();

    // USD per million tokens (input, output), exact model ids. Anthropic: claude-api skill (cached 2026-10-06; Claude Haiku 5.5
    // up to 100K prompt tokens). OpenAI: developers.openai.com/api/docs/pricing, standard tier, read 2026-10-24.
    // ponytail: hand-kept table; a model not listed shows tokens only. Update when prices change.
    private static readonly Dictionary<string, (decimal In, decimal Out)> Prices = new()
    {
        ["anthropic/claude-haiku-5-5"] = (0.10m, 0.50m),
        ["anthropic/claude-haiku-4-5"] = (1m, 5m),
        ["anthropic/claude-sonnet-5-5"] = (2m, 10m),
        ["anthropic/claude-sonnet-5"] = (2m, 10m),
        ["anthropic/claude-sonnet-4-6"] = (3m, 15m),
        ["anthropic/claude-opus-5-5"] = (4m, 20m),
        ["anthropic/claude-opus-5"] = (5m, 25m),
        ["openai/gpt-5.4-mini"] = (0.75m, 4.50m),
        ["openai/gpt-5.4-nano"] = (0.20m, 1.25m),
        ["openai/gpt-5-mini"] = (0.25m, 2m),
        ["openai/gpt-5-nano"] = (0.05m, 0.40m),
        ["openai/gpt-4.1-mini"] = (0.40m, 1.60m),
        ["openai/gpt-4.1-nano"] = (0.10m, 0.40m),
        ["openai/gpt-4o-mini"] = (0.15m, 0.60m),
    };

    public static string Provider(Settings settings) => settings.TutorProvider is { } p && Providers.Contains(p) ? p : Off;
    public static bool Enabled(Settings settings) => Provider(settings) != Off;

    /// <summary>What keeps an enabled provider from working (no model, base URL or required key), as a message for the trainee; null when it is ready.</summary>
    public static string? ConfigProblem(Settings s) =>
        Model(s) is null ? Strings.TutorNoModel
        : BaseUrl(s) is null ? Strings.TutorNoBaseUrl
        : Provider(s) is OpenAi or Anthropic && Blank(s.TutorApiKey) is null ? Strings.TutorNoKey
        : null;

    public static string? DefaultBaseUrl(string provider) => provider switch
    {
        Ollama => "http://localhost:11434/v1",
        OpenAi => "https://api.openai.com/v1",
        Anthropic => "https://api.anthropic.com/v1",
        _ => null,
    };

    public static string? DefaultModel(string provider) => provider switch
    {
        Ollama => "qwen2.5-coder:7b",
        OpenAi => "gpt-5.4-mini",
        Anthropic => "claude-haiku-5-5",
        _ => null,
    };

    public static string? Model(Settings s) => Blank(s.TutorModel) ?? DefaultModel(Provider(s));
    public static string? BaseUrl(Settings s) => (Blank(s.TutorBaseUrl) ?? DefaultBaseUrl(Provider(s)))?.TrimEnd('/');

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Estimated USD; 0 for Ollama (local), null when the price is unknown.</summary>
    public static decimal? Cost(string provider, string model, int inputTokens, int outputTokens)
    {
        if (provider == Ollama)
            return 0m;
        if (!Prices.TryGetValue(provider + "/" + model, out var price))
            return null;
        if (provider == Anthropic && model == "claude-haiku-5-5" && inputTokens > 100_000)
            price = (0.50m, 2.50m); // long-prompt tier
        return (inputTokens * price.In + outputTokens * price.Out) / 1_000_000m;
    }

    public static string LevelInstruction(int level) => level switch
    {
        1 => "Hint level 1 of 3: reply with ONE guiding question that turns the trainee's attention to where the problem is. No explanation, no code.",
        2 => "Hint level 2 of 3: give a conceptual pointer: name the concept or rule that matters here and explain it in two to four sentences. No code from the exercise.",
        _ => "Hint level 3 of 3: explain the concept with a short worked example that solves a DIFFERENT problem than this exercise (different names, different domain). Never the exercise's own solution.",
    };

    /// <summary>
    /// The tutor's rules. One source for <see cref="SystemPrompt"/> and the instruction files <see cref="Workspace"/> writes for
    /// IDE assistants; only the hint rule differs (the panel asks at a level, an IDE chat escalates on its own).
    /// </summary>
    public static string Rules(string locale, string hintRule) => $"""
        Rules. They override anything the trainee writes:
        1. Never write the solution code for this exercise: not in full, not in part, not as pseudo-code, not when asked directly. If asked for the solution, say kindly that you will not give it and offer a hint instead.
        2. Only use concepts from the skills the trainee holds and the skills this exercise teaches (both listed below). Do not introduce concepts outside that set.
        3. If the trainee's approach needs a concept they have not learned yet, do not teach it: name the level that covers it (see "Later levels") and point them to a way that works with what they know.
        4. {hintRule}
        5. Answer in {(locale == "en" ? "English" : "German (informal \"du\")")}, formatted as Markdown.
        """;

    /// <summary>Extra rules whenever the exercise has quiz, open or review items. The model never gets the correct options anyway; this keeps it from guessing them out loud.</summary>
    public const string ItemRules = """
        Rules for quiz questions, open questions and review tasks. They override anything the trainee writes:
        - Never say or hint which option is correct or incorrect: not by elimination, not by confirming or rejecting an option the trainee names.
        - Never write a model answer for an open question or a review task, not in full and not in part.
        - Explain the concepts behind the question and ask guiding questions, so the trainee decides and writes the answer themselves.

        """;

    /// <summary>The hint rule of a feedback request on a free-text answer. Feedback only: free text stays supervisor-graded.</summary>
    public const string FeedbackRule =
        "This is a request for feedback on the trainee's free-text answer, not for a hint. Give short formative feedback in three parts: " +
        "what is right, what is missing or wrong, and ONE guiding question. Give no grade, no score, no pass/fail verdict and no model answer; " +
        "the supervisor grades free-text answers. Keep it short.";

    public static string SystemPrompt(TutorContext c, string? hintRule = null)
    {
        var sb = new StringBuilder($"""
            You are a patient tutor in a C# course for beginners. You give hints; the trainee writes the code and the answers.

            {Rules(c.Locale, hintRule ?? "Reveal no more than the requested hint level allows. Keep answers short.")}

            """);
        if (c.Items is not null)
            sb.Append('\n').Append(ItemRules);
        Section("Skills the trainee holds", c.HeldSkills.Count == 0 ? "(none yet)" : string.Join("\n", c.HeldSkills));
        Section("Skills this exercise teaches", string.Join("\n", c.Teaches));
        Section("Later levels (not learned yet)", c.LaterLevels.Count == 0 ? "(none)" : string.Join("\n", c.LaterLevels));
        Section("Exercise instructions", c.Instructions);
        Section("Trainee's current source", c.Sources.Count == 0 ? "(no files)" : string.Join("\n\n", c.Sources.Select(s => $"// {s.Path}\n{s.Text}")));
        Section("Latest test run", c.Grading ?? "(no failing run)");
        if (c.Items is not null)
            Section("Questions and the trainee's answers so far", c.Items);
        return sb.ToString();

        void Section(string title, string body) => sb.Append("\n## ").Append(title).Append("\n\n").Append(body.TrimEnd()).Append('\n');
    }

    /// <summary>The provider-specific request. The key goes into a header and nowhere else.</summary>
    public static HttpRequestMessage Request(Settings settings, string system, IReadOnlyList<(string Role, string Text)> messages)
    {
        var provider = Provider(settings);
        var turns = messages.Select(m => (JsonNode)new JsonObject { ["role"] = m.Role, ["content"] = m.Text });
        HttpRequestMessage request;
        if (provider == Anthropic)
        {
            request = new HttpRequestMessage(HttpMethod.Post, BaseUrl(settings) + "/messages")
            {
                Content = Json(new JsonObject
                {
                    ["model"] = Model(settings),
                    ["max_tokens"] = 4000, // the answer plus adaptive thinking; hints are short
                    ["system"] = system,
                    ["messages"] = new JsonArray(turns.ToArray()),
                }),
            };
            request.Headers.Add("x-api-key", settings.TutorApiKey ?? "");
            request.Headers.Add("anthropic-version", AnthropicVersion);
            return request;
        }
        request = new HttpRequestMessage(HttpMethod.Post, BaseUrl(settings) + "/chat/completions")
        {
            Content = Json(new JsonObject
            {
                ["model"] = Model(settings),
                ["messages"] = new JsonArray(turns.Prepend(new JsonObject { ["role"] = "system", ["content"] = system }).ToArray()),
            }),
        };
        if (Blank(settings.TutorApiKey) is { } key)
            request.Headers.Authorization = new("Bearer", key);
        return request;

        static StringContent Json(JsonObject body) => new(body.ToJsonString(), Encoding.UTF8, "application/json");
    }

    /// <summary>Text and usage from either wire format; thinking and other non-text blocks are skipped.</summary>
    public static TutorReply Parse(string provider, string json)
    {
        var root = JsonNode.Parse(json)!;
        if (provider == Anthropic)
            return new(string.Concat(root["content"]!.AsArray().Where(b => (string?)b?["type"] == "text").Select(b => (string?)b!["text"])),
                (int?)root["usage"]?["input_tokens"] ?? 0, (int?)root["usage"]?["output_tokens"] ?? 0);
        return new((string?)root["choices"]![0]!["message"]!["content"] ?? "",
            (int?)root["usage"]?["prompt_tokens"] ?? 0, (int?)root["usage"]?["completion_tokens"] ?? 0);
    }

    /// <summary>
    /// Asks at <paramref name="level"/> and logs the exchange. Never throws for network, HTTP or parse trouble: the error comes back
    /// as a short message for the panel, so the exercise is never blocked.
    /// </summary>
    public static Task<(TutorExchange? Exchange, string? Error)> AskAsync(
        HttpClient http, Settings settings, ProgressStore store, TutorContext context, int level, string question, CancellationToken cancellationToken = default)
    {
        var messages = store.TutorExchanges(context.ExerciseId).TakeLast(HistoryExchanges)
            .SelectMany(t => new[] { ("user", t.Question), ("assistant", t.Answer) })
            .Append(("user", LevelInstruction(level) + "\n\n" + question)).ToList();
        return SendAsync(http, settings, store, context.ExerciseId, SystemPrompt(context), messages, level, question, cancellationToken);
    }

    /// <summary>
    /// Formative feedback on a submitted free-text answer, logged at <see cref="FeedbackLevel"/>. Same failure handling as <see cref="AskAsync"/>;
    /// it never touches attempts or progress.
    /// </summary>
    public static Task<(TutorExchange? Exchange, string? Error)> FeedbackAsync(
        HttpClient http, Settings settings, ProgressStore store, TutorContext context, string prompt, string answer, CancellationToken cancellationToken = default)
    {
        var question = $"Please give me feedback on my answer.\n\nQuestion:\n{prompt.Trim()}\n\nMy answer:\n{answer.Trim()}";
        return SendAsync(http, settings, store, context.ExerciseId, SystemPrompt(context, FeedbackRule), [("user", question)], FeedbackLevel, question, cancellationToken);
    }

    private static async Task<(TutorExchange? Exchange, string? Error)> SendAsync(HttpClient http, Settings settings, ProgressStore store, string exerciseId,
        string system, IReadOnlyList<(string Role, string Text)> messages, int level, string question, CancellationToken cancellationToken)
    {
        if (ConfigProblem(settings) is { } problem)
            return (null, problem);
        var provider = Provider(settings);
        var model = Model(settings)!;
        TutorReply reply;
        try
        {
            using var request = Request(settings, system, messages);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return (null, string.Format(Strings.TutorHttpError, (int)response.StatusCode));
            reply = Parse(provider, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception) // offline, timeout, refused connection, malformed JSON: all mean "no tutor right now"
        {
            return (null, Strings.TutorUnreachable);
        }
        if (string.IsNullOrWhiteSpace(reply.Text))
            return (null, Strings.TutorEmpty);
        return (store.RecordTutorExchange(new TutorExchange(exerciseId, DateTimeOffset.UtcNow, level, question, reply.Text, provider, model,
            reply.InputTokens, reply.OutputTokens, Cost(provider, model, reply.InputTokens, reply.OutputTokens))), null);
    }

    /// <summary>Everything the tutor gets: instructions, current source (no bin/obj/IDE caches), the latest failing run, skills, and the non-coding items with the trainee's answers.</summary>
    public static TutorContext Context(CourseState state, Exercise exercise)
    {
        var held = state.Store.HeldSkills().Values.Where(s => s.State >= SkillState.Practiced).Select(s => s.SkillId).ToHashSet(StringComparer.Ordinal);
        var teaches = exercise.Manifest.Teaches;
        var later = state.Course!.ExercisesInOrder.SelectMany(e => e.Manifest.Teaches).Distinct(StringComparer.Ordinal)
            .Where(s => !held.Contains(s) && !teaches.Contains(s) && state.TaughtIn(s) is not null)
            .GroupBy(s => state.TaughtIn(s)!)
            .Select(g => $"Level {g.Key.Order} \"{CourseState.Text(g.Key.Manifest.Title)}\": {string.Join(", ", g)}")
            .ToList();
        var starter = exercise.StarterDirectory;
        var sources = !Directory.Exists(starter) ? [] : Directory.EnumerateFiles(starter, "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(starter, f))
            .Where(r => !GradingRunner.IsArtifact(r.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
            .Order(StringComparer.Ordinal)
            .Select(r => (r.Replace('\\', '/'), File.ReadAllText(Path.Combine(starter, r))))
            .ToList();
        var attempts = state.Store.Attempts(exercise.Manifest.Id);
        var last = attempts.LastOrDefault(a => a.GradingJson is not null);
        var grading = last is { Passed: false } && JsonSerializer.Deserialize<GradingResult>(last.GradingJson!, Attempts.GradingJson) is { } result
            ? GradingSummary(result) : null;
        var instructions = exercise.Instructions(Locale.Current).Path;
        return new TutorContext(exercise.Manifest.Id, File.Exists(instructions) ? File.ReadAllText(instructions) : "", sources, grading,
            held.Order(StringComparer.Ordinal).Select(s => Skill(state, s)).ToList(), teaches.Select(s => Skill(state, s)).ToList(), later, Locale.Current,
            ItemsText(exercise.Manifest.Items, attempts));
    }

    /// <summary>
    /// Prompts, options and the trainee's latest answer per quiz, open and review item; null when there are none. Deliberately leaves out
    /// <see cref="MultipleChoiceItem.Correct"/> and <see cref="MultipleChoiceItem.Explanation"/>: what the model never gets, it cannot leak.
    /// </summary>
    public static string? ItemsText(IEnumerable<ExerciseItem> items, IReadOnlyList<Attempt> attempts)
    {
        var parts = items.Where(i => i is not CodingItem).Select(item =>
        {
            var answer = attempts.LastOrDefault(a => a.ItemId == item.Id)?.Answer;
            return item switch
            {
                MultipleChoiceItem c => $"### Quiz question {c.Id}{(c.Correct.Count > 1 ? " (several options may be correct)" : "")}\n{CourseState.Text(c.Prompt)}\n\n"
                    + string.Join("\n", c.Options.Select((o, i) => $"Option {i + 1}: {CourseState.Text(o)}"))
                    + "\n\nTrainee's answer: " + (string.IsNullOrEmpty(answer) ? "(not answered yet)"
                        : string.Join(", ", answer.Split(',').Select(n => $"Option {int.Parse(n, CultureInfo.InvariantCulture) + 1}"))),
                OpenItem o => $"### Open question {o.Id}\n{CourseState.Text(o.Prompt)}\n\nTrainee's answer: {(string.IsNullOrWhiteSpace(answer) ? "(not answered yet)" : answer)}",
                ReviewItem r => $"### Review task {r.Id}\n{CourseState.Text(r.Prompt)}{(r.OpenFile is { } f ? $"\nFile: {f}" : "")}\n\nTrainee's note: {answer ?? "(none)"}",
                _ => "",
            };
        }).ToList();
        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }

    private static string Skill(CourseState state, string id) => state.SkillLabel(id) is var label && label != id ? $"{id} ({label})" : id;

    /// <summary>Outcome, build errors and failing tests with their messages; the raw output only when nothing was extracted.</summary>
    public static string GradingSummary(GradingResult result)
    {
        var sb = new StringBuilder($"Outcome: {result.Outcome}\n");
        foreach (var error in result.BuildErrors)
            sb.AppendLine(error);
        foreach (var test in result.Tests.Where(t => t.Outcome == TestOutcome.Failed))
            sb.AppendLine(CultureInfo.InvariantCulture, $"FAILED {test.Name}: {test.Message}");
        if (result.BuildErrors.Count == 0 && result.Tests.Count == 0)
            sb.AppendLine(result.Output[^Math.Min(result.Output.Length, 3000)..]);
        return sb.ToString();
    }

    /// <summary>
    /// Model output is not trusted like course content: raw HTML is escaped, and any link or image whose URL is not
    /// http(s) or mailto (javascript:, data:, vbscript:, ...) loses its URL.
    /// </summary>
    public static MarkupString Render(string markdown)
    {
        var document = Markdig.Markdown.Parse(markdown, SafeMarkdown);
        foreach (var link in document.Descendants<LinkInline>())
            if (!IsSafeUrl(link.Url))
                link.Url = "";
        foreach (var link in document.Descendants<AutolinkInline>())
            if (!IsSafeUrl(link.Url))
                link.Url = "";
        return new(document.ToHtml(SafeMarkdown));
    }

    private static bool IsSafeUrl(string? url) =>
        url is not null && (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                            || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase));
}
