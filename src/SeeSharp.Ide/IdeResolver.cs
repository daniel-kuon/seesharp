using System.Diagnostics;

namespace SeeSharp.Ide;

public enum IdeKind { Rider, VsCode, VisualStudio }

/// <summary>What the resolver asks the machine. Tests swap the delegates; production uses the defaults.</summary>
public sealed record IdeProbe
{
    public Func<string, string?> Env { get; init; } = Environment.GetEnvironmentVariable;
    public Func<string, bool> FileExists { get; init; } = File.Exists;
    /// <summary>Every file named <c>pattern</c> below <c>dir</c>, any depth; empty when the folder is missing.</summary>
    public Func<string, string, IEnumerable<string>> FindFiles { get; init; } = (dir, pattern) =>
        Directory.Exists(dir) ? Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories) : [];
    /// <summary>Full path of <c>name</c> (without extension) on PATH, honouring PATHEXT; null when absent.</summary>
    public Func<string, string?> OnPath { get; init; } = DefaultOnPath;
    /// <summary>devenv.exe as reported by vswhere, or null.</summary>
    public Func<string?> VsWhere { get; init; } = DefaultVsWhere;
    /// <summary>Which set of install locations to try; tests pin it so they run on any OS.</summary>
    public bool Windows { get; init; } = OperatingSystem.IsWindows();

    public static readonly IdeProbe Default = new();

    private static string? DefaultOnPath(string name)
    {
        var extensions = OperatingSystem.IsWindows() ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".exe;.cmd;.bat").Split(';') : [""];
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(dir, name + extension);
                if (File.Exists(candidate))
                    return candidate;
            }
        return null;
    }

    private static string? DefaultVsWhere()
    {
        var vswhere = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "", @"Microsoft Visual Studio\Installer\vswhere.exe");
        if (!File.Exists(vswhere))
            return null;
        var start = new ProcessStartInfo(vswhere) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
        foreach (var argument in new[] { "-latest", "-products", "*", "-property", "productPath" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(5000);
        return output.Length > 0 && File.Exists(output) ? output : null;
    }
}

public static class IdeResolver
{
    /// <summary>
    /// The IDE's executable: <paramref name="configured"/> when it still exists, else the known install
    /// locations, else PATH. Null when nothing is found; never throws.
    /// </summary>
    public static string? Resolve(IdeKind ide, string? configured, IdeProbe? probe = null)
    {
        probe ??= IdeProbe.Default;
        try
        {
            if (!string.IsNullOrWhiteSpace(configured) && probe.FileExists(configured))
                return configured;
            return Candidates(ide, probe).FirstOrDefault(probe.FileExists)
                ?? PathNames(ide).Select(probe.OnPath).FirstOrDefault(p => p is not null);
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> Candidates(IdeKind ide, IdeProbe probe) =>
        probe.Windows ? WindowsCandidates(ide, probe) : UnixCandidates(ide, probe);

    /// <summary>Linux and macOS: Toolbox under ~/.local/share, then the usual package and snap locations. No Visual Studio.</summary>
    private static IEnumerable<string> UnixCandidates(IdeKind ide, IdeProbe probe)
    {
        var home = probe.Env("HOME") ?? "";
        switch (ide)
        {
            case IdeKind.Rider:
                // '/' joins on purpose: Path.Combine would use '\' when this runs under the Windows test host.
                foreach (var sh in probe.FindFiles(home + "/.local/share/JetBrains/Toolbox/apps", "rider.sh").OrderDescending(StringComparer.Ordinal))
                    yield return sh;
                yield return home + "/.local/share/JetBrains/Toolbox/scripts/rider";
                yield return "/opt/jetbrains/rider/bin/rider.sh";
                yield return "/opt/rider/bin/rider.sh";
                yield return "/snap/bin/rider";
                yield return "/Applications/Rider.app/Contents/MacOS/rider";
                break;
            case IdeKind.VsCode:
                yield return "/usr/bin/code";
                yield return "/usr/share/code/bin/code";
                yield return "/snap/bin/code";
                yield return "/usr/local/bin/code";
                yield return "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
                break;
        }
    }

    private static IEnumerable<string> WindowsCandidates(IdeKind ide, IdeProbe probe)
    {
        var local = probe.Env("LOCALAPPDATA") ?? "";
        var programs = probe.Env("ProgramFiles") ?? "";
        switch (ide)
        {
            case IdeKind.Rider:
                // ponytail: ordinal max picks the newest Toolbox build; good enough until a 9→10 version rollover bites.
                foreach (var exe in probe.FindFiles(Path.Combine(local, @"JetBrains\Toolbox\apps\Rider"), "rider64.exe").OrderDescending(StringComparer.Ordinal))
                    yield return exe;
                yield return Path.Combine(local, @"Programs\Rider\bin\rider64.exe");
                yield return Path.Combine(local, @"JetBrains\Toolbox\scripts\rider.cmd");
                foreach (var exe in probe.FindFiles(Path.Combine(programs, "JetBrains"), "rider64.exe").OrderDescending(StringComparer.Ordinal))
                    yield return exe;
                break;
            case IdeKind.VsCode:
                yield return Path.Combine(local, @"Programs\Microsoft VS Code\Code.exe");
                yield return Path.Combine(programs, @"Microsoft VS Code\Code.exe");
                break;
            case IdeKind.VisualStudio:
                if (probe.VsWhere() is { } devenv)
                    yield return devenv;
                break;
        }
    }

    private static string[] PathNames(IdeKind ide) => ide switch
    {
        IdeKind.Rider => ["rider64", "rider", "rider.sh"],
        IdeKind.VsCode => ["code"],
        _ => ["devenv"],
    };
}
