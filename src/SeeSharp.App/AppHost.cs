using System.Diagnostics;
using SeeSharp.App.Components;
using SeeSharp.Progress;

namespace SeeSharp.App;

public static class AppHost
{
    /// <summary>
    /// Serves the UI on localhost (<paramref name="port"/> 0 = any free port), opens the app window (the browser when no
    /// window can be created) and blocks until the window is closed or Ctrl+C. <paramref name="courseArgument"/> is <c>--course</c>; see <see cref="CoursePaths.Resolve"/> for the fallbacks.
    /// </summary>
    public static async Task<int> RunAsync(string? courseArgument, int port, bool openBrowser, bool admin, TextWriter console, bool review = false)
    {
        Locale.Apply(Locale.Resolve(Environment.GetEnvironmentVariable(Locale.EnvironmentVariable), Settings.Load().Locale));
        var app = await BuildAsync(courseArgument, port, admin, new ProgressStore(), review);
        app.Services.GetRequiredService<CourseState>().DetectIdes();

        await app.StartAsync();
        var url = app.Urls.First();
        PluginApi.WriteServerFile(url, app.Services.GetRequiredService<CourseState>().ApiToken);
        try
        {
            console.WriteLine(string.Format(Strings.Listening, url));
            if (openBrowser && !await AppWindow.TryShowAsync(url, app.Lifetime))
                OpenBrowser(url);
            await app.WaitForShutdownAsync();
        }
        finally
        {
            PluginApi.DeleteServerFile();
        }
        return 0;
    }

    /// <summary>The configured app with the course loaded, not yet started; tests host it on a temp store. Without <paramref name="admin"/> every <c>/admin</c> URL is a 404.</summary>
    public static async Task<WebApplication> BuildAsync(string? courseArgument, int port, bool admin, ProgressStore store, bool review = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(o => o.DetailedErrors = true);
        builder.Services.AddSingleton(new CourseState(store, courseArgument, admin, review));

        var app = builder.Build();
        var state = app.Services.GetRequiredService<CourseState>();
        await state.LoadAsync();

        if (!admin)
            app.Use((context, next) =>
            {
                if (!context.Request.Path.StartsWithSegments("/admin"))
                    return next(context);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            });
        PluginApi.Map(app, state);
        app.UseAntiforgery();
        app.MapRazorComponents<Components.App>().AddInteractiveServerRenderMode();
        app.MapGet("/book.pdf", () => state.BookPdfPath is { } pdf
            ? Results.File(CoursePaths.LocalizedBook(pdf, Locale.Current, File.Exists), "application/pdf")
            : Results.NotFound());
        // Embedded by the EmbedBlazorScript target in SeeSharp.App.csproj; see the comment there.
        app.MapGet("/_framework/blazor.web.js", () =>
            Results.Stream(typeof(AppHost).Assembly.GetManifestResourceStream("blazor.web.js")!, "text/javascript"));
        return app;
    }

    /// <summary>Windows and macOS open URLs through the shell; Linux has no such association, so go through xdg-open. Failure is not fatal — the URL was printed.</summary>
    private static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsLinux())
                Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false })?.Dispose();
            else
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception)
        {
            // No browser: the printed URL is the fallback.
        }
    }
}
