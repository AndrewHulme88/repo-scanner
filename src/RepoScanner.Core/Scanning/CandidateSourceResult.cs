namespace RepoScanner.Core;

public sealed class CandidateSourceResult
{
    public CandidateSourceResult(
        IEnumerable<ScanCandidate> candidates,
        IEnumerable<ScanDiagnostic> diagnostics,
        bool isComplete)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(diagnostics);

        Candidates = Array.AsReadOnly(candidates.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        IsComplete = isComplete;
    }

    public IReadOnlyList<ScanCandidate> Candidates { get; }

    public IReadOnlyList<ScanDiagnostic> Diagnostics { get; }

    public bool IsComplete { get; }
}
