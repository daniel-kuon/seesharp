using System.Globalization;
using SeeSharp.App;

namespace SeeSharp.Cli;

/// <summary><c>seesharp start [--course &lt;path&gt;] [--port &lt;n&gt;] [--no-browser]</c>; also what a bare <c>seesharp</c> runs.</summary>
internal static class StartCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string? course = null;
        var port = 0;
        var openBrowser = true;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--course" when i + 1 < args.Length: course = args[++i]; break;
                case "--port" when i + 1 < args.Length && int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out port): break;
                case "--no-browser": openBrowser = false; break;
                default:
                    Console.Error.WriteLine(Strings.StartUsage);
                    return 2;
            }
        }
        return await AppHost.RunAsync(course, port, openBrowser, Console.Out);
    }
}
