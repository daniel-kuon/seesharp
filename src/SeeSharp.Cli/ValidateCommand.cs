using SeeSharp.Content;

namespace SeeSharp.Cli;

/// <summary><c>seesharp validate &lt;course-path&gt; [--skip-starter-check]</c>. Exit 0 when there are no errors, 1 otherwise, 2 on usage.</summary>
internal static class ValidateCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var positional = new List<string>();
        var runStarterCheck = true;
        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--skip-starter-check": runStarterCheck = false; break;
                case var a when !a.StartsWith('-'): positional.Add(a); break;
                default:
                    Console.Error.WriteLine(Strings.ValidateUsage);
                    return 2;
            }
        }
        if (positional is not [var course])
        {
            Console.Error.WriteLine(Strings.ValidateUsage);
            return 2;
        }

        var report = await CourseValidator.ValidateAsync(course, new ValidationOptions(runStarterCheck));

        foreach (var finding in report.Findings.OrderByDescending(f => f.Severity).ThenBy(f => f.Location, StringComparer.Ordinal))
        {
            var severity = finding.Severity == Severity.Error ? Strings.SeverityError : Strings.SeverityWarning;
            Console.WriteLine($"{severity} {finding.Location}: {finding.Message}");
        }

        var summary = report.Summary;
        Console.WriteLine();
        Console.WriteLine(Strings.SummaryHeader);
        foreach (var module in summary.Modules)
            Console.WriteLine(Strings.SummaryModuleLine, module.Order, module.Id, module.Exercises, module.SkillsTaught, module.SkillsNew, module.SkillsRequired);
        Console.WriteLine(Strings.SummaryTotals, summary.SkillsTaught, summary.SkillsInCatalog);
        Console.WriteLine(Strings.SummaryDangling, summary.Dangling.Count == 0 ? "–" : string.Join(", ", summary.Dangling));
        Console.WriteLine(Strings.SummaryDeadEnds, summary.DeadEnds.Count == 0 ? "–" : string.Join(", ", summary.DeadEnds));
        Console.WriteLine();
        Console.WriteLine(Strings.ValidateResultLine,
            report.Findings.Count(f => f.Severity == Severity.Error),
            report.Findings.Count(f => f.Severity == Severity.Warning));

        return report.HasErrors ? 1 : 0;
    }
}
