using System.Text;

namespace RepoScanner.IntegrationTests;

internal sealed class TemporaryScanDirectory : IDisposable
{
    private TemporaryScanDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TemporaryScanDirectory Create()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"repo-scanner-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return new TemporaryScanDirectory(path);
    }

    public string GetPath(string relativePath)
    {
        return System.IO.Path.Combine(Path, relativePath);
    }

    public async Task WriteTextAsync(
        string relativePath,
        string content,
        Encoding? encoding = null)
    {
        string fullPath = PrepareFilePath(relativePath);

        if (encoding is null)
        {
            await File.WriteAllTextAsync(
                fullPath,
                content,
                CancellationToken.None);
        }
        else
        {
            await File.WriteAllTextAsync(
                fullPath,
                content,
                encoding,
                CancellationToken.None);
        }
    }

    public async Task WriteBytesAsync(string relativePath, byte[] content)
    {
        string fullPath = PrepareFilePath(relativePath);
        await File.WriteAllBytesAsync(
            fullPath,
            content,
            CancellationToken.None);
    }

    public bool TryCreateFileSymbolicLink(string relativeLinkPath, string relativeTargetPath)
    {
        try
        {
            File.CreateSymbolicLink(GetPath(relativeLinkPath), relativeTargetPath);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public bool TryCreateDirectorySymbolicLink(string relativeLinkPath, string relativeTargetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(GetPath(relativeLinkPath), relativeTargetPath);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }

    private string PrepareFilePath(string relativePath)
    {
        string fullPath = GetPath(relativePath);
        string? directoryPath = System.IO.Path.GetDirectoryName(fullPath);

        if (directoryPath is not null)
        {
            Directory.CreateDirectory(directoryPath);
        }

        return fullPath;
    }
}
