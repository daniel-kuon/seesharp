using System.Diagnostics;
using SeeSharp.App.Components;
using SeeSharp.Progress;

namespace SeeSharp.App;

public static class AppHost
{
    /// <summary>
    /// Serves the UI on localhost (<paramref name="port"/> 0 = any free port), opens the browser and blocks until
    /// Ctrl+C. <paramref name="courseArgument"/> is <c>--course</c>; see <see cref="CoursePaths.Resolve"/> for the fallbacks.
    /// </summary>
    public static async Task<int> RunAsync(string? courseArgument, int port, bool openBrowser, TextWriter console)
    {
        Locale.Apply(Locale.Resolve(Environment.GetEnvironmentVariable(Locale.EnvironmentVariable), Settings.Load().Locale));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(o => o.DetailedErrors = true);
        builder.Services.AddSingleton(new CourseState(new ProgressStore(), courseArgument));

        var app = builder.Build();
        var state = app.Services.GetRequiredService<CourseState>();
        await state.LoadAsync();
        state.DetectIdes();

        app.UseAntiforgery();
        app.MapRazorComponents<Components.App>().AddInteractiveServerRenderMode();
        app.MapGet("/book.pdf", () => state.BookPdfPath is { } pdf ? Results.File(pdf, "application/pdf") : Results.NotFound());
        // Embedded by the EmbedBlazorScript target in SeeSharp.App.csproj; see the comment there.
        app.MapGet("/_framework/blazor.web.js", () =>
            Results.Stream(typeof(AppHost).Assembly.GetManifestResourceStream("blazor.web.js")!, "text/javascript"));

        await app.StartAsync();
        var url = app.Urls.First();
        console.WriteLine(string.Format(Strings.Listening, url));
        if (openBrowser)
            OpenBrowser(url);
        await app.WaitForShutdownAsync();
        return 0;
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
