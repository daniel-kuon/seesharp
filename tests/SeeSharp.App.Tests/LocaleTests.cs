using System.Collections;
using System.Globalization;
using System.Resources;

namespace SeeSharp.App.Tests;

public sealed class LocaleTests
{
    [Theory]
    [InlineData(null, null, "de")]
    [InlineData(null, "en", "en")]
    [InlineData("en", null, "en")]
    [InlineData("de", "en", "de")]
    [InlineData("fr", " EN ", "en")]
    [InlineData("xx", "", "de")]
    public void Environment_wins_over_the_setting_and_unknown_values_mean_german(string? environment, string? setting, string expected) =>
        Assert.Equal(expected, Locale.Resolve(environment, setting));

    [Fact]
    public void Locale_round_trips_through_the_settings_file()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("seesharp-settings-").FullName, "settings.json");
        new Settings { Locale = "en" }.Save(path);
        Assert.Equal("en", Settings.Load(path).Locale);
        Assert.Contains("\"locale\": \"en\"", File.ReadAllText(path));
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Fact]
    public void English_resources_cover_every_german_key()
    {
        var resources = new ResourceManager("SeeSharp.App.Strings", typeof(AppHost).Assembly);
        Assert.Equal(Keys(resources, CultureInfo.InvariantCulture), Keys(resources, new CultureInfo("en")));
    }

    internal static IEnumerable<string> Keys(ResourceManager resources, CultureInfo culture) =>
        resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!.Cast<DictionaryEntry>().Select(e => (string)e.Key).Order();
}
