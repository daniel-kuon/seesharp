using System.Reflection;
using System.Text.Json.Nodes;

namespace SeeSharp.App;

/// <summary>
/// Is a newer <c>SeeSharp.Cli</c> on nuget.org? Asked once per start (user decision 2026-10-27); the layout shows a banner then.
/// A plain version lookup, nothing about the trainee is sent. Offline or a dev build: no message.
/// </summary>
public static class UpdateCheck
{
    public const string Index = "https://api.nuget.org/v3-flatcontainer/seesharp.cli/index.json";
    private static Task<string?>? _newer;

    /// <summary>The running tool's version: the entry assembly's (SeeSharp.Cli; the release build sets it from the tag) without the <c>+commit</c> suffix.</summary>
    public static string? Current =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];

    public static Task<string?> NewerAsync() => _newer ??= NewerAsync(Tutor.Http, Current);

    /// <summary>The newest stable version on nuget.org when it is newer than <paramref name="current"/>; null otherwise.</summary>
    public static async Task<string?> NewerAsync(HttpClient http, string? current)
    {
        if (!Version.TryParse(current, out var mine))
            return null; // dev build (0.0.0-dev) or unknown
        try
        {
            var latest = JsonNode.Parse(await http.GetStringAsync(Index))!["versions"]!.AsArray()
                .Select(v => Version.TryParse((string?)v, out var parsed) ? parsed : null) // prerelease versions don't parse and are skipped
                .Max();
            return latest > mine ? latest.ToString() : null;
        }
        catch (Exception) // offline, timeout, odd JSON: no message
        {
            return null;
        }
    }
}
