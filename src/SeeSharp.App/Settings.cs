using System.Globalization;
using System.Text.Json;
using SeeSharp.Content;

namespace SeeSharp.App;

/// <summary><c>%LOCALAPPDATA%\SeeSharp\settings.json</c> (or <c>SEESHARP_SETTINGS</c>). Missing or unreadable file = defaults.</summary>
public sealed class Settings
{
    public const string EnvironmentVariable = "SEESHARP_SETTINGS";

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeeSharp", "settings.json");

    /// <summary>The settings file Load/Save use: <c>SEESHARP_SETTINGS</c> when set (tests, scratch runs), else <see cref="DefaultPath"/>.</summary>
    public static string FilePath =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } path ? Path.GetFullPath(path) : DefaultPath;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string? CoursePath { get; set; }
    /// <summary>The book; null means <c>book/csharp14.pdf</c> in the course folder (what <c>seesharp init</c> downloads), else <c>../csharp14.pdf</c> next to it.</summary>
    public string? BookPdfPath { get; set; }
    /// <summary>Resolved IDE executables, cached by <see cref="CourseState.DetectIdes"/>; null = not found.</summary>
    public string? RiderPath { get; set; }
    public string? VsCodePath { get; set; }
    public string? VisualStudioPath { get; set; }
    /// <summary>UI language (<c>de</c> or <c>en</c>); null = German. <c>SEESHARP_LOCALE</c> overrides it at start.</summary>
    public string? Locale { get; set; }
    /// <summary>Admin mode: credentials for push/pull over HTTPS (a personal access token as the password). Unset = anonymous.</summary>
    public string? GitUserName { get; set; }
    public string? GitToken { get; set; }
    /// <summary>AI tutor (task 9): <c>off</c> (null), <c>ollama</c>, <c>openai</c>, <c>openai-compatible</c> or <c>anthropic</c>; see <see cref="Tutor"/>.</summary>
    public string? TutorProvider { get; set; }
    /// <summary>Null = the provider's default (<see cref="Tutor.DefaultModel"/>).</summary>
    public string? TutorModel { get; set; }
    /// <summary>Only ever sent to the provider; never logged, stored in the progress DB or exported.</summary>
    public string? TutorApiKey { get; set; }
    /// <summary>Null = the provider's default (<see cref="Tutor.DefaultBaseUrl"/>); required for <c>openai-compatible</c>.</summary>
    public string? TutorBaseUrl { get; set; }

    public static Settings Load(string? path = null)
    {
        path ??= FilePath;
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
        path ??= FilePath;
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
    public const string BookFolder = "book";
    public const string BookFile = "csharp14.pdf";
    public const string LegacyBookFile = "../csharp14.pdf";

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

    /// <summary>The setting; else <c>&lt;course&gt;/book/csharp14.pdf</c> when it exists; else <c>../csharp14.pdf</c>.</summary>
    public static string BookPdf(string? setting, string coursePath, Func<string, bool> fileExists)
    {
        if (!string.IsNullOrWhiteSpace(setting))
            return Path.GetFullPath(setting);
        var inCourse = Path.GetFullPath(Path.Combine(BookFolder, BookFile), coursePath);
        return fileExists(inCourse) ? inCourse : Path.GetFullPath(LegacyBookFile, coursePath);
    }

    /// <summary>
    /// A translated book sits next to the original as <c>&lt;name&gt;.&lt;locale&gt;.pdf</c> (e.g. <c>csharp14.en.pdf</c>,
    /// merged by tools/book_pdf.py with the same physical pages); use it when it exists, else the original.
    /// </summary>
    public static string LocalizedBook(string pdf, string locale, Func<string, bool> fileExists)
    {
        var translated = Path.Combine(Path.GetDirectoryName(pdf)!, $"{Path.GetFileNameWithoutExtension(pdf)}.{locale}.pdf");
        return locale != CourseLoader.DefaultLocale && fileExists(translated) ? translated : pdf;
    }
}
