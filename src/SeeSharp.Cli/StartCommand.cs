using System.Globalization;
using SeeSharp.App;

namespace SeeSharp.Cli;

/// <summary><c>seesharp start [--course &lt;path&gt;] [--port &lt;n&gt;] [--no-browser] [--no-update] [--admin] [--review]</c>; also what a bare <c>seesharp</c> (with or without <c>--admin</c>) runs.</summary>
internal static class StartCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string? course = null;
        var port = 0;
        var openBrowser = true;
        var admin = false;
        var review = false;
        var update = true;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--course" when i + 1 < args.Length: course = args[++i]; break;
                case "--port" when i + 1 < args.Length && int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out port): break;
                case "--no-browser": openBrowser = false; break;
                case "--admin": admin = true; break;
                case "--review": review = true; break;
                case SelfUpdate.Flag: update = false; break;
                default:
                    Console.Error.WriteLine(Strings.StartUsage);
                    return 2;
            }
        }
        // Only a window start updates itself: a headless start belongs to an IDE plugin, which holds on to this process.
        if (openBrowser && update && await SelfUpdate.TryHandOverAsync(args))
            return 0;
        if (openBrowser)
            Launcher.EnsureShortcuts(desktop: false); // installs from before the shortcuts get theirs on the first start
        return await AppHost.RunAsync(course, port, openBrowser, admin, Console.Out, review);
    }
}
