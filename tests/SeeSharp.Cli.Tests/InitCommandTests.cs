using System.Net;
using LibGit2Sharp;
using SeeSharp.App;

namespace SeeSharp.Cli.Tests;

/// <summary>Against a local bare repository, never GitHub; the settings file goes to a temp path via SEESHARP_SETTINGS.</summary>
public sealed class InitCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "seesharp-init-" + Guid.NewGuid().ToString("N"));
    private readonly string _work;
    private readonly string _bare;
    private readonly string _target;
    private readonly StringWriter _output = new();

    public InitCommandTests()
    {
        _work = Path.Combine(_root, "work");
        _bare = Path.Combine(_root, "upstream.git");
        _target = Path.Combine(_root, "Kurs, Übung #1");
        Repository.Init(_work);
        Commit("skills.yaml", "skills: []");
        Repository.Clone(_work, _bare, new CloneOptions { IsBare = true });
    }

    [Fact]
    public void Clones_into_an_empty_folder()
    {
        Directory.CreateDirectory(_target);
        Assert.True(InitCommand.CloneOrUpdate(_target, _bare, _output));
        Assert.Equal("skills: []", File.ReadAllText(Path.Combine(_target, "skills.yaml")));
    }

    [Fact]
    public void Refuses_a_folder_that_is_not_empty_and_not_a_clone()
    {
        Directory.CreateDirectory(_target);
        File.WriteAllText(Path.Combine(_target, "notes.txt"), "mine");
        Assert.False(InitCommand.CloneOrUpdate(_target, _bare, _output));
        Assert.False(Directory.Exists(Path.Combine(_target, ".git")));
        Assert.Equal("mine", File.ReadAllText(Path.Combine(_target, "notes.txt")));
    }

    [Fact]
    public void Refuses_a_clone_of_another_repository()
    {
        Assert.True(InitCommand.CloneOrUpdate(_target, _bare, _output));
        Assert.False(InitCommand.CloneOrUpdate(_target, "https://example.invalid/other.git", _output));
    }

    [Fact]
    public void Pulls_an_existing_clone_and_keeps_local_changes()
    {
        Assert.True(InitCommand.CloneOrUpdate(_target, _bare, _output));
        File.WriteAllText(Path.Combine(_target, "skills.yaml"), "skills: [mine]"); // the trainee's edit
        Commit("modules/new.txt", "new");
        Publish();

        Assert.True(InitCommand.CloneOrUpdate(_target, _bare, _output));
        Assert.Equal("new", File.ReadAllText(Path.Combine(_target, "modules", "new.txt")));
        Assert.Equal("skills: [mine]", File.ReadAllText(Path.Combine(_target, "skills.yaml")));
    }

    [Fact]
    public void Stops_instead_of_overwriting_a_local_change()
    {
        Assert.True(InitCommand.CloneOrUpdate(_target, _bare, _output));
        File.WriteAllText(Path.Combine(_target, "skills.yaml"), "skills: [mine]");
        Commit("skills.yaml", "skills: [theirs]");
        Publish();

        Assert.False(InitCommand.CloneOrUpdate(_target, _bare, _output));
        Assert.Equal("skills: [mine]", File.ReadAllText(Path.Combine(_target, "skills.yaml")));
    }

    [Theory]
    [InlineData("https://github.com/daniel-kuon/seesharp-course.git", "https://github.com/daniel-kuon/seesharp-course/")]
    [InlineData(@"C:\x\Kurs.git", "C:/x/kurs")]
    public void Remote_urls_compare_loosely(string a, string b) => Assert.True(InitCommand.SameUrl(a, b));

    [Fact]
    public async Task Writes_the_overridden_settings_file_and_skips_a_book_that_is_present()
    {
        var settingsFile = Path.Combine(_root, "settings dir", "settings.json");
        var previous = Environment.GetEnvironmentVariable(Settings.EnvironmentVariable);
        Environment.SetEnvironmentVariable(Settings.EnvironmentVariable, settingsFile);
        try
        {
            Assert.True(InitCommand.CloneOrUpdate(_target, _bare, _output));
            var book = Path.Combine(_target, "book");
            Directory.CreateDirectory(book);
            File.WriteAllText(Path.Combine(book, "csharp14.pdf"), "AAAA"); // same size as the server's file: kept
            var handler = new StubHandler("BBBB");

            using var http = new HttpClient(handler);
            await InitCommand.RunAsync(_target, _bare, books: true, http, _output);

            Assert.Equal("AAAA", File.ReadAllText(Path.Combine(book, "csharp14.pdf")));
            Assert.Equal("BBBB", File.ReadAllText(Path.Combine(book, "csharp14.en.pdf")));
            Assert.False(File.Exists(Path.Combine(book, "csharp14.en.pdf.part")));
            Assert.Equal(2, handler.Requests.Count);
            Assert.All(handler.Requests, u => Assert.StartsWith(InitCommand.BookRelease, u.ToString()));

            Assert.Equal(settingsFile, Settings.FilePath);
            var settings = Settings.Load();
            Assert.Equal(_target, settings.CoursePath);
            Assert.Equal(Path.Combine(book, "csharp14.pdf"), settings.BookPdfPath);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Settings.EnvironmentVariable, previous);
        }
    }

    private void Commit(string file, string content)
    {
        using var repo = new Repository(_work);
        var path = Path.Combine(_work, file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        Commands.Stage(repo, file);
        var signature = new Signature("Test", "test@example.invalid", DateTimeOffset.Now);
        repo.Commit("update " + file, signature, signature);
    }

    /// <summary>Moves the work repository's branches into the bare "server" repository.</summary>
    private void Publish()
    {
        using var bare = new Repository(_bare);
        if (bare.Network.Remotes["work"] is null)
            bare.Network.Remotes.Add("work", _work);
        Commands.Fetch(bare, "work", ["+refs/heads/*:refs/heads/*"], new FetchOptions(), null);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root))
            return;
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal); // git objects are read-only
        Directory.Delete(_root, recursive: true);
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
