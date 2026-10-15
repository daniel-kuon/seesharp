using System.Resources;

namespace SeeSharp.Cli;

internal static class Strings
{
    private static readonly ResourceManager Resources = new("SeeSharp.Cli.Strings", typeof(Strings).Assembly);

    public static string Usage => Resources.GetString(nameof(Usage))!;
}
