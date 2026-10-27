using LibGit2Sharp;
using SeeSharp.App;
using SeeSharp.Content;

namespace SeeSharp.Cli;

/// <summary>
/// <c>seesharp init [--dir &lt;path&gt;] [--repo &lt;url&gt;] [--no-book]</c>: clone (or fast-forward) the course, download the
/// books into <c>&lt;dir&gt;/book</c>, write the course path to the settings file and validate. Idempotent; never discards
/// the trainee's changes (safe checkout: a pull that would overwrite one stops with a message instead).
/// </summary>
internal static class InitCommand
{
    public const string DefaultRepository = "https://github.com/wollume-studios/seesharp-course.git";
    public const string BookRelease = "https://github.com/wollume-studios/seesharp-course/releases/download/book-1/";
    // The course moved to wollume-studios on 2026-10-27; clones made before still point here.
    public const string FormerRepository = "https://github.com/daniel-kuon/seesharp-course.git";
    public static readonly string[] BookFiles = [CoursePaths.BookFile, "csharp14.en.pdf"];

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "seesharp-course");

    public static async Task<int> RunAsync(string[] args)
    {
        var directory = DefaultDirectory;
        var repository = DefaultRepository;
        var books = true;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dir" when i + 1 < args.Length: directory = args[++i]; break;
                case "--repo" when i + 1 < args.Length: repository = args[++i]; break;
                case "--no-book": books = false; break;
                default:
                    Console.Error.WriteLine(Strings.InitUsage);
                    return 2;
            }
        }
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        return await RunAsync(directory, repository, books, http, Console.Out);
    }

    public static async Task<int> RunAsync(string directory, string repository, bool books, HttpClient http, TextWriter output)
    {
        directory = Path.GetFullPath(directory);
        if (!CloneOrUpdate(directory, repository, output))
            return 1;

        var failed = false;
        if (books)
        {
            foreach (var file in BookFiles)
            {
                try
                {
                    await DownloadAsync(http, new Uri(BookRelease + file), Path.Combine(directory, CoursePaths.BookFolder, file), output);
                }
                catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException)
                {
                    output.WriteLine();
                    output.WriteLine(Strings.InitBookFailed, file, e.Message);
                    failed = true;
                }
            }
        }

        var settings = Settings.Load();
        settings.CoursePath = directory;
        var book = Path.Combine(directory, CoursePaths.BookFolder, CoursePaths.BookFile);
        if (File.Exists(book))
            settings.BookPdfPath = book;
        settings.Save();
        output.WriteLine(Strings.InitSettingsWritten, Settings.FilePath);

        var report = await CourseValidator.ValidateAsync(directory, new ValidationOptions(RunStarterCheck: false));
        foreach (var finding in report.Findings.Where(f => f.Severity == Severity.Error))
            output.WriteLine($"{Strings.SeverityError} {finding.Location}: {finding.Message}");
        output.WriteLine(Strings.InitSummary, report.Course.Modules.Count, report.Course.ExercisesInOrder.Count());
        output.WriteLine(Strings.ValidateResultLine,
            report.Findings.Count(f => f.Severity == Severity.Error),
            report.Findings.Count(f => f.Severity == Severity.Warning));
        if (report.HasErrors || failed)
            return 1;
        output.WriteLine(Strings.InitDone);
        return 0;
    }

    /// <summary>Empty or missing folder: clone. A clone of <paramref name="repository"/>: fetch + fast-forward. Anything else: refuse.</summary>
    internal static bool CloneOrUpdate(string directory, string repository, TextWriter output)
    {
        if (!Directory.Exists(directory) || !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            output.Write(Strings.InitCloning, repository, directory);
            var progress = new Percent(output);
            var options = new CloneOptions();
            options.FetchOptions.OnTransferProgress = p =>
            {
                progress.Report(p.ReceivedObjects, p.TotalObjects);
                return true; // continue
            };
            Repository.Clone(repository, directory, options);
            output.WriteLine();
            return true;
        }

        if (!Repository.IsValid(directory))
        {
            output.WriteLine(Strings.InitForeignFolder, directory, repository);
            return false;
        }
        using var repo = new Repository(directory);
        if (!PointRemoteAt(repo, repository))
        {
            output.WriteLine(Strings.InitForeignFolder, directory, repository);
            return false;
        }

        output.WriteLine(Strings.InitUpdating, directory);
        var signature = repo.Config.BuildSignature(DateTimeOffset.Now) ?? new Signature("SeeSharp", "seesharp@localhost", DateTimeOffset.Now);
        try
        {
            // Fast-forward only, safe checkout: a local commit or a changed file the update would overwrite throws instead of being touched.
            var result = Commands.Pull(repo, signature, new PullOptions
            {
                MergeOptions = new MergeOptions { FastForwardStrategy = FastForwardStrategy.FastForwardOnly },
            });
            output.WriteLine(result.Status == MergeStatus.UpToDate ? Strings.InitUpToDate : Strings.InitUpdated);
            return true;
        }
        catch (LibGit2SharpException e)
        {
            output.WriteLine(Strings.InitUpdateFailed, e.Message, directory);
            return false;
        }
    }

    /// <summary>True when a remote fetches from <paramref name="repository"/>; a remote still on <see cref="FormerRepository"/> is moved to the default first.</summary>
    internal static bool PointRemoteAt(Repository repo, string repository)
    {
        if (repo.Network.Remotes.Any(r => SameUrl(r.Url, repository)))
            return true;
        var former = SameUrl(repository, DefaultRepository) ? repo.Network.Remotes.FirstOrDefault(r => SameUrl(r.Url, FormerRepository)) : null;
        if (former is null)
            return false;
        repo.Network.Remotes.Update(former.Name, r => r.Url = repository);
        return true;
    }

    internal static bool SameUrl(string a, string b) => Normalize(a) == Normalize(b);

    private static string Normalize(string url)
    {
        url = url.Trim().Replace('\\', '/').TrimEnd('/');
        return (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? url[..^4] : url).ToLowerInvariant();
    }

    /// <summary>Skips a file that is already there with the server's size; otherwise streams to <c>.part</c> and renames.</summary>
    internal static async Task DownloadAsync(HttpClient http, Uri url, string target, TextWriter output)
    {
        var name = Path.GetFileName(target);
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength;
        if (File.Exists(target) && new FileInfo(target).Length == length)
        {
            output.WriteLine(Strings.InitBookPresent, name);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        output.Write(Strings.InitBookDownloading, name, (length ?? 0) / 1024.0 / 1024.0);
        var part = target + ".part";
        var progress = new Percent(output);
        await using (var source = await response.Content.ReadAsStreamAsync())
        await using (var file = File.Create(part))
        {
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read));
                done += read;
                progress.Report(done, length ?? 0);
            }
        }
        File.Move(part, target, overwrite: true);
        output.WriteLine();
    }

    /// <summary>Writes " 10%", " 20%" … on the current line; nothing while the total is unknown.</summary>
    private sealed class Percent(TextWriter output)
    {
        private int _last;

        public void Report(long done, long total)
        {
            if (total <= 0)
                return;
            var step = (int)(done * 10 / total) * 10;
            while (_last < step)
            {
                _last += 10;
                output.Write($" {_last}%");
            }
        }
    }
}
