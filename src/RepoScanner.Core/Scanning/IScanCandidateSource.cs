namespace RepoScanner.Core;

public interface IScanCandidateSource
{
    IAsyncEnumerable<CandidateSourceItem> ReadAsync(
        ScanRequest request,
        CancellationToken cancellationToken);
}
