using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SeeSharp.Grading;

/// <summary>
/// Grades a trainee's working copy against an exercise's pristine test project by running
/// <c>dotnet test</c> in a throw-away copy. Shells out to <c>dotnet</c>; no MSBuild API, no in-process test host.
/// </summary>
/// <remarks>
/// Layout contract: <c>exerciseDir</c> is an overlay with the same shape as the trainee's working copy and
/// contains exactly one <c>.csproj</c> (the test project). Every top-level directory of the overlay replaces
/// the trainee's directory of the same name wholesale, every file overwrites. The trainee's <c>bin</c>/<c>obj</c>
/// folders and MSBuild/NuGet configuration files (<c>Directory.Build.*</c>, <c>nuget.config</c>, ...) are never
/// copied, so the trainee cannot redirect or disable the pristine tests.
/// </remarks>
public sealed class GradingRunner
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

    private static readonly HashSet<string> UntrustedConfigFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Directory.Build.props", "Directory.Build.targets", "Directory.Build.rsp",
        "Directory.Packages.props", "nuget.config", "dotnet.config",
    };

    /// <summary>
    /// Build output, IDE caches and the editor/AI-assistant folders SeeSharp writes into the starter (SeeSharp.App/Workspace.cs):
    /// never part of a submission. Rider's <c>_ReSharper.Caches</c> even holds a locked mutex file while the project is open.
    /// </summary>
    private static readonly HashSet<string> ArtifactDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", ".idea", "_ReSharper.Caches", ".git", ".vscode", ".github", ".junie", ".aiassistant",
    };

    /// <summary>True when any directory on the relative path is build output or an IDE cache.</summary>
    public static bool IsArtifact(string[] relativeSegments) => relativeSegments[..^1].Any(ArtifactDirectories.Contains);

    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public async Task<GradingResult> RunAsync(
        string exerciseDir,
        string sourceDir,
        TimeSpan? timeout = null,
        bool keepArtifacts = false,
        CancellationToken cancellationToken = default)
    {
        var testProject = Directory.GetFiles(exerciseDir, "*.csproj", SearchOption.AllDirectories) switch
        {
            [var single] => single,
            var found => throw new InvalidOperationException($"Exercise must contain exactly one .csproj, found {found.Length}: {exerciseDir}"),
        };

        var work = Path.Combine(Path.GetTempPath(), "seesharp-grade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            CopyTree(sourceDir, work, trusted: false);
            foreach (var dir in Directory.GetDirectories(exerciseDir))
            {
                var stale = Path.Combine(work, Path.GetFileName(dir));
                if (Directory.Exists(stale))
                    Directory.Delete(stale, recursive: true);
            }
            CopyTree(exerciseDir, work, trusted: true);

            var project = Path.Combine(work, Path.GetRelativePath(exerciseDir, testProject));
            var resultsDir = Path.Combine(work, "TestResults");
            var (exitCode, timedOut, output, elapsed) = await RunDotnetTestAsync(project, resultsDir, timeout ?? DefaultTimeout, cancellationToken);

            var trx = Path.Combine(resultsDir, "results.trx");
            var tests = File.Exists(trx) ? ParseTrx(trx) : [];
            var outcome = timedOut ? GradingOutcome.TimedOut
                : !File.Exists(trx) ? GradingOutcome.BuildFailed
                : exitCode == 0 && tests.Count > 0 && tests.All(t => t.Outcome != TestOutcome.Failed) ? GradingOutcome.Passed
                : GradingOutcome.Failed;
            var buildErrors = outcome == GradingOutcome.BuildFailed ? ExtractBuildErrors(output, work) : [];

            return new GradingResult(outcome, elapsed, tests, buildErrors, output, keepArtifacts ? work : null)
            {
                Trx = File.Exists(trx) ? await File.ReadAllTextAsync(trx, CancellationToken.None) : null,
            };
        }
        finally
        {
            if (!keepArtifacts)
                await DeleteWithRetryAsync(work);
        }
    }

    private static void CopyTree(string from, string to, bool trusted)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (IsArtifact(segments))
                continue;
            if (!trusted && UntrustedConfigFiles.Contains(segments[^1]))
                continue;
            var target = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static async Task<(int ExitCode, bool TimedOut, string Output, TimeSpan Elapsed)> RunDotnetTestAsync(
        string project, string resultsDir, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(project),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // ArgumentList quotes each argument itself, so spaces, umlauts, commas and '#' in paths are safe.
        foreach (var arg in new[] { "test", project, "--nologo", "--logger", "trx;LogFileName=results.trx", "--results-directory", resultsDir })
            psi.ArgumentList.Add(arg);
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        psi.Environment["DOTNET_NOLOGO"] = "1";

        var output = new StringBuilder();
        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };

        var clock = Stopwatch.StartNew();
        process.Start();
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timedOut = false;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            cancellationToken.ThrowIfCancellationRequested();
            timedOut = true;
        }
        try
        {
            // Waits for the redirected streams to drain; capped in case a grandchild survived the kill.
            await Task.Run(process.WaitForExit, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        }
        catch (TimeoutException)
        {
        }
        clock.Stop();

        lock (output)
            return (timedOut ? -1 : process.ExitCode, timedOut, output.ToString(), clock.Elapsed);
    }

    private static List<TestResult> ParseTrx(string path)
    {
        var doc = XDocument.Load(path);
        return doc.Descendants(Trx + "UnitTestResult").Select(r =>
        {
            var error = r.Element(Trx + "Output")?.Element(Trx + "ErrorInfo");
            return new TestResult(
                (string?)r.Attribute("testName") ?? "",
                (string?)r.Attribute("outcome") switch
                {
                    "Passed" => TestOutcome.Passed,
                    "NotExecuted" => TestOutcome.Skipped,
                    _ => TestOutcome.Failed,
                },
                TimeSpan.TryParse((string?)r.Attribute("duration"), CultureInfo.InvariantCulture, out var d) ? d : TimeSpan.Zero,
                (string?)error?.Element(Trx + "Message"),
                (string?)error?.Element(Trx + "StackTrace"));
        }).OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
    }

    private static List<string> ExtractBuildErrors(string output, string work) =>
        output.Split('\n')
            .Select(l => l.Trim().Replace(work + Path.DirectorySeparatorChar, "")) // paths relative to the working copy
            .Where(l => Regex.IsMatch(l, @"\berror [A-Z]+\d+\s*:"))
            .Distinct()
            .ToList();

    private static async Task DeleteWithRetryAsync(string dir)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 5)
            {
                await Task.Delay(500); // a killed test host may still hold file locks for a moment
            }
        }
    }
}
