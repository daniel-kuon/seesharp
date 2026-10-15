using System.Reflection;
using SeeSharp.Cli;

if (args is ["--version"])
{
    var assembly = typeof(Strings).Assembly;
    Console.WriteLine(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString());
    return 0;
}

Console.WriteLine(Strings.Usage);
return 0;
