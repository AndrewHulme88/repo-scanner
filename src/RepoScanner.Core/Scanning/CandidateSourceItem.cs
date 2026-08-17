namespace RepoScanner.Core;

public sealed class CandidateSourceItem
{
    private CandidateSourceItem(
        CandidateDisposition disposition,
        ScanCandidate? candidate,
        ScanDiagnostic? diagnostic,
        bool affectsCompleteness)
    {
        Disposition = disposition;
        Candidate = candidate;
        Diagnostic = diagnostic;
        AffectsCompleteness = affectsCompleteness;
    }

    public CandidateDisposition Disposition { get; }

    public ScanCandidate? Candidate { get; }

    public ScanDiagnostic? Diagnostic { get; }

    public bool AffectsCompleteness { get; }

    public static CandidateSourceItem Ready(ScanCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new CandidateSourceItem(
            CandidateDisposition.Ready,
            candidate,
            null,
            affectsCompleteness: false);
    }

    public static CandidateSourceItem Skipped(ScanDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new CandidateSourceItem(
            CandidateDisposition.Skipped,
            null,
            diagnostic,
            affectsCompleteness: false);
    }

    public static CandidateSourceItem Failed(ScanDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new CandidateSourceItem(
            CandidateDisposition.Failed,
            null,
            diagnostic,
            affectsCompleteness: true);
    }

    public static CandidateSourceItem Information(
        ScanDiagnostic diagnostic,
        bool affectsCompleteness = false)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new CandidateSourceItem(
            CandidateDisposition.None,
            null,
            diagnostic,
            affectsCompleteness);
    }
}
