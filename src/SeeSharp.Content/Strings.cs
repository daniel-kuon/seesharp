using System.Resources;

namespace SeeSharp.Content;

internal static class Strings
{
    private static readonly ResourceManager Resources = new("SeeSharp.Content.Strings", typeof(Strings).Assembly);

    public static string Format(FindingCode code, params object[] args) =>
        string.Format(Resources.GetString(code.ToString()) ?? code.ToString(), args);
}
