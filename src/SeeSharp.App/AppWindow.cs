using Photino.NET;

namespace SeeSharp.App;

/// <summary>
/// The app's own window: a WebView2 (Windows) / WebKit window on the local server via Photino. The server stays, so the IDE
/// plugins keep their <c>/api</c>; the window is only the face. Closing it stops the server, stopping the server closes it.
/// </summary>
public static class AppWindow
{
    private static string Folder => Path.GetDirectoryName(Settings.DefaultPath)!;

    /// <summary>The app icon as a file, for the window and the shortcuts: written next to the settings from the embedded copy when missing.</summary>
    public static string IconFile()
    {
        var path = Path.Combine(Folder, "seesharp.ico");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Folder);
            using var icon = typeof(AppWindow).Assembly.GetManifestResourceStream("seesharp.ico")!;
            using var file = File.Create(path);
            icon.CopyTo(file);
        }
        return path;
    }

    /// <summary>
    /// Opens the window on its own STA thread and returns once it exists. False when no window could be created
    /// (no WebView2 runtime, no display); the caller falls back to the browser.
    /// </summary>
    public static async Task<bool> TryShowAsync(string url, IHostApplicationLifetime lifetime)
    {
        var created = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var window = new PhotinoWindow()
                    .SetTitle(Strings.Title)
                    .SetIconFile(IconFile())
                    .SetUseOsDefaultSize(false)
                    .SetSize(1280, 900)
                    .Center()
                    .SetContextMenuEnabled(true)
                    .SetDevToolsEnabled(false)
                    // WebView2 keeps its profile next to the executable by default, which is the tool store an update replaces.
                    .SetTemporaryFilesPath(Path.Combine(Folder, "window"))
                    .Load(new Uri(url));
                window.WindowCreated += (_, _) => created.TrySetResult(true);
                using var stopping = lifetime.ApplicationStopping.Register(() => window.Close());
                window.WaitForClose();
                lifetime.StopApplication();
            }
            catch (Exception)
            {
                created.TrySetResult(false);
            }
        }) { IsBackground = true, Name = "SeeSharp window" };
        if (OperatingSystem.IsWindows())
            thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return await created.Task;
    }
}
