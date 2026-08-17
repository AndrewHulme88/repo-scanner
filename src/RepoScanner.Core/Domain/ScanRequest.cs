namespace RepoScanner.Core;

public sealed class ScanRequest
{
    public ScanRequest(string path, FindingSeverity failureThreshold = FindingSeverity.High)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!Enum.IsDefined(failureThreshold))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureThreshold),
                failureThreshold,
                "Failure threshold must be a defined severity.");
        }

        Path = System.IO.Path.GetFullPath(path);
        FailureThreshold = failureThreshold;
    }

    public string Path { get; }

    public FindingSeverity FailureThreshold { get; }
}
