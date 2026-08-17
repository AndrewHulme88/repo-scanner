namespace RepoScanner.Core;

public sealed class ScanResult
{
    public ScanResult(
        IEnumerable<Finding> findings,
        IEnumerable<ScanDiagnostic> diagnostics,
        FindingSeverity failureThreshold,
        int scannedFileCount,
        bool isComplete,
        TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentOutOfRangeException.ThrowIfNegative(scannedFileCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

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
        ScannedFileCount = scannedFileCount;
        IsComplete = isComplete;
        Elapsed = elapsed;
    }

    public IReadOnlyList<Finding> Findings { get; }

    public IReadOnlyList<ScanDiagnostic> Diagnostics { get; }

    public FindingSeverity FailureThreshold { get; }

    public int ScannedFileCount { get; }

    public bool IsComplete { get; }

    public TimeSpan Elapsed { get; }

    public bool HasFindingsAtOrAboveThreshold =>
        Findings.Any(finding => finding.Severity >= FailureThreshold);
}
