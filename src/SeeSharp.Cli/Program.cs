using System.Reflection;
using System.Text;
using SeeSharp.App;
using SeeSharp.Cli;

// Piped (IDE plugins read the output): UTF-8 instead of the console's OEM code page, so umlauts survive.
// Swapping the writer leaves the code page of an attached console alone.
if (Console.IsOutputRedirected)
    Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true });
if (Console.IsErrorRedirected)
    Console.SetError(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true });

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

if (args is ["init", ..])
    return await InitCommand.RunAsync(args[1..]);

if (args is ["grade", ..])
    return await GradeCommand.RunAsync(args[1..]);

if (args is ["validate", ..])
    return await ValidateCommand.RunAsync(args[1..]);

if (args is ["export-review", ..])
    return ExportReviewCommand.Run(args[1..]);

Console.WriteLine(Strings.Usage);
return 0;
