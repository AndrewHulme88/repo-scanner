using System.Text;

using RepoScanner.Core;

namespace RepoScanner.IntegrationTests;

public sealed class FileSystemTraversalTests
{
    [Fact]
    public async Task ScanRecursesIntoDeepDirectoriesAndScansEmptyFiles()
    {
        const string secret = "synthetic-deep-value";
        using TemporaryScanDirectory directory = TemporaryScanDirectory.Create();
        await directory.WriteTextAsync("empty.txt", string.Empty);
        await directory.WriteTextAsync(
            "one/two/three/secret.txt",
            $"{SyntheticSecretRule.Marker}{secret}");
        RepositoryScanner scanner = RepositoryScanner.CreateDefault();

        ScanResult result = await scanner.ScanAsync(
            new ScanRequest(directory.Path, maximumConcurrency: 2),
            CancellationToken.None);

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal(Path.Combine("one", "two", "three", "secret.txt"), finding.Location.RelativePath);
        Assert.Equal(2, result.SelectedFileCount);
        Assert.Equal(2, result.ScannedFileCount);
        Assert.Equal(0, result.SkippedFileCount);
        Assert.Equal(0, result.FailedFileCount);
        Assert.True(result.IsComplete);
        Assert.DoesNotContain(secret, finding.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScanAccountsForLargeBinaryAndInvalidEncodingFiles()
    {
        using TemporaryScanDirectory directory = TemporaryScanDirectory.Create();
        await directory.WriteTextAsync("empty.txt", string.Empty);
        await directory.WriteBytesAsync("binary.dat", [0x01, 0x00, 0x02]);
        await directory.WriteBytesAsync("invalid-utf8.txt", [0xC3, 0x28]);
        await directory.WriteTextAsync("large.txt", new string('x', 17));
        RepositoryScanner scanner = RepositoryScanner.CreateDefault();

        ScanResult result = await scanner.ScanAsync(
            new ScanRequest(
                directory.Path,
                maximumFileSizeBytes: 16,
                maximumConcurrency: 2),
            CancellationToken.None);

        Assert.Equal(4, result.SelectedFileCount);
        Assert.Equal(1, result.ScannedFileCount);
        Assert.Equal(3, result.SkippedFileCount);
        Assert.Equal(0, result.FailedFileCount);
        Assert.True(result.IsComplete);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "RS-D004");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "RS-D005");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "RS-D006");
    }

    [Fact]
    public async Task ScanReadsBomEncodedUtf16Text()
    {
        const string secret = "synthetic-utf16-value";
        using TemporaryScanDirectory directory = TemporaryScanDirectory.Create();
        await directory.WriteTextAsync(
            "utf16.txt",
            $"{SyntheticSecretRule.Marker}{secret}",
            Encoding.Unicode);
        RepositoryScanner scanner = RepositoryScanner.CreateDefault();

        ScanResult result = await scanner.ScanAsync(
            new ScanRequest(directory.Path),
            CancellationToken.None);

        Assert.Single(result.Findings);
        Assert.Equal(1, result.ScannedFileCount);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task ScanDoesNotFollowSymbolicLinks()
    {
        using TemporaryScanDirectory directory = TemporaryScanDirectory.Create();
        using TemporaryScanDirectory outsideDirectory = TemporaryScanDirectory.Create();
        await directory.WriteTextAsync("target.txt", "safe content");
        await outsideDirectory.WriteTextAsync(
            "secret.txt",
            $"{SyntheticSecretRule.Marker}synthetic-link-value");

        if (!directory.TryCreateFileSymbolicLink(
                "file-link.txt",
                outsideDirectory.GetPath("secret.txt")) ||
            !directory.TryCreateDirectorySymbolicLink("directory-link", outsideDirectory.Path))
        {
            return;
        }

        RepositoryScanner scanner = RepositoryScanner.CreateDefault();

        ScanResult result = await scanner.ScanAsync(
            new ScanRequest(directory.Path),
            CancellationToken.None);

        Assert.Empty(result.Findings);
        Assert.Equal(2, result.SelectedFileCount);
        Assert.Equal(1, result.ScannedFileCount);
        Assert.Equal(1, result.SkippedFileCount);
        Assert.Equal(0, result.FailedFileCount);
        Assert.True(result.IsComplete);
        Assert.Equal(2, result.Diagnostics.Count(diagnostic => diagnostic.Code == "RS-D003"));
    }

    [Fact]
    public async Task ScanRejectsSymbolicLinkAsRoot()
    {
        using TemporaryScanDirectory directory = TemporaryScanDirectory.Create();
        await directory.WriteTextAsync("target.txt", "safe content");

        if (!directory.TryCreateFileSymbolicLink("file-link.txt", "target.txt"))
        {
            return;
        }

        RepositoryScanner scanner = RepositoryScanner.CreateDefault();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => scanner.ScanAsync(
                new ScanRequest(directory.GetPath("file-link.txt")),
                CancellationToken.None));

        Assert.Contains("symbolic link", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InaccessibleNestedDirectoryMakesScanIncompleteWhereSupported()
    {
        if (OperatingSystem.IsWindows() || string.Equals(Environment.UserName, "root", StringComparison.Ordinal))
        {
            return;
        }

        using TemporaryScanDirectory directory = TemporaryScanDirectory.Create();
        await directory.WriteTextAsync("restricted/secret.txt", "safe content");
        string restrictedPath = directory.GetPath("restricted");
        UnixFileMode originalMode = File.GetUnixFileMode(restrictedPath);

        try
        {
            File.SetUnixFileMode(restrictedPath, UnixFileMode.None);
            RepositoryScanner scanner = RepositoryScanner.CreateDefault();

            ScanResult result = await scanner.ScanAsync(
                new ScanRequest(directory.Path),
                CancellationToken.None);

            Assert.False(result.IsComplete);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "RS-D007");
        }
        finally
        {
            File.SetUnixFileMode(restrictedPath, originalMode);
        }
    }

    [Fact]
    public async Task PreCancelledScanStopsBeforeTraversal()
    {
        using TemporaryScanDirectory directory = TemporaryScanDirectory.Create();
        await directory.WriteTextAsync("sample.txt", "safe content");
        using CancellationTokenSource cancellationSource = new();
        await cancellationSource.CancelAsync();
        RepositoryScanner scanner = RepositoryScanner.CreateDefault();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scanner.ScanAsync(
                new ScanRequest(directory.Path),
                cancellationSource.Token));
    }
}
