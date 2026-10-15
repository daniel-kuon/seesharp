namespace SeeSharp.Ide.Tests;

public class IdeLauncherTests : IDisposable
{
    // Real folders: spaces, umlauts and a comma, like the course content.
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SeeSharp Ide Tests " + Guid.NewGuid().ToString("N"));
    private readonly string _project;
    private readonly string _file;

    public IdeLauncherTests()
    {
        var projectDir = Path.Combine(_root, "Übung, Stein", "starter", "Schere");
        Directory.CreateDirectory(Path.Combine(projectDir, "Modelle"));
        _project = Path.Combine(projectDir, "Schere.csproj");
        File.WriteAllText(_project, "<Project />");
        _file = Path.Combine(projectDir, "Modelle", "Spiel.cs");
        File.WriteAllText(_file, "");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Nearest_project_walks_up_from_the_file()
    {
        Assert.Equal(_project, IdeLauncher.NearestProject(_file));
        Assert.Null(IdeLauncher.NearestProject(Path.Combine(_root, "Übung, Stein", "nowhere.cs")));
    }

    [Fact]
    public void Rider_opens_the_project_at_file_and_line()
    {
        var start = IdeLauncher.Arguments(IdeKind.Rider, @"C:\Tools\rider64.exe", _file, 12);
        Assert.Equal(@"C:\Tools\rider64.exe", start.FileName);
        Assert.False(start.UseShellExecute);
        Assert.Equal([_project, "--line", "12", _file], start.ArgumentList);
    }

    [Fact]
    public void VsCode_opens_the_folder_and_goes_to_file_line()
    {
        var start = IdeLauncher.Arguments(IdeKind.VsCode, @"C:\Tools\Code.exe", _file, 12);
        Assert.Equal([Path.GetDirectoryName(_project)!, "-g", _file + ":12"], start.ArgumentList);
    }

    [Fact]
    public void VisualStudio_opens_the_project_and_runs_goto()
    {
        var start = IdeLauncher.Arguments(IdeKind.VisualStudio, @"C:\VS\devenv.exe", _file, 12);
        Assert.Equal([_project, _file, "/Command", "Edit.GoTo 12"], start.ArgumentList);
    }

    [Fact]
    public void Without_a_line_nothing_is_added()
    {
        Assert.Equal([_project, _file], IdeLauncher.Arguments(IdeKind.Rider, "r", _file, null).ArgumentList);
        Assert.Equal([Path.GetDirectoryName(_project)!, "-g", _file], IdeLauncher.Arguments(IdeKind.VsCode, "c", _file, null).ArgumentList);
        Assert.Equal([_project, _file], IdeLauncher.Arguments(IdeKind.VisualStudio, "d", _file, null).ArgumentList);
    }

    [Fact]
    public void Launch_reports_a_missing_executable_instead_of_throwing()
    {
        Assert.NotNull(IdeLauncher.Launch(IdeKind.Rider, Path.Combine(_root, "missing.exe"), _file, 1));
    }
}
