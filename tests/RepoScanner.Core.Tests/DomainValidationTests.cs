namespace RepoScanner.Core.Tests;

public sealed class DomainValidationTests
{
    [Fact]
    public void ScanRequestRejectsUndefinedFailureThreshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ScanRequest(".", (FindingSeverity)99));
    }

    [Fact]
    public void FindingRejectsUndefinedSeverity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Finding(
                "RS-TEST",
                (FindingSeverity)99,
                "Test finding",
                "Synthetic test explanation.",
                new FindingLocation("sample.txt", 1, 1),
                RedactedEvidence.FromSecret("synthetic"),
                "Remove the synthetic value."));
    }

    [Fact]
    public void ScanRequestRejectsUnboundedCombinedFileSizeAndConcurrency()
    {
        Assert.Throws<ArgumentException>(
            () => new ScanRequest(
                ".",
                maximumFileSizeBytes: ScanRequest.MaximumAllowedFileSizeBytes,
                maximumConcurrency: 2));
    }
}
