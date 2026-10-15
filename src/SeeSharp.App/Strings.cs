using System.Resources;

namespace SeeSharp.App;

internal static class Strings
{
    private static readonly ResourceManager Resources = new("SeeSharp.App.Strings", typeof(Strings).Assembly);

    public static string Title => Resources.GetString(nameof(Title))!;
    public static string Welcome => Resources.GetString(nameof(Welcome))!;
}
