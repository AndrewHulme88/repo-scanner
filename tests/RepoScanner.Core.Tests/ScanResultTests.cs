namespace RepoScanner.Core.Tests;

public sealed class ScanResultTests
{
    [Theory]
    [InlineData(FindingSeverity.Low, FindingSeverity.High, false)]
    [InlineData(FindingSeverity.High, FindingSeverity.High, true)]
    [InlineData(FindingSeverity.Critical, FindingSeverity.High, true)]
    public void HasFindingsAtOrAboveThresholdUsesSeverityOrdering(
        FindingSeverity findingSeverity,
        FindingSeverity threshold,
        bool expected)
    {
        Finding finding = CreateFinding(findingSeverity);
        ScanResult result = new(
            [finding],
            [],
            threshold,
            scannedFileCount: 1,
            isComplete: true,
            TimeSpan.Zero);

        Assert.Equal(expected, result.HasFindingsAtOrAboveThreshold);
    }

    private static Finding CreateFinding(FindingSeverity severity)
    {
        return new Finding(
            "RS-TEST",
            severity,
            "Test finding",
            "Synthetic test explanation.",
            new FindingLocation("sample.txt", 1, 1),
            RedactedEvidence.FromSecret("synthetic"),
            "Remove the synthetic value.");
    }
}
