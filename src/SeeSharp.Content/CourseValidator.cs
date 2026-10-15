using System.Text.RegularExpressions;
using SeeSharp.Grading;

namespace SeeSharp.Content;

/// <param name="RunStarterCheck">Run every coding exercise's tests against the untouched <c>starter/</c>. Slow (one <c>dotnet test</c> per exercise).</param>
public sealed record ValidationOptions(bool RunStarterCheck = true, TimeSpan? StarterTimeout = null);

/// <param name="SkillsNew">Skills taught here for the first time in published order.</param>
public sealed record ModuleSummary(int Order, string Id, int Exercises, int SkillsTaught, int SkillsRequired, int SkillsNew);

public sealed record SkillGraphSummary(
    int SkillsInCatalog,
    int SkillsTaught,
    IReadOnlyList<ModuleSummary> Modules,
    IReadOnlyList<string> Dangling,
    IReadOnlyList<string> DeadEnds);

public sealed record ValidationReport(IReadOnlyList<Finding> Findings, SkillGraphSummary Summary, Course Course)
{
    public bool HasErrors => Findings.Any(f => f.Severity == Severity.Error);
}

/// <summary>Every rule from AGENT_TASKS.md task 3 and SKILL-MODEL.md §3. One <see cref="FindingCode"/> per rule.</summary>
public static class CourseValidator
{
    private static readonly Regex SkillId = new(@"^[a-z][a-z0-9-]*(\.[a-z0-9][a-z0-9-]*)+$", RegexOptions.CultureInvariant);
    private static readonly Regex SlugId = new(@"^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant);
    private const int TaughtTooOftenThreshold = 3;

    public static async Task<ValidationReport> ValidateAsync(string root, ValidationOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ValidationOptions();
        var findings = new List<Finding>();
        var course = CourseLoader.Load(root, findings);
        var catalogLoaded = !findings.Any(f => f.Code is FindingCode.SkillsFileMissing or FindingCode.SkillsFileInvalid);

        var aliases = catalogLoaded ? ValidateSkills(course, findings) : new Dictionary<string, string>();
        ValidateStructure(course, findings);
        var summary = catalogLoaded
            ? ValidateSkillGraph(course, aliases, findings)
            : new SkillGraphSummary(0, 0, [], [], []);
        if (options.RunStarterCheck)
            await CheckStartersAsync(course, options.StarterTimeout, findings, cancellationToken);

        return new ValidationReport(findings, summary, course);
    }

    private static void Add(List<Finding> findings, Severity severity, FindingCode code, string location, params object[] args) =>
        findings.Add(new Finding(severity, code, location, Strings.Format(code, args)));

    private static bool HasDefaultLocale(IDictionary<string, string>? text) =>
        text is not null && text.TryGetValue(CourseLoader.DefaultLocale, out var value) && !string.IsNullOrWhiteSpace(value);

    // ----- skills.yaml -------------------------------------------------------------------------

    /// <returns>alias → canonical id, plus id → id, for resolving manifest references.</returns>
    private static Dictionary<string, string> ValidateSkills(Course course, List<Finding> findings)
    {
        const string file = CourseLoader.SkillsFileName;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var skill in course.Skills.Skills)
        {
            var location = $"{file}#{skill.Id}";
            if (!SkillId.IsMatch(skill.Id))
                Add(findings, Severity.Error, FindingCode.SkillIdInvalid, location, skill.Id);
            else if (!ids.Add(skill.Id))
                Add(findings, Severity.Error, FindingCode.SkillIdDuplicate, location, skill.Id);
            if (string.IsNullOrWhiteSpace(skill.Category))
                Add(findings, Severity.Error, FindingCode.SkillCategoryMissing, location, skill.Id);
            if (!HasDefaultLocale(skill.Label))
                Add(findings, Severity.Error, FindingCode.SkillLabelMissingDefaultLocale, location, skill.Id, CourseLoader.DefaultLocale);
        }

        var resolve = ids.ToDictionary(id => id, id => id, StringComparer.Ordinal);
        foreach (var skill in course.Skills.Skills)
        {
            var location = $"{file}#{skill.Id}";
            if (skill.Supersedes is not null && !ids.Contains(skill.Supersedes))
                Add(findings, Severity.Error, FindingCode.SkillSupersedesUnknown, location, skill.Id, skill.Supersedes);
            foreach (var alias in skill.Aliases)
            {
                if (!resolve.TryAdd(alias, skill.Id))
                    Add(findings, Severity.Error, FindingCode.SkillAliasConflict, location, skill.Id, alias);
            }
        }
        return resolve;
    }

    // ----- modules, exercises, items -----------------------------------------------------------

