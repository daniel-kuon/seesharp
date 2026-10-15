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
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddRazorComponents().AddInteractiveServerComponents(o => o.DetailedErrors = true);
        builder.Services.AddSingleton(new CourseState(new ProgressStore(), courseArgument));

        var app = builder.Build();
        var state = app.Services.GetRequiredService<CourseState>();
        await state.LoadAsync();

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
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        await app.WaitForShutdownAsync();
        return 0;
    }
}
