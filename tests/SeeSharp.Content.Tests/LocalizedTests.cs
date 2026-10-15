using System.Collections;
using System.Globalization;
using System.Resources;

namespace SeeSharp.Content.Tests;

public sealed class LocalizedTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("seesharp-localized-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void File_falls_back_to_the_default_locale_and_says_so()
    {
        File.WriteAllText(Path.Combine(_dir, "instructions.de.md"), "# de");
        var exercise = new Exercise(_dir, 1, new ExerciseManifest());

        Assert.Equal((exercise.InstructionsPath, true), (exercise.Instructions("en").Path, exercise.Instructions("en").FellBack));
        Assert.Equal((exercise.InstructionsPath, false), (exercise.Instructions("de").Path, exercise.Instructions("de").FellBack));

        File.WriteAllText(Path.Combine(_dir, "instructions.en.md"), "# en");
        Assert.Equal((Path.Combine(_dir, "instructions.en.md"), false), (exercise.Instructions("en").Path, exercise.Instructions("en").FellBack));
    }

    [Fact]
    public void Default_locale_never_falls_back_even_when_its_file_is_missing()
    {
        var module = new Module(_dir, 1, new ModuleManifest(), []);
        Assert.Equal((module.SummaryPath, false), (module.Summary("de").Path, module.Summary("de").FellBack));
    }

    [Fact]
    public void Text_prefers_the_locale_then_the_default_then_empty()
    {
        var both = new Dictionary<string, string> { ["de"] = "Hallo", ["en"] = "Hello" };
        Assert.Equal("Hello", Localized.Text(both, "en"));
        Assert.Equal("Hallo", Localized.Text(both, "de"));
        Assert.Equal("Hallo", Localized.Text(new Dictionary<string, string> { ["de"] = "Hallo" }, "en"));
        Assert.Equal("Hallo", Localized.Text(new Dictionary<string, string> { ["de"] = "Hallo", ["en"] = " " }, "en"));
        Assert.Equal("", Localized.Text(new Dictionary<string, string> { ["en"] = "Hello" }, "fr"));
        Assert.Equal("", Localized.Text(null, "en"));
    }

    [Fact]
    public void English_resources_cover_every_finding_code()
    {
        var resources = new ResourceManager("SeeSharp.Content.Strings", typeof(CourseLoader).Assembly);
        var english = resources.GetResourceSet(new CultureInfo("en"), createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>().Select(e => (string)e.Key).Order();
        Assert.Equal(Enum.GetNames<FindingCode>().Order(), english);
    }
}
