using Microsoft.Extensions.DependencyInjection;
using SeeSharp.Content;
using SeeSharp.Core;
using SeeSharp.Progress;

namespace SeeSharp.App.Tests;

/// <summary>Exam mode (task 8) through the prerendered pages: results withheld while the run is open, the gate on other pages, real exams reveal nothing.</summary>
public sealed class ExamPageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "seesharp-exam-page-tests-" + Guid.NewGuid().ToString("N"));
    private const string SecretTest = "Secret_test_name_only_after_submission";

    public ExamPageTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public async Task Results_are_withheld_until_submission_and_real_exams_reveal_nothing()
    {
        Locale.Apply("de");
        using var store = new ProgressStore(Path.Combine(_dir, "progress.db"));
        var app = await AppHost.BuildAsync(WriteCourse(), port: 0, admin: false, store);
        var course = app.Services.GetRequiredService<CourseState>().Course!;
        var first = course.Modules[0].Exercises[0].Manifest;
        var second = course.Modules[0].Exercises[1].Manifest;
        store.RecordAttempt(new Attempt("first", "q1", DateTimeOffset.UtcNow, TimeSpan.Zero, true, "0"), first);
        store.RecordAttempt(new Attempt("second", "code", DateTimeOffset.UtcNow, TimeSpan.Zero, true, GradingJson: "{}"), second);
        var practice = course.Exams.Single(e => !e.IsReal);
        var real = course.Exams.Single(e => e.IsReal);
        await app.StartAsync();
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
            // Prerendered HTML carries umlauts as numeric entities.
            Assert.Contains("verf&#xFC;gbar", await http.GetStringAsync("/exams"));

            var run = Exams.Start(store, course, practice, seed: 1);
            var grading = $$"""{"outcome":"Failed","duration":"00:00:01","tests":[{"name":"{{SecretTest}}","outcome":"Failed","duration":"00:00:00","message":null,"stackTrace":null}],"buildErrors":[],"output":"","artifactsDirectory":null}""";
            Assert.Equal([new ExamItemRef("first", "q1"), new ExamItemRef("second", "code")], run.Items);
            store.RecordAttempt(new Attempt("second", "code", DateTimeOffset.UtcNow, TimeSpan.Zero, false, GradingJson: grading) { ExamRunId = run.Id });
            store.RecordAttempt(new Attempt("first", "q1", DateTimeOffset.UtcNow, TimeSpan.Zero, true, "0") { ExamRunId = run.Id });

            var open = await http.GetStringAsync($"/exam/{run.Id}");
            Assert.DoesNotContain(SecretTest, open);
            Assert.Contains("abgegeben", open); // the item shows only that it was submitted
            Assert.Contains("Verbleibend:", open);
            Assert.DoesNotContain("Weil.", open); // no explanation either
            Assert.Contains("Eine Pr&#xFC;fung l&#xE4;uft", await http.GetStringAsync("/exercise/first")); // every other page is gated

            store.SubmitExamRun(run.Id);
            var result = await http.GetStringAsync($"/exam/{run.Id}");
            Assert.Contains(SecretTest, result);
            Assert.Contains("Weil.", result);
            Assert.Contains("1 von 2 Aufgaben richtig (2 beantwortet)", result);
            Assert.DoesNotContain("Eine Pr&#xFC;fung l&#xE4;uft", await http.GetStringAsync("/exercise/first"));

            var realRun = Exams.Start(store, course, real, seed: 1);
            store.RecordAttempt(new Attempt("second", "code", DateTimeOffset.UtcNow, TimeSpan.Zero, false, GradingJson: grading) { ExamRunId = realRun.Id });
            store.SubmitExamRun(realRun.Id);
            var realPage = await http.GetStringAsync($"/exam/{realRun.Id}");
            Assert.DoesNotContain(SecretTest, realPage);
            Assert.Contains("wird nicht angezeigt", realPage);
            Assert.Throws<InvalidOperationException>(() => { Exams.Start(store, course, real, seed: 2); });
            var list = await http.GetStringAsync("/exams");
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(list, "Erneut starten")); // the practice exam offers a retake, the real one does not
            Assert.DoesNotContain(">Starten<", list);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    /// <summary>One level: a choice exercise and a coding exercise (no starter — the page only shows the path), two exams over it.</summary>
    private string WriteCourse()
    {
        var root = Path.Combine(_dir, "course");
        Write("skills.yaml", "skills:\n  - id: a.one\n    category: a\n    label: { de: \"Eins\" }\n  - id: b.two\n    category: b\n    label: { de: \"Zwei\" }\n");
        Write("modules/01-basics/module.yaml", "id: basics\ntitle: { de: \"Grundlagen\" }\n");
        Write("modules/01-basics/summary.de.md", "# Grundlagen");
        Write("modules/01-basics/01-first/manifest.yaml", """
            id: first
            title: { de: "Erste" }
            teaches: [a.one]
            requires: []
            items:
              - type: multipleChoice
                id: q1
                prompt: { de: "Was?" }
                options: [{ de: "A" }, { de: "B" }]
                correct: [0]
                explanation: { de: "Weil." }
            """);
        Write("modules/01-basics/01-first/instructions.de.md", "# Erste");
        Write("modules/01-basics/02-second/manifest.yaml", """
            id: second
            title: { de: "Zweite" }
            teaches: [b.two]
            requires: [a.one]
            items:
              - type: coding
                id: code
                openFile: "X/X.cs"
                testProject: "T/T.csproj"
            """);
        Write("modules/01-basics/02-second/instructions.de.md", "# Zweite");
        Write("exams/probe.yaml", "id: probe\ntitle: { de: \"Probe\" }\nkind: practice\ncoverage: { levels: [1] }\ntimeLimitMinutes: 30\n");
        Write("exams/zz-ernst.yaml", "id: ernst\ntitle: { de: \"Ernst\" }\nkind: real\ncoverage: { levels: [1] }\ntimeLimitMinutes: 30\n");
        return root;

        void Write(string relative, string content)
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
