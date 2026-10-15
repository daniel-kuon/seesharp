using System.Globalization;
using SeeSharp.Progress;

namespace SeeSharp.Cli;

/// <summary><c>seesharp export-review [--out &lt;zip&gt;]</c>. Writes the supervisor bundle from the local progress database.</summary>
internal static class ExportReviewCommand
{
    public static int Run(string[] args)
    {
        string? output = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--out" && i + 1 < args.Length)
                output = args[++i];
            else
            {
                Console.Error.WriteLine(Strings.ExportReviewUsage);
                return 2;
            }
        }
        output = Path.GetFullPath(output ?? $"seesharp-review-{DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.zip");

        using var store = new ProgressStore();
        ReviewExport.Write(store, output);
        Console.WriteLine(Strings.ExportReviewWritten, output, store.Attempts().Count);
        return 0;
    }
}
