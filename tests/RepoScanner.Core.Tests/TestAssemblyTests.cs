namespace RepoScanner.Core.Tests;

public sealed class TestAssemblyTests
{
    [Fact]
    public void TestAssemblyHasExpectedName()
    {
        string? assemblyName = typeof(TestAssemblyTests).Assembly.GetName().Name;

        Assert.Equal("RepoScanner.Core.Tests", assemblyName);
    }
}
