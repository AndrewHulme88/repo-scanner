namespace RepoScanner.Core;

public sealed class ScanCandidate
{
    public ScanCandidate(string fullPath, string relativePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);

        FullPath = fullPath;
        RelativePath = relativePath;
        Content = content;
    }

    public string FullPath { get; }

    public string RelativePath { get; }

    public string Content { get; }
}