    private static void ValidateStructure(Course course, List<Finding> findings)
    {
        var moduleIds = new HashSet<string>(StringComparer.Ordinal);
        var exerciseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in course.Modules)
        {
            var location = CourseLoader.Relative(course.Root, Path.Combine(module.Directory, CourseLoader.ModuleManifestFileName));
            if (!SlugId.IsMatch(module.Manifest.Id))
                Add(findings, Severity.Error, FindingCode.ModuleIdInvalid, location, module.Manifest.Id);
            else if (!moduleIds.Add(module.Manifest.Id))
                Add(findings, Severity.Error, FindingCode.ModuleIdDuplicate, location, module.Manifest.Id);
            if (!HasDefaultLocale(module.Manifest.Title))
                Add(findings, Severity.Error, FindingCode.ModuleTitleMissingDefaultLocale, location, CourseLoader.DefaultLocale);
            if (module.Manifest.Reading is { } reading && !IsValid(reading))
                Add(findings, Severity.Error, FindingCode.ReadingInvalid, location, CourseLoader.DefaultLocale);
            if (!File.Exists(module.SummaryPath))
                Add(findings, Severity.Error, FindingCode.ModuleSummaryMissing, CourseLoader.Relative(course.Root, module.Directory), CourseLoader.DefaultLocale);
            if (module.Exercises.Count == 0)
                Add(findings, Severity.Warning, FindingCode.ModuleWithoutExercises, CourseLoader.Relative(course.Root, module.Directory));

            foreach (var exercise in module.Exercises)
                ValidateExercise(course, exercise, exerciseIds, findings);
        }
    }

    private static bool IsValid(Reading reading) =>
        !string.IsNullOrWhiteSpace(reading.Section) && HasDefaultLocale(reading.Title) && reading.PrintedPage >= 1;

    private static void ValidateExercise(Course course, Exercise exercise, HashSet<string> exerciseIds, List<Finding> findings)
    {
        var manifest = exercise.Manifest;
        var location = CourseLoader.Relative(course.Root, Path.Combine(exercise.Directory, CourseLoader.ExerciseManifestFileName));
        if (!SlugId.IsMatch(manifest.Id))
            Add(findings, Severity.Error, FindingCode.ExerciseIdInvalid, location, manifest.Id);
        else if (!exerciseIds.Add(manifest.Id))
            Add(findings, Severity.Error, FindingCode.ExerciseIdDuplicate, location, manifest.Id);
        if (!HasDefaultLocale(manifest.Title))
            Add(findings, Severity.Error, FindingCode.ExerciseTitleMissingDefaultLocale, location, CourseLoader.DefaultLocale);
        if (manifest.Reading is { } reading && !IsValid(reading))
            Add(findings, Severity.Error, FindingCode.ReadingInvalid, location, CourseLoader.DefaultLocale);
        if (!File.Exists(exercise.InstructionsPath))
            Add(findings, Severity.Error, FindingCode.InstructionsMissing, CourseLoader.Relative(course.Root, exercise.Directory), CourseLoader.DefaultLocale);
        if (!File.Exists(exercise.RubricPath))
            Add(findings, Severity.Warning, FindingCode.RubricMissing, CourseLoader.Relative(course.Root, exercise.Directory));

        if (manifest.Items.Count == 0)
            Add(findings, Severity.Error, FindingCode.ItemsEmpty, location);
        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < manifest.Items.Count; i++)
        {
            var item = manifest.Items[i];
            var itemLocation = $"{location}#items[{i}]";
            if (!SlugId.IsMatch(item.Id))
                Add(findings, Severity.Error, FindingCode.ItemIdInvalid, itemLocation, item.Id);
            else if (!itemIds.Add(item.Id))
                Add(findings, Severity.Error, FindingCode.ItemIdDuplicate, itemLocation, item.Id);

            switch (item)
            {
                case MultipleChoiceItem choice:
                    if (!HasDefaultLocale(choice.Prompt))
                        Add(findings, Severity.Error, FindingCode.ItemTextMissingDefaultLocale, itemLocation, "prompt", CourseLoader.DefaultLocale);
                    if (choice.Options.Count < 2)
                        Add(findings, Severity.Error, FindingCode.ChoiceOptionsTooFew, itemLocation);
                    if (choice.Options.Any(o => !HasDefaultLocale(o)))
                        Add(findings, Severity.Error, FindingCode.ItemTextMissingDefaultLocale, itemLocation, "options", CourseLoader.DefaultLocale);
                    if (choice.Correct.Count == 0 || choice.Correct.Any(c => c < 0 || c >= choice.Options.Count))
                        Add(findings, Severity.Error, FindingCode.ChoiceCorrectInvalid, itemLocation, choice.Options.Count - 1);
                    if (choice.Explanation is not null && !HasDefaultLocale(choice.Explanation))
                        Add(findings, Severity.Error, FindingCode.ItemTextMissingDefaultLocale, itemLocation, "explanation", CourseLoader.DefaultLocale);
                    break;
                case OpenItem open:
                    if (!HasDefaultLocale(open.Prompt))
                        Add(findings, Severity.Error, FindingCode.ItemTextMissingDefaultLocale, itemLocation, "prompt", CourseLoader.DefaultLocale);
                    break;
                case ReviewItem review:
                    if (!HasDefaultLocale(review.Prompt))
                        Add(findings, Severity.Error, FindingCode.ItemTextMissingDefaultLocale, itemLocation, "prompt", CourseLoader.DefaultLocale);
                    if (review.OpenFile is not null && !FileExistsUnder(exercise.StarterDirectory, review.OpenFile))
                        Add(findings, Severity.Error, FindingCode.CodingOpenFileMissing, itemLocation, review.OpenFile);
                    break;
                case CodingItem coding:
                    ValidateCoding(exercise, coding, itemLocation, findings);
                    break;
            }
        }
    }

    private static void ValidateCoding(Exercise exercise, CodingItem coding, string location, List<Finding> findings)
    {
        var starterOk = Directory.Exists(exercise.StarterDirectory);
        if (!starterOk)
            Add(findings, Severity.Error, FindingCode.CodingStarterMissing, location);
        else if (!FileExistsUnder(exercise.StarterDirectory, coding.OpenFile))
            Add(findings, Severity.Error, FindingCode.CodingOpenFileMissing, location, coding.OpenFile);

        if (!Directory.Exists(exercise.TestsDirectory))
        {
            Add(findings, Severity.Error, FindingCode.CodingTestsMissing, location);
            return;
        }
        var projects = Directory.GetFiles(exercise.TestsDirectory, "*.csproj", SearchOption.AllDirectories);
        if (projects.Length != 1)
            Add(findings, Severity.Error, FindingCode.CodingTestsNotSingleProject, location, projects.Length);
        if (!FileExistsUnder(exercise.TestsDirectory, coding.TestProject))
            Add(findings, Severity.Error, FindingCode.CodingTestProjectMissing, location, coding.TestProject);
    }

    /// <summary>True when <paramref name="relative"/> names an existing file inside <paramref name="directory"/> (no escaping via <c>..</c>).</summary>
    private static bool FileExistsUnder(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return false;
        var full = Path.GetFullPath(Path.Combine(directory, relative));
        var prefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && File.Exists(full);
    }

    // ----- skill graph -------------------------------------------------------------------------

    private static SkillGraphSummary ValidateSkillGraph(Course course, Dictionary<string, string> resolve, List<Finding> findings)
    {
        var exercises = course.ExercisesInOrder.ToList();
        var locations = exercises.ToDictionary(e => e, e => CourseLoader.Relative(course.Root, Path.Combine(e.Directory, CourseLoader.ExerciseManifestFileName)));
        var teaches = new Dictionary<Exercise, HashSet<string>>();
        var requires = new Dictionary<Exercise, HashSet<string>>();

        foreach (var exercise in exercises)
        {
            var location = locations[exercise];
            teaches[exercise] = Resolve(exercise.Manifest.Teaches, resolve, location, findings);
            requires[exercise] = Resolve(exercise.Manifest.Requires, resolve, location, findings);
            if (exercise.Manifest.Teaches.Count == 0)
                Add(findings, Severity.Warning, FindingCode.TeachesEmpty, location);
            foreach (var both in requires[exercise].Intersect(teaches[exercise]).Order(StringComparer.Ordinal))
                Add(findings, Severity.Error, FindingCode.SkillRequiredAndTaught, location, both);
        }

        // skill → indices of exercises teaching it, in published order
        var taughtBy = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < exercises.Count; i++)
            foreach (var skill in teaches[exercises[i]])
                (taughtBy.TryGetValue(skill, out var list) ? list : taughtBy[skill] = []).Add(i);

        var dangling = new SortedSet<string>(StringComparer.Ordinal);
        var required = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < exercises.Count; i++)
        {
            foreach (var skill in requires[exercises[i]].Order(StringComparer.Ordinal))
            {
                required.Add(skill);
                if (taughtBy.TryGetValue(skill, out var teachers))
                {
                    if (!teachers.Any(j => j < i))
                        Add(findings, Severity.Error, FindingCode.SkillForwardReference, locations[exercises[i]], skill, exercises[teachers[0]].Manifest.Id);
                }
                else
                {
                    dangling.Add(skill);
                    Add(findings, Severity.Error, FindingCode.SkillDangling, locations[exercises[i]], skill);
                }
            }
        }

        foreach (var cycle in FindCycles(exercises, teaches, requires))
            Add(findings, Severity.Error, FindingCode.SkillCycle, CourseLoader.SkillsFileName, string.Join(" -> ", cycle));

        foreach (var (skill, teachers) in taughtBy.OrderBy(p => p.Key, StringComparer.Ordinal))
            if (teachers.Count > TaughtTooOftenThreshold)
                Add(findings, Severity.Warning, FindingCode.SkillTaughtTooOften, locations[exercises[teachers[0]]], skill, teachers.Count);

        var deadEnds = taughtBy.Keys.Where(s => !required.Contains(s)).Order(StringComparer.Ordinal).ToList();
        foreach (var skill in deadEnds)
            Add(findings, Severity.Warning, FindingCode.SkillDeadEnd, locations[exercises[taughtBy[skill][0]]], skill);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var modules = new List<ModuleSummary>();
        foreach (var module in course.Modules)
        {
            var taught = module.Exercises.SelectMany(e => teaches[e]).ToHashSet(StringComparer.Ordinal);
            var req = module.Exercises.SelectMany(e => requires[e]).ToHashSet(StringComparer.Ordinal);
            var fresh = taught.Count(seen.Add);
            modules.Add(new ModuleSummary(module.Order, module.Manifest.Id, module.Exercises.Count, taught.Count, req.Count, fresh));
        }
        return new SkillGraphSummary(course.Skills.Skills.Count, taughtBy.Count, modules, dangling.ToList(), deadEnds);
    }

    private static HashSet<string> Resolve(List<string> ids, Dictionary<string, string> resolve, string location, List<Finding> findings)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (resolve.TryGetValue(id, out var canonical))
                result.Add(canonical);
            else
                Add(findings, Severity.Error, FindingCode.SkillUnknown, location, id);
        }
        return result;
    }

    /// <summary>Edges run from each required skill to each taught skill of the same exercise; a cycle means no valid order exists.</summary>
    private static IEnumerable<List<string>> FindCycles(List<Exercise> exercises, Dictionary<Exercise, HashSet<string>> teaches, Dictionary<Exercise, HashSet<string>> requires)
    {
        var edges = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var exercise in exercises)
            foreach (var from in requires[exercise])
                foreach (var to in teaches[exercise])
                    if (from != to)
                        (edges.TryGetValue(from, out var set) ? set : edges[from] = new SortedSet<string>(StringComparer.Ordinal)).Add(to);

        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 1 = on stack, 2 = done
        var stack = new List<string>();
        var cycles = new List<List<string>>();
        foreach (var start in edges.Keys.Order(StringComparer.Ordinal))
            Visit(start);
        return cycles;

        void Visit(string node)
        {
            if (state.TryGetValue(node, out var s))
            {
                if (s == 1)
                {
                    var cycle = stack.Skip(stack.IndexOf(node)).ToList();
                    cycle.Add(node);
                    cycles.Add(cycle);
                }
                return;
            }
            state[node] = 1;
            stack.Add(node);
            if (edges.TryGetValue(node, out var next))
                foreach (var n in next)
                    Visit(n);
            stack.RemoveAt(stack.Count - 1);
            state[node] = 2;
        }
    }

    // ----- starter check -----------------------------------------------------------------------

    /// <summary>The most valuable rule: tests that pass against the untouched starter mean the solution shipped.</summary>
    private static async Task CheckStartersAsync(Course course, TimeSpan? timeout, List<Finding> findings, CancellationToken cancellationToken)
    {
        var runner = new GradingRunner();
        foreach (var exercise in course.ExercisesInOrder)
        {
            if (!exercise.Manifest.Items.OfType<CodingItem>().Any()
                || !Directory.Exists(exercise.StarterDirectory)
                || !Directory.Exists(exercise.TestsDirectory)
                || Directory.GetFiles(exercise.TestsDirectory, "*.csproj", SearchOption.AllDirectories).Length != 1)
                continue; // structural findings already cover these

            var location = CourseLoader.Relative(course.Root, exercise.Directory);
            var result = await runner.RunAsync(exercise.TestsDirectory, exercise.StarterDirectory, timeout, cancellationToken: cancellationToken);
            switch (result.Outcome)
            {
                case GradingOutcome.Passed:
                    Add(findings, Severity.Error, FindingCode.StarterPassesTests, location);
                    break;
                case GradingOutcome.BuildFailed:
                    Add(findings, Severity.Warning, FindingCode.StarterBuildFailed, location, string.Join("; ", result.BuildErrors.Take(3)));
                    break;
                case GradingOutcome.TimedOut:
                    Add(findings, Severity.Error, FindingCode.StarterTimedOut, location);
                    break;
            }
        }
    }
}
