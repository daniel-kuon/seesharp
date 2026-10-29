using Microsoft.Extensions.DependencyInjection;
using SeeSharp.Core;
using SeeSharp.Progress;

namespace SeeSharp.App.Tests;

/// <summary>Reviewer mode through the prerendered pages: report to supervisor, code view with line remarks, reply back to the trainee.</summary>
public sealed class ReviewerModeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "seesharp-reviewer-tests-" + Guid.NewGuid().ToString("N"));

    public ReviewerModeTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Supervisor_remarks_a_line_and_only_the_shared_remarks_show_on_the_trainee_page()
    {
        Locale.Apply("de");
        var course = WriteCourse();
        var source = Path.Combine(_dir, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Program.cs"), "int marker_line_one;\nint marker_line_two;\n");

        using var trainee = new ProgressStore(Path.Combine(_dir, "trainee.db"));
        var attempt = trainee.RecordAttempt(new Attempt("first", "q1", DateTimeOffset.UtcNow, TimeSpan.Zero, true, "Weil.", Source: Attempts.Zip(source)));
        var report = Path.Combine(_dir, "report.zip");
        ReviewExport.Write(trainee, report);

        // Supervisor: open the report, remark line 2 (shared) and the answer (private), write the reply.
        using var supervisor = new ProgressStore(Path.Combine(_dir, "supervisor.db"));
        var reply = Path.Combine(_dir, "reply.json");
        await WithApp(course, admin: true, supervisor, async (state, http) =>
        {
            using (var stream = File.OpenRead(report))
                state.ReviewBundle = ReviewBundle.Read(stream);
            var who = state.ReviewBundle.Trainee;
            supervisor.AddRemark(new Remark(who, "first", "q1", attempt.Id, "Program.cs", 2, "Shared_line_remark", true, DateTimeOffset.UtcNow));
            supervisor.AddRemark(new Remark(who, "first", "q1", attempt.Id, null, null, "Private_answer_remark", false, DateTimeOffset.UtcNow));

            var page = await http.GetStringAsync("/admin/review");
            Assert.Contains("marker_line_two", page);
            Assert.Contains("Shared_line_remark", page);
            Assert.Contains("Private_answer_remark", page);
            Assert.Contains("1 private, 1 geteilte Anmerkungen", page);
            Assert.Contains("title=\"Anmerkung zu dieser Zeile\"", page); // line numbers are buttons

            Assert.Equal(1, RemarksReply.Write(supervisor, who, reply));
        });

        // Trainee: import the reply; the shared remark shows next to line 2 of their code, the private one nowhere.
        using (var stream = File.OpenRead(reply))
            trainee.ReplaceReceivedRemarks(RemarksReply.Read(stream));
        await WithApp(course, admin: false, trainee, async (_, http) =>
        {
            var page = await http.GetStringAsync("/exercise/first");
            Assert.Contains("Anmerkungen der Betreuung", page);
            Assert.Contains("marker_line_two", page);
            Assert.Contains("Shared_line_remark", page);
            Assert.DoesNotContain("Private_answer_remark", page);
            Assert.DoesNotContain("title=\"Anmerkung zu dieser Zeile\"", page); // read-only view
            var line = page.IndexOf("marker_line_two", StringComparison.Ordinal);
            Assert.True(line < page.IndexOf("Shared_line_remark", StringComparison.Ordinal)); // the remark sits under its line

            var feedback = await http.GetStringAsync("/feedback");
            Assert.Contains("href=\"exercise/first\"", feedback);
            Assert.Contains("href=\"feedback\"", feedback); // nav link
        });
    }

    private static async Task WithApp(string course, bool admin, ProgressStore store, Func<CourseState, HttpClient, Task> test)
    {
        var app = await AppHost.BuildAsync(course, port: 0, admin, store);
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            await test(app.Services.GetRequiredService<CourseState>(), http);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private string WriteCourse()
    {
        var root = Path.Combine(_dir, "course");
        Write("skills.yaml", """
            skills:
              - id: a.one
                category: a
                label: { de: "Eins" }
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
        return root;

        void Write(string relative, string content)
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
