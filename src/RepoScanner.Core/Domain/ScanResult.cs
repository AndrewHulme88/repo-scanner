namespace RepoScanner.Core;

public sealed class ScanResult
{
    public ScanResult(
        IEnumerable<Finding> findings,
        IEnumerable<ScanDiagnostic> diagnostics,
        FindingSeverity failureThreshold,
        int selectedFileCount,
        int scannedFileCount,
        int skippedFileCount,
        int failedFileCount,
        bool isComplete,
        TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentOutOfRangeException.ThrowIfNegative(selectedFileCount);
        ArgumentOutOfRangeException.ThrowIfNegative(scannedFileCount);
        ArgumentOutOfRangeException.ThrowIfNegative(skippedFileCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedFileCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

        if (selectedFileCount != scannedFileCount + skippedFileCount + failedFileCount)
        {
            throw new ArgumentException(
                "Selected file count must equal scanned, skipped, and failed file counts.",
                nameof(selectedFileCount));
        }

        if (isComplete && failedFileCount > 0)
        {
            throw new ArgumentException(
                "A scan with failed files cannot be complete.",
                nameof(isComplete));
        }

        if (!Enum.IsDefined(failureThreshold))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureThreshold),
                failureThreshold,
                "Failure threshold must be a defined severity.");
        }

        Findings = Array.AsReadOnly(findings.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        FailureThreshold = failureThreshold;
        SelectedFileCount = selectedFileCount;
        ScannedFileCount = scannedFileCount;
        SkippedFileCount = skippedFileCount;
        FailedFileCount = failedFileCount;
        IsComplete = isComplete;
        Elapsed = elapsed;
    }

    public IReadOnlyList<Finding> Findings { get; }

    public IReadOnlyList<ScanDiagnostic> Diagnostics { get; }

    public FindingSeverity FailureThreshold { get; }

    public int SelectedFileCount { get; }

    public int ScannedFileCount { get; }

    public int SkippedFileCount { get; }

    public int FailedFileCount { get; }

    public bool IsComplete { get; }

    public TimeSpan Elapsed { get; }

    public bool HasFindingsAtOrAboveThreshold =>
        Findings.Any(finding => finding.Severity >= FailureThreshold);
}
