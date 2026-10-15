using System.Reflection;

namespace SeeSharp.Core.Tests;

public class CoreDependencyTests
{
    [Fact]
    public void Core_references_nothing_but_the_bcl()
    {
        var referenced = Assembly.Load("SeeSharp.Core").GetReferencedAssemblies().Select(a => a.Name!);
        Assert.All(referenced, name => Assert.True(name == "netstandard" || name.StartsWith("System", StringComparison.Ordinal), name));
    }
}
