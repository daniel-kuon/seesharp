namespace SeeSharp.Cli.Tests;

/// <summary>The command lines the shortcuts and the update hand-over build: paths with spaces, commas and quotes must survive.</summary>
public sealed class LauncherTests
{
    private const string Tool = @"C:\Users\Jörg Mü\.dotnet\tools\seesharp.exe";

    [Fact]
    public void HeadlessArguments_quote_values_and_keep_flags_bare()
    {
        Assert.Equal($"--headless \"{Tool}\"", Launcher.HeadlessArguments(Tool, []));
        Assert.Equal($$"""--headless "{{Tool}}" "start" --course "D:\Kurs, Neu" --no-update""",
            Launcher.HeadlessArguments(Tool, ["start", "--course", @"D:\Kurs, Neu", "--no-update"]));
    }

    [Fact]
    public void Script_waits_updates_and_restarts_headless_without_another_update()
    {
        var script = SelfUpdate.Script("It's updating", 4711, Tool, ["--course", @"D:\O'Brien\Kurs"]);
        Assert.StartsWith("Write-Host 'It''s updating'; Wait-Process -Id 4711 ", script);
        Assert.Contains("dotnet tool update -g SeeSharp.Cli; ", script);
        Assert.EndsWith($$"""-ArgumentList '--headless "{{Tool}}" "start" --course "D:\O''Brien\Kurs" --no-update'""", script);
        Assert.Contains($"Start-Process -FilePath '{Launcher.Conhost}' ", script);
    }
}
