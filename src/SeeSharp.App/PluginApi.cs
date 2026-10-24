using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SeeSharp.Content;
using SeeSharp.Grading;
using SeeSharp.Progress;

namespace SeeSharp.App;

/// <summary>
/// JSON API under <c>/api</c> for IDE plugins (docs/IDE-PLUGINS.md). A website in the trainee's browser can reach localhost too,
/// so every request needs the token from <see cref="ServerFile"/> and must not carry a foreign <c>Origin</c>.
/// </summary>
public static class PluginApi
{
    public const string TokenHeader = "X-SeeSharp-Token";

    /// <summary><c>%LOCALAPPDATA%\SeeSharp\server.json</c>: url, token and pid of the running server, for plugins to find it.</summary>
    public static string ServerFile => Path.Combine(Path.GetDirectoryName(Settings.DefaultPath)!, "server.json");

    public static void WriteServerFile(string url, string token, string? path = null)
    {
        path ??= ServerFile;
        var json = JsonSerializer.Serialize(new { url, token, pid = Environment.ProcessId });
        var dir = Path.GetDirectoryName(path)!;
        if (OperatingSystem.IsWindows())
        {
            // %LOCALAPPDATA% is already private to the user.
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, json);
            return;
        }
        // Unix: the token must not be readable by other local users. Create owner-only, never reuse an older,
        // possibly permissive file (UnixCreateMode only applies to new files).
        Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.Delete(path);
        using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        });
        using var writer = new StreamWriter(stream);
        writer.Write(json);
    }

    /// <summary>Only our own file: a second server started meanwhile owns it now.</summary>
    public static void DeleteServerFile(string? path = null)
    {
        path ??= ServerFile;
        try
        {
            if (File.Exists(path) && JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("pid").GetInt32() == Environment.ProcessId)
                File.Delete(path);
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Stale file: plugins treat an unreachable url as "not running".
        }
    }

    public static void Map(WebApplication app, CourseState state)
    {
        app.Use((context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/api"))
                return next(context);
            var origin = context.Request.Headers.Origin.ToString();
            var own = $"{context.Request.Scheme}://{context.Request.Host}";
            var token = Encoding.UTF8.GetBytes(context.Request.Headers[TokenHeader].ToString());
            if ((origin.Length > 0 && !string.Equals(origin, own, StringComparison.OrdinalIgnoreCase))
                || !CryptographicOperations.FixedTimeEquals(token, Encoding.UTF8.GetBytes(state.ApiToken)))
                return Error(StatusCodes.Status403Forbidden, "forbidden").ExecuteAsync(context);
            // The exam gate (MainLayout) covers plugins too: no instructions, no test output while a run is open.
            if (state.Store.ActiveRun() is not null)
                return Error(StatusCodes.Status409Conflict, Strings.ExamActiveGate).ExecuteAsync(context);
            if (state.Course is null)
                return Error(StatusCodes.Status503ServiceUnavailable, Strings.NoCourse).ExecuteAsync(context);
            return next(context);
        });

        // Time spent per attempt, like CodingTask's stopwatch: from opening the exercise (or the last grade) to the grade.
        var opened = new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        var api = app.MapGroup("/api");

        api.MapGet("/course", () =>
        {
            var progress = state.Progress();
            return Results.Json(new
            {
                locale = Locale.Current,
                modules = progress.Modules.Select(m => new
                {
                    id = m.Module.Manifest.Id,
                    level = m.Module.Order,
                    title = CourseState.Text(m.Module.Manifest.Title),
                    unlocked = m.Unlocked,
                    passed = m.Passed,
                    exercises = m.Exercises.Select(e => ExerciseJson(state, e)),
                }),
            });
        });

        api.MapGet("/exercise/{id}", (string id) =>
        {
            if (Find(state, id) is not { } p)
                return Error(StatusCodes.Status404NotFound, Strings.ApiNotFound);
            var instructions = p.Exercise.Instructions(Locale.Current);
            opened[id] = DateTimeOffset.UtcNow;
            return Results.Json(new
            {
                exercise = ExerciseJson(state, p),
                // Locked: like the exercise page, nothing but the title and the missing skills.
                instructionsHtml = p.Unlocked ? CourseState.RenderMarkdown(instructions.Path).Value : null,
                instructionsFellBack = instructions.FellBack,
            });
        });

        api.MapPost("/exercise/{id}/grade", async (string id, CancellationToken cancel) =>
        {
            var (exercise, item, error) = Coding(state, id);
            if (error is not null)
                return error;
            var result = await new GradingRunner().RunAsync(exercise!.TestsDirectory, exercise.StarterDirectory, cancellationToken: cancel);
            var now = DateTimeOffset.UtcNow;
            var spent = opened.TryGetValue(id, out var since) ? now - since : TimeSpan.Zero;
            opened[id] = now;
            var attempt = state.Store.RecordAttempt(Attempts.Coding(id, item!, exercise.StarterDirectory, result, spent), exercise.Manifest);
            return Results.Json(new { passed = attempt.Passed, result }, Attempts.GradingJson);
        });

        api.MapPost("/exercise/{id}/workspace", (string id) =>
        {
            var (exercise, item, error) = Coding(state, id);
            if (error is not null)
                return error;
            var (directory, files) = Workspace.Write(state, exercise!, item!);
            opened.TryAdd(id, DateTimeOffset.UtcNow);
            return Results.Json(new { directory, files });
        });
    }

    private static IResult Error(int status, string message) => Results.Json(new { error = message }, statusCode: status);

    private static ExerciseProgress? Find(CourseState state, string id) =>
        state.Progress().Exercises.FirstOrDefault(e => e.Exercise.Manifest.Id == id);

    /// <summary>An unlocked exercise with its (only) coding item, or the error response.</summary>
    private static (Exercise? Exercise, CodingItem? Item, IResult? Error) Coding(CourseState state, string id) =>
        Find(state, id) switch
        {
            null => (null, null, Error(StatusCodes.Status404NotFound, Strings.ApiNotFound)),
            { Unlocked: false } => (null, null, Error(StatusCodes.Status409Conflict, Strings.ApiExerciseLocked)),
            var p when p.Exercise.Manifest.Items.OfType<CodingItem>().FirstOrDefault() is { } item => (p.Exercise, item, null),
            _ => (null, null, Error(StatusCodes.Status404NotFound, Strings.ApiNoCodingItem)),
        };

    private static object ExerciseJson(CourseState state, ExerciseProgress p)
    {
        var e = p.Exercise;
        var coding = e.Manifest.Items.OfType<CodingItem>().FirstOrDefault();
        return new
        {
            id = e.Manifest.Id,
            title = CourseState.Text(e.Manifest.Title),
            optional = e.Manifest.Optional,
            unlocked = p.Unlocked,
            passed = p.Passed,
            missingSkills = p.MissingSkills.Select(s => new { id = s, label = state.SkillLabel(s), level = state.TaughtIn(s)?.Order }),
            items = new { total = e.Manifest.Items.Count, passed = p.PassedItems.Count, coding = coding is not null },
            starterDirectory = coding is null ? null : e.StarterDirectory,
            workspaceDirectory = coding is null ? null : Workspace.Directory(e, coding),
            openFile = coding is null ? null : Workspace.OpenFile(e, coding),
            openLine = coding?.OpenLine,
            instructionsPath = e.Instructions(Locale.Current).Path,
        };
    }
}
