using Markdig;
using Microsoft.AspNetCore.Components;
using SeeSharp.Content;
using SeeSharp.Ide;
using SeeSharp.Progress;

namespace SeeSharp.App;

/// <summary>
/// The one loaded course plus the progress store, shared by every circuit. Reloading swaps the
/// course reference; pages read it fresh on every render.
/// </summary>
public sealed class CourseState(ProgressStore store, string? courseArgument, bool admin = false, bool review = false) : IDisposable
{
    /// <summary><c>seesharp --review</c>: every module and exercise is open regardless of progress (for reviewing
    /// content). Attempts are still recorded and credited as usual; only the lock is lifted.</summary>
    public bool Review { get; } = review;
    private static readonly MarkdownPipeline Markdown = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public ProgressStore Store { get; } = store;
    /// <summary><c>seesharp --admin</c>: the editor pages under <c>/admin</c> exist only then (AppHost returns 404 and Routes renders nothing otherwise).</summary>
    public bool Admin { get; } = admin;
    /// <summary>Skill graph summary of the last load, for the admin skills view.</summary>
    public SkillGraphSummary Summary { get; private set; } = new(0, 0, [], [], []);
    /// <summary>Re-read from disk by <see cref="LoadAsync"/>; tests and scratch hosts set it directly to avoid the real settings.json.</summary>
    public Settings Settings { get; set; } = Settings.Load();
    public string? CoursePath { get; private set; }
    public Course? Course { get; private set; }
    public IReadOnlyList<Finding> Findings { get; private set; } = [];
    /// <summary>Absolute path of the book, or null when the file is not there (links fall back to text).</summary>
    public string? BookPdfPath { get; private set; }
    /// <summary>The IDEs found on this machine with their executables; see <see cref="DetectIdes"/>.</summary>
    public IReadOnlyDictionary<IdeKind, string> Ides { get; private set; } = new Dictionary<IdeKind, string>();
    /// <summary>Required as <c>X-SeeSharp-Token</c> on every <c>/api</c> request; published in <see cref="PluginApi.ServerFile"/>. New per start.</summary>
    public string ApiToken { get; } = System.Security.Cryptography.RandomNumberGenerator.GetHexString(64, lowercase: true);

