namespace RepoScanner.Core;

public interface IScanRule
{
    string Id { get; }

    IReadOnlyList<Finding> Evaluate(ScanCandidate candidate);
}
