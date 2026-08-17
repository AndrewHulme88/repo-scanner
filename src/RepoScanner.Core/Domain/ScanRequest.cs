namespace RepoScanner.Core;

public sealed class ScanRequest
{
    public const int DefaultMaximumFileSizeBytes = 1_048_576;
    public const int MaximumAllowedFileSizeBytes = 64 * 1_048_576;
    public const int MaximumConcurrentFileBytes = 64 * 1_048_576;

    public ScanRequest(
        string path,
        FindingSeverity failureThreshold = FindingSeverity.High,
        int maximumFileSizeBytes = DefaultMaximumFileSizeBytes,
        int? maximumConcurrency = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFileSizeBytes, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            maximumFileSizeBytes,
            MaximumAllowedFileSizeBytes);

        if (!Enum.IsDefined(failureThreshold))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureThreshold),
                failureThreshold,
                "Failure threshold must be a defined severity.");
        }

        int resolvedConcurrency = maximumConcurrency
            ?? Math.Clamp(Environment.ProcessorCount, 1, 8);
        ArgumentOutOfRangeException.ThrowIfLessThan(resolvedConcurrency, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(resolvedConcurrency, 64);

        if ((long)maximumFileSizeBytes * resolvedConcurrency > MaximumConcurrentFileBytes)
        {
            throw new ArgumentException(
                $"Maximum file size multiplied by concurrency cannot exceed " +
                $"{MaximumConcurrentFileBytes} bytes.",
                nameof(maximumConcurrency));
        }

        Path = System.IO.Path.GetFullPath(path);
        FailureThreshold = failureThreshold;
        MaximumFileSizeBytes = maximumFileSizeBytes;
        MaximumConcurrency = resolvedConcurrency;
    }

    public string Path { get; }

    public FindingSeverity FailureThreshold { get; }

    public int MaximumFileSizeBytes { get; }

    public int MaximumConcurrency { get; }
}
