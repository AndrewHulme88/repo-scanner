namespace RepoScanner.IntegrationTests;

public sealed class TestAssemblyTests
{
    [Fact]
    public void TestAssemblyHasExpectedName()
    {
        string? assemblyName = typeof(TestAssemblyTests).Assembly.GetName().Name;

        Assert.Equal("RepoScanner.IntegrationTests", assemblyName);
    }
}
