using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SeeSharp.Content;

/// <summary>
/// Reads a course folder into <see cref="Course"/>. Structural problems (missing files, unparseable YAML,
/// misnamed folders) become findings; the affected module or exercise is skipped and loading continues.
/// Semantic rules live in <see cref="CourseValidator"/>.
/// </summary>
public static class CourseLoader
{
    public const string DefaultLocale = "de";
    public const string SkillsFileName = "skills.yaml";
    public const string ModulesFolderName = "modules";
    public const string ModuleManifestFileName = "module.yaml";
    public const string ExerciseManifestFileName = "manifest.yaml";
    public const string ExamsFolderName = "exams";

    /// <summary>Published order comes from the folder prefix: <c>04-klassen-und-objekte</c>, <c>01-konstruktoren</c>.</summary>
    public static readonly Regex FolderName = new(@"^(\d{2})-[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);

    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeDiscriminatingNodeDeserializer(o =>
            o.AddKeyValueTypeDiscriminator<ExerciseItem>("type", ItemTypes.ByName.ToDictionary(p => p.Key, p => p.Value)))
        .Build();

    public static Course Load(string root, List<Finding> findings)
    {
        root = Path.GetFullPath(root);
        var skills = Parse<SkillCatalog>(root, Path.Combine(root, SkillsFileName), findings,
            FindingCode.SkillsFileMissing, FindingCode.SkillsFileInvalid) ?? new SkillCatalog();

        var modulesDir = Path.Combine(root, ModulesFolderName);
        if (!Directory.Exists(modulesDir))
        {
            findings.Add(new Finding(Severity.Error, FindingCode.ModulesFolderMissing, ModulesFolderName, Strings.Format(FindingCode.ModulesFolderMissing)));
            return new Course(root, skills, []);
        }

        var modules = new List<Module>();
        foreach (var dir in Directory.GetDirectories(modulesDir).OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(dir);
            var match = FolderName.Match(name);
            if (!match.Success)
            {
                findings.Add(new Finding(Severity.Error, FindingCode.ModuleFolderNameInvalid, Relative(root, dir), Strings.Format(FindingCode.ModuleFolderNameInvalid, name)));
                continue;
            }
            var manifest = Parse<ModuleManifest>(root, Path.Combine(dir, ModuleManifestFileName), findings,
                FindingCode.ModuleManifestMissing, FindingCode.ModuleManifestInvalid);
            if (manifest is null)
                continue;
            modules.Add(new Module(dir, int.Parse(match.Groups[1].Value), manifest, LoadExercises(root, dir, findings)));
        }
        return new Course(root, skills, modules) { Exams = LoadExams(root, findings) };
    }

    /// <summary><c>exams/*.yaml</c>, optional; an unreadable file is a finding and skipped.</summary>
    private static List<ExamDefinition> LoadExams(string root, List<Finding> findings)
    {
        var dir = Path.Combine(root, ExamsFolderName);
        if (!Directory.Exists(dir))
            return [];
        return Directory.GetFiles(dir, "*.yaml").OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(file => Parse<ExamDefinition>(root, file, findings, FindingCode.ExamManifestMissing, FindingCode.ExamManifestInvalid))
            .OfType<ExamDefinition>()
            .ToList();
    }

    private static List<Exercise> LoadExercises(string root, string moduleDir, List<Finding> findings)
    {
        var exercises = new List<Exercise>();
        foreach (var dir in Directory.GetDirectories(moduleDir).OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(dir);
            var match = FolderName.Match(name);
            if (!match.Success)
            {
                findings.Add(new Finding(Severity.Error, FindingCode.ExerciseFolderNameInvalid, Relative(root, dir), Strings.Format(FindingCode.ExerciseFolderNameInvalid, name)));
                continue;
            }
            var manifest = Parse<ExerciseManifest>(root, Path.Combine(dir, ExerciseManifestFileName), findings,
                FindingCode.ExerciseManifestMissing, FindingCode.ExerciseManifestInvalid);
            if (manifest is null)
                continue;
            exercises.Add(new Exercise(dir, int.Parse(match.Groups[1].Value), manifest));
        }
        return exercises;
    }

    private static T? Parse<T>(string root, string path, List<Finding> findings, FindingCode missing, FindingCode invalid) where T : class
    {
        var location = Relative(root, path);
        if (!File.Exists(path))
        {
            findings.Add(new Finding(Severity.Error, missing, location, Strings.Format(missing)));
            return null;
        }
        try
        {
            return Deserialize<T>(File.ReadAllText(path));
        }
        catch (YamlException e)
        {
            // The outer message carries the position, the inner one the reason (unknown property, bad type, ...).
            var reason = e.InnerException is { } inner ? $"{e.Message} {inner.Message}" : e.Message;
            findings.Add(new Finding(Severity.Error, invalid, location, Strings.Format(invalid, reason)));
            return null;
        }
    }

    /// <summary>One document of the content format; throws <see cref="YamlException"/> on anything the loader would report as invalid.</summary>
    public static T Deserialize<T>(string yaml) where T : class =>
        Yaml.Deserialize<T>(yaml) ?? throw new YamlException("empty document");

    /// <summary>Course-relative path with forward slashes, so findings read the same on every OS.</summary>
    public static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