    /// <summary>The report open on <c>/admin/review</c>; kept here so it survives navigating to another page and back.</summary>
    public ReviewBundle? ReviewBundle { get; set; }
    /// <summary>Where reports and reply files are written for the user to send on; tests point it at a temp folder.</summary>
    public string OutboxFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    /// <summary>Opens Explorer on <paramref name="file"/> so it can be dragged into a mail or chat. Failure is not fatal — the path is shown.</summary>
    public static void ShowInFolder(string file)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{file}\"")?.Dispose();
        }
        catch (Exception)
        {
            // The page shows the path.
        }
    }

    private Dictionary<string, Skill> _skills = new();
    private Dictionary<string, Module> _taughtIn = new();

    /// <summary>Re-reads settings and the course from disk. Safe to call while circuits are rendering.</summary>
    public async Task LoadAsync()
    {
        Settings = Settings.Load();
        CoursePath = CoursePaths.Resolve(courseArgument, Environment.GetEnvironmentVariable(CoursePaths.EnvironmentVariable),
            Settings.CoursePath, Environment.CurrentDirectory, Directory.Exists);
        if (CoursePath is null || !Directory.Exists(CoursePath))
        {
            Course = null;
            Findings = [];
            return;
        }
        var report = await CourseValidator.ValidateAsync(CoursePath, new ValidationOptions(RunStarterCheck: false));
        var skills = report.Course.Skills.Skills.ToDictionary(s => s.Id, StringComparer.Ordinal);
        foreach (var skill in report.Course.Skills.Skills)
            foreach (var alias in skill.Aliases)
                skills.TryAdd(alias, skill);
        var taughtIn = new Dictionary<string, Module>(StringComparer.Ordinal);
        foreach (var module in report.Course.Modules)
            foreach (var skill in module.Exercises.SelectMany(e => e.Manifest.Teaches))
                taughtIn.TryAdd(skill, module);
        _skills = skills;
        _taughtIn = taughtIn;
        var book = CoursePaths.BookPdf(Settings.BookPdfPath, CoursePath, File.Exists);
        BookPdfPath = File.Exists(book) ? book : null;
        Findings = report.Findings;
        Summary = report.Summary;
        Course = report.Course;
    }

    /// <summary>
    /// Resolves each IDE and writes the result to settings.json, so the next start only checks that the cached
    /// executables still exist. <paramref name="force"/> drops the cache first ("neu erkennen").
    /// </summary>
    public void DetectIdes(bool force = false)
    {
        var settings = Settings.Load();
        if (force)
            settings.RiderPath = settings.VsCodePath = settings.VisualStudioPath = null;
        settings.RiderPath = IdeResolver.Resolve(IdeKind.Rider, settings.RiderPath);
        settings.VsCodePath = IdeResolver.Resolve(IdeKind.VsCode, settings.VsCodePath);
        settings.VisualStudioPath = IdeResolver.Resolve(IdeKind.VisualStudio, settings.VisualStudioPath);
        settings.Save();
        Settings = settings;
        var found = new Dictionary<IdeKind, string>();
        if (settings.RiderPath is { } rider) found[IdeKind.Rider] = rider;
        if (settings.VsCodePath is { } code) found[IdeKind.VsCode] = code;
        if (settings.VisualStudioPath is { } devenv) found[IdeKind.VisualStudio] = devenv;
        Ides = found;
    }

    public CourseProgress Progress()
    {
        var progress = Store.Query(Course!);
        return Review ? Unlock(progress) : progress;
    }

    /// <summary>Review mode: no exercise misses a skill, so every exercise and module counts as unlocked.</summary>
    public static CourseProgress Unlock(CourseProgress progress) => progress with
    {
        Modules = progress.Modules.Select(m => m with
        {
            Exercises = m.Exercises.Select(e => e with { MissingSkills = [] }).ToList(),
        }).ToList(),
    };

    public string SkillLabel(string id) =>
        _skills.TryGetValue(id, out var skill) && Localized.Text(skill.Label, Locale.Current) is { Length: > 0 } label ? label : id;

    /// <summary>Persists the UI language and switches the process to it; callers reload the page so a fresh circuit renders in it.</summary>
    public void SetLocale(string locale)
    {
        var settings = Settings.Load();
        settings.Locale = locale;
        settings.Save();
        Settings = settings;
        Locale.Apply(locale);
    }

    /// <summary>The first module in published order with an exercise that teaches the skill.</summary>
    public Module? TaughtIn(string skillId) => _taughtIn.GetValueOrDefault(skillId);

    public Module? FindModule(string id) => Course?.Modules.FirstOrDefault(m => m.Manifest.Id == id);
    public Exercise? FindExercise(string id) => Course?.ExercisesInOrder.FirstOrDefault(e => e.Manifest.Id == id);
    public Module ModuleOf(Exercise exercise) => Course!.Modules.First(m => m.Exercises.Contains(exercise));

    /// <summary>A locale-keyed field in the current language, falling back to German silently (titles carry no note; the page's file does).</summary>
    public static string Text(IDictionary<string, string>? text) => Localized.Text(text, Locale.Current);

    /// <summary>Deep link into the served PDF (browser viewers honour <c>#page=</c>), or null when the book is missing.</summary>
    public string? BookHref(Reading reading) => BookHref(BookPdfPath, reading);

    public static string? BookHref(string? bookPdfPath, Reading reading) =>
        bookPdfPath is null ? null : $"book.pdf#page={reading.PhysicalPage}";

    /// <summary>Course Markdown is trusted content from the trainee's own checkout, so it is rendered unsanitised.</summary>
    public static MarkupString RenderMarkdown(string path) => MarkdownText(File.Exists(path) ? File.ReadAllText(path) : "");

    public static MarkupString MarkdownText(string markdown) => new(Markdig.Markdown.ToHtml(markdown, Markdown));

    /// <summary>One-line Markdown (quiz options: `code`, **bold**) without the surrounding paragraph.</summary>
    public static MarkupString MarkdownInline(string markdown)
    {
        var html = Markdig.Markdown.ToHtml(markdown, Markdown).Trim();
        return new(html.StartsWith("<p>") && html.EndsWith("</p>") && html.IndexOf("<p>", 1, StringComparison.Ordinal) < 0
            ? html[3..^4] : html);
    }

    public void Dispose() => Store.Dispose();
}
