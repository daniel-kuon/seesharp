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
    public string SummaryPath => Path.Combine(Directory, "summary.de.md");
}

/// <param name="Directory">Absolute path of the exercise folder.</param>
public sealed record Exercise(string Directory, int Order, ExerciseManifest Manifest)
{
    public string StarterDirectory => Path.Combine(Directory, "starter");
    public string TestsDirectory => Path.Combine(Directory, "tests");
    public string InstructionsPath => Path.Combine(Directory, "instructions.de.md");
    public string RubricPath => Path.Combine(Directory, "rubric.md");
}
