using Markdig;
using Microsoft.AspNetCore.Components;
using SeeSharp.Content;
using SeeSharp.Progress;

namespace SeeSharp.App;

/// <summary>
/// The one loaded course plus the progress store, shared by every circuit. Reloading swaps the
/// course reference; pages read it fresh on every render.
/// </summary>
public sealed class CourseState(ProgressStore store, string? courseArgument) : IDisposable
{
    private static readonly MarkdownPipeline Markdown = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public ProgressStore Store { get; } = store;
    public Settings Settings { get; private set; } = Settings.Load();
    public string? CoursePath { get; private set; }
    public Course? Course { get; private set; }
    public IReadOnlyList<Finding> Findings { get; private set; } = [];
    /// <summary>Absolute path of the book, or null when the file is not there (links fall back to text).</summary>
    public string? BookPdfPath { get; private set; }

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
        var book = CoursePaths.BookPdf(Settings.BookPdfPath, CoursePath);
        BookPdfPath = File.Exists(book) ? book : null;
        Findings = report.Findings;
        Course = report.Course;
    }

    public CourseProgress Progress() => Store.Query(Course!);

    public string SkillLabel(string id) =>
        _skills.TryGetValue(id, out var skill) && skill.Label.TryGetValue(CourseLoader.DefaultLocale, out var label) ? label : id;

    /// <summary>The first module in published order with an exercise that teaches the skill.</summary>
    public Module? TaughtIn(string skillId) => _taughtIn.GetValueOrDefault(skillId);

    public Module? FindModule(string id) => Course?.Modules.FirstOrDefault(m => m.Manifest.Id == id);
    public Exercise? FindExercise(string id) => Course?.ExercisesInOrder.FirstOrDefault(e => e.Manifest.Id == id);
    public Module ModuleOf(Exercise exercise) => Course!.Modules.First(m => m.Exercises.Contains(exercise));

    public static string Text(IDictionary<string, string>? text) =>
        text is not null && text.TryGetValue(CourseLoader.DefaultLocale, out var value) ? value : "";

    /// <summary>Deep link into the served PDF (browser viewers honour <c>#page=</c>), or null when the book is missing.</summary>
    public string? BookHref(Reading reading) => BookHref(BookPdfPath, reading);

    public static string? BookHref(string? bookPdfPath, Reading reading) =>
        bookPdfPath is null ? null : $"book.pdf#page={reading.PhysicalPage}";

    /// <summary>Course Markdown is trusted content from the trainee's own checkout, so it is rendered unsanitised.</summary>
    public static MarkupString RenderMarkdown(string path) =>
        new(File.Exists(path) ? Markdig.Markdown.ToHtml(File.ReadAllText(path), Markdown) : "");

    public void Dispose() => Store.Dispose();
}
