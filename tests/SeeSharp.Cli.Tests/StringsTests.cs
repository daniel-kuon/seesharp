using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace SeeSharp.Cli.Tests;

public sealed class StringsTests
{
    [Fact]
    public void English_resources_cover_every_german_key()
    {
        var resources = new ResourceManager("SeeSharp.Cli.Strings", Assembly.Load("SeeSharp.Cli"));
        Assert.Equal(Keys(resources, CultureInfo.InvariantCulture), Keys(resources, new CultureInfo("en")));
    }

    private static IEnumerable<string> Keys(ResourceManager resources, CultureInfo culture) =>
        resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!.Cast<DictionaryEntry>().Select(e => (string)e.Key).Order();
}
