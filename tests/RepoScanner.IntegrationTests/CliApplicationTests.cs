using RepoScanner.Cli;
using RepoScanner.Core;

namespace RepoScanner.IntegrationTests;

public sealed class CliApplicationTests
{
    [Fact]
    public async Task ScanReturnsFindingsExitCodeWithoutExposingSecret()
    {
        const string secret = "synthetic-integration-value";
        using TemporaryScanFile file = await TemporaryScanFile.CreateAsync(
            $"{SyntheticSecretRule.Marker}{secret}");
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await CliApplication.RunAsync(
            ["scan", file.Path],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.Contains(SyntheticSecretRule.RuleId, output.ToString(), StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanReturnsSuccessForCleanFile()
    {
        using TemporaryScanFile file = await TemporaryScanFile.CreateAsync("safe content");
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await CliApplication.RunAsync(
            ["scan", file.Path],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("found 0 issue(s)", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task ScanReturnsOperationalFailureForMissingPath()
    {
        string missingPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"repo-scanner-missing-{Guid.NewGuid():N}");
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await CliApplication.RunAsync(
            ["scan", missingPath],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Contains("does not exist", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanReturnsOperationalFailureWhenCancelled()
    {
        using TemporaryScanFile file = await TemporaryScanFile.CreateAsync("safe content");
        using StringWriter output = new();
        using StringWriter error = new();
        using CancellationTokenSource cancellationSource = new();
        await cancellationSource.CancelAsync();

        int exitCode = await CliApplication.RunAsync(
            ["scan", file.Path],
            output,
            error,
            cancellationSource.Token);

        Assert.Equal(2, exitCode);
        Assert.Contains("cancelled", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScanRejectsNumericSeverity()
    {
        using TemporaryScanFile file = await TemporaryScanFile.CreateAsync("safe content");
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await CliApplication.RunAsync(
            ["scan", file.Path, "--fail-on", "2"],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Contains("Invalid severity", error.ToString(), StringComparison.Ordinal);
    }
}
