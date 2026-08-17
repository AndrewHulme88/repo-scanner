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
            selectedFileCount: 1,
            scannedFileCount: 1,
            skippedFileCount: 0,
            failedFileCount: 0,
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

    [Fact]
    public void ConstructorRejectsInconsistentFileAccounting()
    {
        Assert.Throws<ArgumentException>(
            () => new ScanResult(
                [],
                [],
                FindingSeverity.High,
                selectedFileCount: 2,
                scannedFileCount: 1,
                skippedFileCount: 0,
                failedFileCount: 0,
                isComplete: true,
                TimeSpan.Zero));
    }
}
