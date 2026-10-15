using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SeeSharp.Content;

/// <summary>
/// File operations of the admin editor (task 7) over a course working copy. Every method works on the
/// folder tree directly and caches nothing; the caller reloads the course afterwards. Published order
/// is the <c>NN-</c> folder prefix, so reordering renames folders.
/// </summary>
public static class CourseEditor
{
    private static readonly ISerializer Yaml = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public static string Serialize(ModuleManifest manifest) => Yaml.Serialize(manifest);

    public static string Serialize(ExerciseManifest manifest)
    {
        // The discriminator key is the Type property; make sure it names the item's class whatever the editor did to it.
        foreach (var item in manifest.Items)
            item.Type = ItemTypes.ByName.First(p => p.Value == item.GetType()).Key;
        return Yaml.Serialize(manifest);
    }

    public static void SaveExercise(Exercise exercise, ExerciseManifest manifest) =>
        File.WriteAllText(Path.Combine(exercise.Directory, CourseLoader.ExerciseManifestFileName), Serialize(manifest));

    /// <summary>Parses first so a broken file is never written; returns the parse error, or null when saved.</summary>
    public static string? SaveSkills(string root, string yaml)
    {
        try
        {
            CourseLoader.Deserialize<SkillCatalog>(yaml);
        }
        catch (YamlException e)
        {
            return e.InnerException is { } inner ? $"{e.Message} {inner.Message}" : e.Message;
        }
        File.WriteAllText(Path.Combine(root, CourseLoader.SkillsFileName), yaml);
        return null;
    }

    /// <summary>New module after the last one (its prefix is the level number). Returns the folder.</summary>
    public static string CreateModule(string root, string slug, string id, string title)
    {
        var modules = Path.Combine(root, CourseLoader.ModulesFolderName);
        Directory.CreateDirectory(modules);
        var dir = NewFolder(modules, slug, first: 0);
        File.WriteAllText(Path.Combine(dir, CourseLoader.ModuleManifestFileName),
            Serialize(new ModuleManifest { Id = id, Title = { ["de"] = title } }));
        File.WriteAllText(Path.Combine(dir, $"summary.{CourseLoader.DefaultLocale}.md"), $"# {title}\n");
        return dir;
    }

    /// <summary>
    /// New exercise after the module's last one: manifest, instructions, rubric and a starter/tests pair with one
    /// coding item whose starter throws, so the folder passes <c>seesharp validate</c> including the starter check.
    /// </summary>
    public static string CreateExercise(string moduleDirectory, string slug, string id, string title, IEnumerable<string> teaches, IEnumerable<string> requires)
    {
        var dir = NewFolder(moduleDirectory, slug, first: 1);
        var project = ProjectName(slug);
        var manifest = new ExerciseManifest
        {
            Id = id,
            Title = { ["de"] = title },
            Teaches = teaches.ToList(),
            Requires = requires.ToList(),
            Items = [new CodingItem { Id = "aufgabe", OpenFile = $"{project}/{project}.cs", OpenLine = 9, TestProject = $"{project}.Tests/{project}.Tests.csproj" }],
        };
        File.WriteAllText(Path.Combine(dir, CourseLoader.ExerciseManifestFileName), Serialize(manifest));
        File.WriteAllText(Path.Combine(dir, $"instructions.{CourseLoader.DefaultLocale}.md"), $"# {title}\n\nTODO: Aufgabenstellung.\n");
        File.WriteAllText(Path.Combine(dir, "rubric.md"), $"# Rubrik — {title} (nur für die Betreuung)\n\nTODO: Worauf achten?\n");

        var starter = Path.Combine(dir, "starter", project);
        Directory.CreateDirectory(starter);
        File.WriteAllText(Path.Combine(starter, $"{project}.csproj"), StarterProject);
        File.WriteAllText(Path.Combine(starter, $"{project}.cs"), StarterCode.Replace("PROJECT", project));
        var tests = Path.Combine(dir, "tests", $"{project}.Tests");
        Directory.CreateDirectory(tests);
        File.WriteAllText(Path.Combine(tests, $"{project}.Tests.csproj"), TestProject.Replace("PROJECT", project));
        File.WriteAllText(Path.Combine(tests, $"{project}Tests.cs"), TestCode.Replace("PROJECT", project));
        return dir;
    }

    /// <summary>Swaps the <c>NN-</c> prefixes of two sibling folders (module or exercise), which swaps their published order.</summary>
    public static void Swap(string directoryA, string directoryB)
    {
        var parent = Path.GetDirectoryName(directoryA)!;
        var a = Path.GetFileName(directoryA);
        var b = Path.GetFileName(directoryB);
        var temp = directoryA + ".swap";
        Move(directoryA, temp);
        Move(directoryB, Path.Combine(parent, a[..3] + b[3..]));
        Move(temp, Path.Combine(parent, b[..3] + a[3..]));
    }

    /// <summary>On Windows a folder the indexer or virus scanner is still looking at refuses to move for a moment; try again briefly.</summary>
    private static void Move(string from, string to)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Move(from, to);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 10)
            {
                Thread.Sleep(100 * attempt);
            }
        }
    }

    private static string NewFolder(string parent, string slug, int first)
    {
        if (!CourseLoader.FolderName.IsMatch($"00-{slug}"))
            throw new ArgumentException(slug, nameof(slug));
        var existing = Directory.GetDirectories(parent).Select(Path.GetFileName)
            .Where(n => CourseLoader.FolderName.IsMatch(n!)).Select(n => int.Parse(n![..2])).ToList();
        var order = existing.Count == 0 ? first : existing.Max() + 1;
        var dir = Path.Combine(parent, $"{order:00}-{slug}");
        if (Directory.Exists(dir))
            throw new IOException(dir);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary><c>hallo-welt</c> → <c>HalloWelt</c>: a C# identifier usable as project, class and folder name.</summary>
    public static string ProjectName(string slug)
    {
        var sb = new StringBuilder();
        foreach (var part in slug.Split('-', StringSplitOptions.RemoveEmptyEntries))
            sb.Append(char.ToUpperInvariant(part[0])).Append(part, 1, part.Length - 1);
        if (sb.Length == 0 || char.IsDigit(sb[0]))
            sb.Insert(0, 'X');
        return sb.ToString();
    }

    private const string StarterProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
        </Project>

        """;

    private const string StarterCode = """
        public class PROJECT
        {
            static void Main()
            {
                Console.WriteLine(Ergebnis());
            }

            // TODO: Aufgabe hier lösen.
            public static string Ergebnis()
            {
                throw new NotImplementedException();
            }
        }

        """;

    private const string TestProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
            <PackageReference Include="xunit" Version="2.9.3" />
            <PackageReference Include="xunit.runner.visualstudio" Version="3.1.3" />
            <Using Include="Xunit" />
            <ProjectReference Include="../PROJECT/PROJECT.csproj" />
          </ItemGroup>
        </Project>

        """;

    private const string TestCode = """
        public class PROJECTTests
        {
            [Fact]
            public void Ergebnis_ist_korrekt()
            {
                Assert.Equal("TODO", PROJECT.Ergebnis());
            }
        }

        """;
}
