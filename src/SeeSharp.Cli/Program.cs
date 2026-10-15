using System.Reflection;
using SeeSharp.App;
using SeeSharp.Cli;

Locale.Apply(Locale.Resolve(Environment.GetEnvironmentVariable(Locale.EnvironmentVariable), Settings.Load().Locale));

if (args is ["--version"])
{
    var assembly = typeof(Strings).Assembly;
    Console.WriteLine(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString());
    return 0;
}

// `seesharp --admin` is `seesharp start --admin`: a bare option list goes to start.
if (args is [] or ["start", ..] || args[0].StartsWith("--"))
    return await StartCommand.RunAsync(args is ["start", ..] ? args[1..] : args);

if (args is ["grade", ..])
    return await GradeCommand.RunAsync(args[1..]);

if (args is ["validate", ..])
    return await ValidateCommand.RunAsync(args[1..]);

if (args is ["export-review", ..])
    return ExportReviewCommand.Run(args[1..]);

Console.WriteLine(Strings.Usage);
return 0;
