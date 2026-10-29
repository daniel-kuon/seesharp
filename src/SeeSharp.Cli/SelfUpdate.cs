using System.Diagnostics;
using SeeSharp.App;

namespace SeeSharp.Cli;

/// <summary>
/// Update on start: <c>dotnet tool update</c> cannot replace the files of a running tool, so with a newer release on nuget.org
/// a PowerShell window waits for this process to end, updates, and starts <c>seesharp start … --no-update</c> again, headless
/// like the shortcut. The flag keeps a failed update (offline, files held by a second instance) from looping: the old version
/// then simply starts.
/// </summary>
internal static class SelfUpdate
{
    public const string Flag = "--no-update";

    /// <summary>True when the update was handed over and this process should exit now.</summary>
    public static async Task<bool> TryHandOverAsync(string[] startArgs)
    {
        if (!OperatingSystem.IsWindows())
            return false;
        string? newer;
        try
        {
            newer = await UpdateCheck.NewerAsync().WaitAsync(TimeSpan.FromSeconds(5)); // never hold up the start for long
        }
        catch (TimeoutException)
        {
            return false;
        }
        if (newer is null)
            return false;

        var message = string.Format(Strings.Updating, newer);
        Console.WriteLine(message);
        try
        {
            // A visible window on purpose: updating takes a few seconds, and a click on the icon should not seem to do nothing.
            Process.Start(new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {Launcher.Encode(Script(message, Environment.ProcessId, Launcher.Tool, startArgs))}")
                { UseShellExecute = true })?.Dispose();
            return true;
        }
        catch (Exception) // no PowerShell: start this version, the banner still offers the command
        {
            return false;
        }
    }

    /// <summary>The PowerShell the hand-over runs.</summary>
    internal static string Script(string message, int processId, string tool, IEnumerable<string> startArgs) =>
        $"Write-Host {Launcher.Quote(message)}; " +
        $"Wait-Process -Id {processId} -ErrorAction SilentlyContinue; " +
        "dotnet tool update -g SeeSharp.Cli; " +
        $"Start-Process -FilePath {Launcher.Quote(Launcher.Conhost)} " +
        $"-ArgumentList {Launcher.Quote(Launcher.HeadlessArguments(tool, new[] { "start" }.Concat(startArgs).Append(Flag)))}";
}
