namespace SeeSharp.Content;

// On-disk shapes (deserialised 1:1 from YAML; property names are camelCase keys in the files).
// Schema decisions are documented in docs/CONTENT-FORMAT.md. Keep new fields optional so old manifests keep loading.

public sealed class SkillCatalog
{
    public List<Skill> Skills { get; set; } = [];
}

public sealed class Skill
{
    public string Id { get; set; } = "";
    public string Category { get; set; } = "";
    public Dictionary<string, string> Label { get; set; } = new();
    public string? Supersedes { get; set; }
    public List<string> Aliases { get; set; } = [];
}

public sealed class ModuleManifest
{
    public string Id { get; set; } = "";
    public Dictionary<string, string> Title { get; set; } = new();
    public Reading? Reading { get; set; }
}

public sealed class ExerciseManifest
{
    public string Id { get; set; } = "";
    public Dictionary<string, string> Title { get; set; } = new();
    public Reading? Reading { get; set; }
    public List<string> Teaches { get; set; } = [];
    public List<string> Requires { get; set; } = [];
    public List<ExerciseItem> Items { get; set; } = [];
}

/// <summary>A pointer into the bundled book. Authors write the printed page; the physical page is derived.</summary>
public sealed class Reading
{
    /// <summary>
    /// Printed page = physical PDF page - 32 for the C# 14 edition (chapter 1 starts on printed page 1 = physical 33).
    /// A new edition of the book changes this constant and nothing else.
    /// </summary>
    public const int PrintedToPhysicalOffset = 32;

    public string Section { get; set; } = "";
    public Dictionary<string, string> Title { get; set; } = new();
    public int PrintedPage { get; set; }

    public int PhysicalPage => PrintedPage + PrintedToPhysicalOffset;
}

/// <summary>Base of every item; <c>type</c> picks the subclass. New item types are new subclasses plus a line in <see cref="ItemTypes"/>.</summary>
public abstract class ExerciseItem
{
    public string Type { get; set; } = "";
    public string Id { get; set; } = "";
}

public sealed class MultipleChoiceItem : ExerciseItem
{
    public Dictionary<string, string> Prompt { get; set; } = new();
    public List<Dictionary<string, string>> Options { get; set; } = [];
    public List<int> Correct { get; set; } = [];
    public Dictionary<string, string>? Explanation { get; set; }
}

/// <summary>Free-text answer; gates on <em>answered</em>, never graded automatically.</summary>
public sealed class OpenItem : ExerciseItem
{
    public Dictionary<string, string> Prompt { get; set; } = new();
}

public sealed class CodingItem : ExerciseItem
{
    /// <summary>File to open in the IDE, relative to <c>starter/</c>.</summary>
    public string OpenFile { get; set; } = "";
    public int? OpenLine { get; set; }
    /// <summary>The test project, relative to <c>tests/</c>.</summary>
    public string TestProject { get; set; } = "";
    /// <summary>Test names that must pass; null or empty means all of them.</summary>
    public List<string>? RequiredTests { get; set; }
}

/// <summary>Graded by the supervisor; the trainee marks it done.</summary>
public sealed class ReviewItem : ExerciseItem
{
    public Dictionary<string, string> Prompt { get; set; } = new();
    public string? OpenFile { get; set; }
}

public static class ItemTypes
{
    public static readonly IReadOnlyDictionary<string, Type> ByName = new Dictionary<string, Type>
    {
        ["multipleChoice"] = typeof(MultipleChoiceItem),
        ["open"] = typeof(OpenItem),
        ["coding"] = typeof(CodingItem),
        ["review"] = typeof(ReviewItem),
    };
}

// Loaded, path-aware view of a course.

public sealed record Course(string Root, SkillCatalog Skills, IReadOnlyList<Module> Modules)
{
    public IEnumerable<Exercise> ExercisesInOrder => Modules.SelectMany(m => m.Exercises);
}

/// <param name="Directory">Absolute path of the module folder.</param>
/// <param name="Order">The numeric folder prefix; also the level number.</param>
public sealed record Module(string Directory, int Order, ModuleManifest Manifest, IReadOnlyList<Exercise> Exercises)
{
    public string SummaryPath => Localized.FilePath(Directory, "summary", CourseLoader.DefaultLocale);
    /// <summary><c>summary.&lt;locale&gt;.md</c>, or the default locale's file when that is missing.</summary>
    public LocalizedFile Summary(string locale) => Localized.File(Directory, "summary", locale);
}

/// <param name="Directory">Absolute path of the exercise folder.</param>
public sealed record Exercise(string Directory, int Order, ExerciseManifest Manifest)
{
    public string StarterDirectory => Path.Combine(Directory, "starter");
    public string TestsDirectory => Path.Combine(Directory, "tests");
    public string InstructionsPath => Localized.FilePath(Directory, "instructions", CourseLoader.DefaultLocale);
    /// <summary><c>instructions.&lt;locale&gt;.md</c>, or the default locale's file when that is missing.</summary>
    public LocalizedFile Instructions(string locale) => Localized.File(Directory, "instructions", locale);
    public string RubricPath => Path.Combine(Directory, "rubric.md");
}

/// <param name="FellBack">True when the requested locale had no file and <paramref name="Path"/> is the default locale's.</param>
public sealed record LocalizedFile(string Path, bool FellBack);

/// <summary>Locale lookup with explicit fallback to <see cref="CourseLoader.DefaultLocale"/>; the UI shows a note when it fell back.</summary>
public static class Localized
{
    public static string FilePath(string directory, string baseName, string locale) =>
        Path.Combine(directory, $"{baseName}.{locale}.md");

    public static LocalizedFile File(string directory, string baseName, string locale)
    {
        var path = FilePath(directory, baseName, locale);
        return locale == CourseLoader.DefaultLocale || System.IO.File.Exists(path)
            ? new LocalizedFile(path, false)
            : new LocalizedFile(FilePath(directory, baseName, CourseLoader.DefaultLocale), true);
    }

    /// <summary>The text in <paramref name="locale"/>, else in the default locale, else empty.</summary>
    public static string Text(IDictionary<string, string>? text, string locale) =>
        text is null ? ""
        : text.TryGetValue(locale, out var value) && !string.IsNullOrWhiteSpace(value) ? value
        : text.TryGetValue(CourseLoader.DefaultLocale, out var fallback) ? fallback : "";
}
