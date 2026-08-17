namespace RepoScanner.Core;

public interface IScanCandidateSource
{
    Task<CandidateSourceResult> ReadAsync(
        ScanRequest request,
        CancellationToken cancellationToken);
}
