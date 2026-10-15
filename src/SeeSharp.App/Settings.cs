using System.Globalization;
using System.Text.Json;
using SeeSharp.Content;

namespace SeeSharp.App;

/// <summary><c>%LOCALAPPDATA%\SeeSharp\settings.json</c>. Missing or unreadable file = defaults.</summary>
public sealed class Settings
{
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeeSharp", "settings.json");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string? CoursePath { get; set; }
    /// <summary>The bundled book; null means <c>../csharp14.pdf</c> next to the course folder.</summary>
    public string? BookPdfPath { get; set; }
    /// <summary>Resolved IDE executables, cached by <see cref="CourseState.DetectIdes"/>; null = not found.</summary>
    public string? RiderPath { get; set; }
    public string? VsCodePath { get; set; }
    public string? VisualStudioPath { get; set; }
    /// <summary>UI language (<c>de</c> or <c>en</c>); null = German. <c>SEESHARP_LOCALE</c> overrides it at start.</summary>
    public string? Locale { get; set; }

    public static Settings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), Json) ?? new() : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
    }
}

/// <summary>
/// UI language of the whole process: one trainee, one machine, so the culture is set process-wide
/// (<see cref="CultureInfo.DefaultThreadCurrentUICulture"/>) rather than per request or circuit.
/// </summary>
public static class Locale
{
    public const string EnvironmentVariable = "SEESHARP_LOCALE";
    public static readonly IReadOnlyList<string> Supported = ["de", "en"];

    /// <summary>First supported value of: <c>SEESHARP_LOCALE</c>, the <c>locale</c> setting; else German.</summary>
    public static string Resolve(string? environment, string? setting) =>
        new[] { environment, setting }.Select(v => v?.Trim().ToLowerInvariant())
            .FirstOrDefault(v => v is not null && Supported.Contains(v)) ?? CourseLoader.DefaultLocale;

    /// <summary>The locale content and strings are shown in right now.</summary>
    public static string Current =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is var language && Supported.Contains(language) ? language : CourseLoader.DefaultLocale;

    /// <summary>Only the default is set, never the calling thread's own culture: an explicit one would flow into every later request and pin it.</summary>
    public static void Apply(string locale) => CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(locale);
}

public static class CoursePaths
{
    public const string EnvironmentVariable = "SEESHARP_COURSE";
    public const string SiblingFolder = "../seesharp-course";
    public const string DefaultBookFile = "../csharp14.pdf";

    /// <summary>
    /// First of: <c>--course</c>, <c>SEESHARP_COURSE</c>, the setting, <c>../seesharp-course</c> relative to <paramref name="cwd"/> if it exists.
    /// Explicit sources win even when the folder is missing, so the UI can say which path is wrong.
    /// </summary>
    public static string? Resolve(string? argument, string? environment, string? setting, string cwd, Func<string, bool> directoryExists)
    {
        var explicitPath = new[] { argument, environment, setting }.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
        if (explicitPath is not null)
            return Path.GetFullPath(explicitPath, cwd);
        var sibling = Path.GetFullPath(SiblingFolder, cwd);
        return directoryExists(sibling) ? sibling : null;
    }

    public static string BookPdf(string? setting, string coursePath) =>
        string.IsNullOrWhiteSpace(setting) ? Path.GetFullPath(DefaultBookFile, coursePath) : Path.GetFullPath(setting);
}
