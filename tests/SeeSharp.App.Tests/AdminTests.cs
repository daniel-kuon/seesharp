using LibGit2Sharp;
using SeeSharp.Content;
using SeeSharp.Progress;

namespace SeeSharp.App.Tests;

/// <summary>Admin mode (task 7): the flag gates the routes, and a commit needs a clean validation.</summary>
public sealed class AdminTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "seesharp-admin-tests-" + Guid.NewGuid().ToString("N"));

    public AdminTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (!Directory.Exists(_dir))
            return;
        foreach (var file in Directory.EnumerateFiles(_dir, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal); // git objects are read-only
        Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [InlineData(false, 404)]
    [InlineData(true, 200)]
    public async Task Admin_routes_exist_only_with_the_flag(bool admin, int expected)
    {
        using var store = new ProgressStore(Path.Combine(_dir, "progress.db"));
        var app = await AppHost.BuildAsync(WriteCourse(), port: 0, admin, store);
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            foreach (var route in new[] { "/admin", "/admin/skills", "/admin/skills/edit", "/admin/git", "/admin/review" })
                Assert.Equal(expected, (int)(await http.GetAsync(route)).StatusCode);
            Assert.Equal(200, (int)(await http.GetAsync("/modules")).StatusCode);
            var nav = await http.GetStringAsync("/");
            Assert.Equal(admin, nav.Contains("href=\"admin/git\""));
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Commit_is_blocked_on_validation_errors_and_pushes_when_clean()
    {
        var course = WriteCourse();
        Repository.Init(course);
        var bare = Repository.Init(Path.Combine(_dir, "remote.git"), isBare: true);
        using (var repo = new Repository(course))
        {
            repo.Config.Set("user.name", "Test");
            repo.Config.Set("user.email", "test@example.com");
            repo.Network.Remotes.Add("origin", bare);
        }
        var manifest = Path.Combine(course, "modules", "01-basics", "01-first", "manifest.yaml");
        var good = File.ReadAllText(manifest);
        File.WriteAllText(manifest, good.Replace("a.one", "a.unknown"));

        var (report, commit) = await CourseGit.CommitAsync(course, "broken");

        Assert.Null(commit);
        Assert.Contains(report.Findings, f => f.Code == FindingCode.SkillUnknown);
        using (var repo = new Repository(course))
            Assert.Null(repo.Head.Tip);

        File.WriteAllText(manifest, good);
        (report, commit) = await CourseGit.CommitAsync(course, "first commit");

        Assert.NotNull(commit);
        Assert.False(report.HasErrors);
        var status = CourseGit.GetStatus(course);
        Assert.Empty(status.Files);
        Assert.Equal("", status.Patch);

        // Push to the local bare remote, then set the upstream so Pull has something to track.
        CourseGit.Push(course, new Settings());
        using (var repo = new Repository(course))
        {
            Assert.Equal(commit.Sha, repo.Branches["origin/" + repo.Head.FriendlyName].Tip.Sha);
            repo.Branches.Update(repo.Head, b => b.TrackedBranch = "refs/remotes/origin/" + repo.Head.FriendlyName);
        }
        Assert.Equal(MergeStatus.UpToDate, CourseGit.Pull(course, new Settings()));

        File.WriteAllText(Path.Combine(course, "notes.md"), "untracked");
        status = CourseGit.GetStatus(course);
        Assert.Equal(("notes.md", FileStatus.NewInWorkdir), Assert.Single(status.Files));
        Assert.Contains("+untracked", status.Patch);
    }

    /// <summary>The validator tests' minimal course: two exercises without coding items, so the starter check has nothing to run.</summary>
    private string WriteCourse()
    {
        var root = Path.Combine(_dir, "course");
        Write("skills.yaml", """
            skills:
              - id: a.one
                category: a
                label: { de: "Eins" }
              - id: b.two
                category: b
                label: { de: "Zwei" }
            """);
        Write("modules/01-basics/module.yaml", "id: basics\ntitle: { de: \"Grundlagen\" }\n");
        Write("modules/01-basics/summary.de.md", "# Grundlagen");
        Write("modules/01-basics/01-first/manifest.yaml", """
            id: first
            title: { de: "Erste" }
            teaches: [a.one]
            requires: []
            items:
              - type: open
                id: q1
                prompt: { de: "Warum?" }
            """);
        Write("modules/01-basics/01-first/instructions.de.md", "# Erste");
        Write("modules/01-basics/01-first/rubric.md", "# Rubrik");
        Write("modules/01-basics/02-second/manifest.yaml", """
            id: second
            title: { de: "Zweite" }
            teaches: [b.two]
            requires: [a.one]
            items:
              - type: review
                id: r1
                prompt: { de: "Zeig her." }
            """);
        Write("modules/01-basics/02-second/instructions.de.md", "# Zweite");
        Write("modules/01-basics/02-second/rubric.md", "# Rubrik");
        return root;

        void Write(string relative, string content)
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
