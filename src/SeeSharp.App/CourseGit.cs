using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using SeeSharp.Content;

namespace SeeSharp.App;

/// <summary>
/// Admin-mode Git over the course working copy (LibGit2Sharp, no git executable). One repository handle per call;
/// nothing is cached. Commit runs the full validator first and refuses on errors — that rule lives here, not in the page.
/// </summary>
public static class CourseGit
{
    public sealed record Status(string Branch, string? Remote, IReadOnlyList<(string Path, FileStatus State)> Files, string Patch);

    public static bool IsRepository(string root) => Repository.IsValid(root);

    public static Status GetStatus(string root)
    {
        using var repo = new Repository(root);
        var files = repo.RetrieveStatus(new StatusOptions())
            .Where(e => e.State != FileStatus.Ignored)
            .Select(e => (e.FilePath, e.State)).ToList();
        var patch = repo.Diff.Compare<Patch>(repo.Head.Tip?.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory); // the working-directory target includes untracked files
        return new Status(repo.Head.FriendlyName, repo.Network.Remotes.FirstOrDefault()?.Url, files, patch.Content);
    }

    /// <summary>Validates (including the starter check), then stages everything and commits. The commit is null when the report has errors.</summary>
    public static async Task<(ValidationReport Report, Commit? Commit)> CommitAsync(string root, string message, CancellationToken cancellationToken = default)
    {
        var report = await CourseValidator.ValidateAsync(root, new ValidationOptions(RunStarterCheck: true), cancellationToken);
        if (report.HasErrors)
            return (report, null);
        using var repo = new Repository(root);
        Commands.Stage(repo, "*");
        var signature = repo.Config.BuildSignature(DateTimeOffset.Now);
        return (report, repo.Commit(message, signature, signature));
    }

    public static void Push(string root, Settings settings)
    {
        using var repo = new Repository(root);
        var remote = repo.Network.Remotes.FirstOrDefault() ?? throw new InvalidOperationException(Strings.GitNoRemote);
        repo.Network.Push(remote, repo.Head.CanonicalName, new PushOptions { CredentialsProvider = Credentials(settings) });
    }

    /// <summary>Fetch and merge the tracked branch. <see cref="MergeStatus.Conflicts"/> means the working copy now holds conflict markers for the IDE.</summary>
    public static MergeStatus Pull(string root, Settings settings)
    {
        using var repo = new Repository(root);
        if (repo.Head.TrackedBranch is null)
            throw new InvalidOperationException(Strings.GitNoUpstream);
        var signature = repo.Config.BuildSignature(DateTimeOffset.Now);
        var options = new PullOptions { FetchOptions = new FetchOptions { CredentialsProvider = Credentials(settings) } };
        return Commands.Pull(repo, signature, options).Status;
    }

    // ponytail: HTTPS token from settings.json only; SSH remotes are not supported by LibGit2Sharp's bundled native library.
    private static CredentialsHandler? Credentials(Settings settings) =>
        string.IsNullOrEmpty(settings.GitToken) ? null
        : (_, _, _) => new UsernamePasswordCredentials { Username = settings.GitUserName ?? "token", Password = settings.GitToken };
}
