using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeeSharp.Grading;

namespace SeeSharp.Cli;

/// <summary><c>seesharp grade &lt;exercise-path&gt; &lt;source-path&gt; [--json] [--keep-artifacts] [--timeout &lt;seconds&gt;]</c></summary>
internal static class GradeCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<int> RunAsync(string[] args)
    {
        var positional = new List<string>();
        var json = false;
        var keepArtifacts = false;
        TimeSpan? timeout = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json": json = true; break;
                case "--keep-artifacts": keepArtifacts = true; break;
                case "--timeout" when i + 1 < args.Length && double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds):
                    timeout = TimeSpan.FromSeconds(seconds); break;
                case var arg when !arg.StartsWith('-'): positional.Add(arg); break;
                default:
                    Console.Error.WriteLine(Strings.GradeUsage);
                    return 2;
            }
        }
        if (positional is not [var exercise, var source])
        {
            Console.Error.WriteLine(Strings.GradeUsage);
            return 2;
        }

        var result = await new GradingRunner().RunAsync(exercise, source, timeout, keepArtifacts);

        if (json)
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        else
            PrintText(result);

        return result.Outcome == GradingOutcome.Passed ? 0 : 1;
    }

    private static void PrintText(GradingResult result)
    {
        var outcome = result.Outcome switch
        {
            GradingOutcome.Passed => Strings.OutcomePassed,
            GradingOutcome.Failed => Strings.OutcomeFailed,
            GradingOutcome.BuildFailed => Strings.OutcomeBuildFailed,
            _ => Strings.OutcomeTimedOut,
        };
        Console.WriteLine(Strings.ResultLine, outcome, result.Duration.TotalSeconds);

        if (result.Outcome == GradingOutcome.BuildFailed)
        {
            foreach (var error in result.BuildErrors)
                Console.WriteLine("  " + error);
            if (result.BuildErrors.Count == 0)
                Console.WriteLine(result.Output);
        }
        else if (result.Tests.Count > 0)
        {
            Console.WriteLine(Strings.TestsLine, result.Passed, result.Failed);
            foreach (var test in result.Tests)
            {
                var mark = test.Outcome switch
                {
                    TestOutcome.Passed => Strings.TestPassed,
                    TestOutcome.Failed => Strings.TestFailed,
                    _ => Strings.TestSkipped,
                };
                Console.WriteLine($"  {mark} {test.Name}");
                if (test.Outcome == TestOutcome.Failed && test.Message is not null)
                    foreach (var line in test.Message.Split('\n'))
                        Console.WriteLine("        " + line.TrimEnd());
            }
        }

        if (result.ArtifactsDirectory is not null)
            Console.WriteLine(Strings.ArtifactsLine, result.ArtifactsDirectory);
    }
}
