using System.Diagnostics;

namespace SeeSharp.Ide;

public static class IdeLauncher
{
    /// <summary>Starts the IDE on the project that owns <paramref name="file"/>, at <paramref name="line"/>. Returns the failure message, or null; never waits.</summary>
    public static string? Launch(IdeKind ide, string executable, string file, int? line)
    {
        try
        {
            Process.Start(Arguments(ide, executable, file, line))?.Dispose();
            return null;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>The command line per IDE. ArgumentList quotes spaces and umlauts for us.</summary>
    public static ProcessStartInfo Arguments(IdeKind ide, string executable, string file, int? line)
    {
        var project = NearestProject(file);
        var projectDir = Path.GetDirectoryName(project ?? file)!;
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        var args = start.ArgumentList;
        switch (ide)
        {
            case IdeKind.Rider: // rider64.exe "<csproj>" --line <n> "<file>"
                args.Add(project ?? projectDir);
                if (line is { } l1) { args.Add("--line"); args.Add(l1.ToString()); }
                args.Add(file);
                break;
            case IdeKind.VsCode: // Code.exe "<project dir>" -g "<file>:<n>"
                args.Add(projectDir);
                args.Add("-g");
                args.Add(line is { } l2 ? $"{file}:{l2}" : file);
                break;
            case IdeKind.VisualStudio: // devenv.exe "<csproj>" "<file>" /Command "Edit.GoTo <n>"
                if (project is not null) args.Add(project);
                args.Add(file);
                if (line is { } l3) { args.Add("/Command"); args.Add($"Edit.GoTo {l3}"); }
                break;
        }
        return start;
    }

    /// <summary>The first <c>.csproj</c> in the file's folder or any parent, or null.</summary>
    public static string? NearestProject(string file)
    {
        for (var dir = Path.GetDirectoryName(file); dir is not null; dir = Path.GetDirectoryName(dir))
            if (Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*.csproj").FirstOrDefault() is { } project)
                return project;
        return null;
    }
}
