namespace SeeSharp.Ide.Tests;

public class IdeResolverTests
{
    private const string Local = @"C:\Users\Jörg Müller\AppData\Local";
    private const string Programs = @"C:\Program Files";
    private static readonly string ToolboxRider = Path.Combine(Local, @"JetBrains\Toolbox\apps\Rider\ch-0\251.1\bin\rider64.exe");
    private static readonly string ProgramsRider = Path.Combine(Local, @"Programs\Rider\bin\rider64.exe");
    private static readonly string UserCode = Path.Combine(Local, @"Programs\Microsoft VS Code\Code.exe");

    /// <summary>A machine where exactly <paramref name="files"/> exist and <paramref name="onPath"/> is on PATH.</summary>
    private static IdeProbe Machine(IEnumerable<string> files, IDictionary<string, string>? onPath = null, string? vsWhere = null)
    {
        var set = files.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new IdeProbe
        {
            Env = name => name == "LOCALAPPDATA" ? Local : name == "ProgramFiles" ? Programs : null,
            FileExists = set.Contains,
            FindFiles = (dir, pattern) => set.Where(f => f.StartsWith(dir, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(f) == pattern),
            OnPath = name => onPath?.TryGetValue(name, out var p) == true ? p : null,
            VsWhere = () => vsWhere,
        };
    }

    [Fact]
    public void Configured_path_wins_when_it_exists()
    {
        var probe = Machine([@"D:\my rider\rider64.exe", ToolboxRider]);
        Assert.Equal(@"D:\my rider\rider64.exe", IdeResolver.Resolve(IdeKind.Rider, @"D:\my rider\rider64.exe", probe));
    }

    [Fact]
    public void Stale_configured_path_falls_through_to_detection()
    {
        var probe = Machine([ProgramsRider]);
        Assert.Equal(ProgramsRider, IdeResolver.Resolve(IdeKind.Rider, @"D:\gone\rider64.exe", probe));
    }

    [Fact]
    public void Rider_prefers_toolbox_then_programs_then_program_files_then_path()
    {
        var programFilesRider = Path.Combine(Programs, @"JetBrains\JetBrains Rider 2025.1\bin\rider64.exe");
        var onPath = new Dictionary<string, string> { ["rider"] = @"C:\scripts\rider.cmd" };
        Assert.Equal(ToolboxRider, IdeResolver.Resolve(IdeKind.Rider, null, Machine([ToolboxRider, ProgramsRider, programFilesRider], onPath)));
        Assert.Equal(ProgramsRider, IdeResolver.Resolve(IdeKind.Rider, null, Machine([ProgramsRider, programFilesRider], onPath)));
        Assert.Equal(programFilesRider, IdeResolver.Resolve(IdeKind.Rider, null, Machine([programFilesRider], onPath)));
        Assert.Equal(@"C:\scripts\rider.cmd", IdeResolver.Resolve(IdeKind.Rider, null, Machine([], onPath)));
        Assert.Null(IdeResolver.Resolve(IdeKind.Rider, null, Machine([])));
    }

    [Fact]
    public void Newest_toolbox_build_wins()
    {
        var older = Path.Combine(Local, @"JetBrains\Toolbox\apps\Rider\ch-0\243.9\bin\rider64.exe");
        Assert.Equal(ToolboxRider, IdeResolver.Resolve(IdeKind.Rider, null, Machine([older, ToolboxRider])));
    }

    [Fact]
    public void VsCode_prefers_user_install_then_machine_install_then_path()
    {
        var machineCode = Path.Combine(Programs, @"Microsoft VS Code\Code.exe");
        var onPath = new Dictionary<string, string> { ["code"] = @"C:\bin\code.cmd" };
        Assert.Equal(UserCode, IdeResolver.Resolve(IdeKind.VsCode, null, Machine([UserCode, machineCode], onPath)));
        Assert.Equal(machineCode, IdeResolver.Resolve(IdeKind.VsCode, null, Machine([machineCode], onPath)));
        Assert.Equal(@"C:\bin\code.cmd", IdeResolver.Resolve(IdeKind.VsCode, null, Machine([], onPath)));
    }

    [Fact]
    public void VisualStudio_uses_vswhere_then_path()
    {
        const string devenv = @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe";
        var onPath = new Dictionary<string, string> { ["devenv"] = @"C:\bin\devenv.exe" };
        Assert.Equal(devenv, IdeResolver.Resolve(IdeKind.VisualStudio, null, Machine([devenv], onPath, vsWhere: devenv)));
        Assert.Equal(@"C:\bin\devenv.exe", IdeResolver.Resolve(IdeKind.VisualStudio, null, Machine([], onPath)));
        Assert.Null(IdeResolver.Resolve(IdeKind.VisualStudio, null, Machine([])));
    }

    [Fact]
    public void A_throwing_probe_means_not_found()
    {
        var probe = new IdeProbe { FileExists = _ => throw new IOException("disk on fire") };
        Assert.Null(IdeResolver.Resolve(IdeKind.Rider, @"C:\x.exe", probe));
    }
}
