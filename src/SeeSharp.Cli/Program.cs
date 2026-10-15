using System.Reflection;
using SeeSharp.Cli;

if (args is ["--version"])
{
    var assembly = typeof(Strings).Assembly;
    Console.WriteLine(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString());
    return 0;
}

if (args is ["grade", ..])
    return await GradeCommand.RunAsync(args[1..]);

if (args is ["validate", ..])
    return await ValidateCommand.RunAsync(args[1..]);

if (args is ["export-review", ..])
    return ExportReviewCommand.Run(args[1..]);

Console.WriteLine(Strings.Usage);
return 0;
