using System.Diagnostics;
using System.Text;
using SeeSharp.App;

namespace SeeSharp.Cli;

/// <summary>
/// Starting SeeSharp without a console: the tool is a console program, so the shortcuts run it under
/// <c>conhost.exe --headless</c>, which gives it a console that never shows (not even as a Windows Terminal tab).
/// </summary>
internal static class Launcher
{
    public static string Conhost => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "conhost.exe");

    /// <summary>The installed <c>seesharp.exe</c>: this process when it is the tool, else where <c>dotnet tool install -g</c> puts it.</summary>
    public static string Tool => Environment.ProcessPath is { } path && Path.GetFileNameWithoutExtension(path).Equals("seesharp", StringComparison.OrdinalIgnoreCase)
        ? path
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools", "seesharp.exe");

    /// <summary>Conhost's command line for <c>seesharp</c> with <paramref name="args"/>; each argument quoted, so paths with spaces survive.</summary>
    public static string HeadlessArguments(string tool, IEnumerable<string> args) =>
        string.Join(" ", new[] { "--headless", tool }.Concat(args).Select(a => a.StartsWith("--", StringComparison.Ordinal) ? a : $"\"{a}\""));

    /// <summary>
    /// The Start menu entry (and with <paramref name="desktop"/> a desktop one), pointing at the installed tool. A window start only
    /// adds a missing Start menu entry; <c>init</c> writes both, so a desktop icon someone deleted stays deleted.
    /// </summary>
    public static void EnsureShortcuts(bool desktop)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(Tool)) // a checkout without the installed tool has nothing to point at
            return;
        var links = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "SeeSharp.lnk") };
        if (desktop)
            links.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SeeSharp.lnk"));
        else if (File.Exists(links[0]))
            return;
        var script = new StringBuilder("$shell = New-Object -ComObject WScript.Shell; ");
        foreach (var link in links)
            script.Append($"$l = $shell.CreateShortcut({Quote(link)}); $l.TargetPath = {Quote(Conhost)}; " +
                          $"$l.Arguments = {Quote(HeadlessArguments(Tool, []))}; $l.IconLocation = {Quote(AppWindow.IconFile())}; " +
                          $"$l.Description = 'SeeSharp'; $l.Save(); ");
        try
        {
            using var process = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -EncodedCommand {Encode(script.ToString())}")
                { UseShellExecute = false, CreateNoWindow = true });
            process?.WaitForExit(15_000);
        }
        catch (Exception)
        {
            // No shortcut is no reason not to start; `seesharp` in a terminal still works.
        }
    }

    /// <summary>A PowerShell single-quoted string: nothing inside is expanded, a quote is doubled.</summary>
    public static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

    /// <summary>For <c>-EncodedCommand</c>: Base64 of UTF-16, so no quoting rules of the Windows command line apply.</summary>
    public static string Encode(string script) => Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
}
