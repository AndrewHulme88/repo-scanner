using RepoScanner.Core;

namespace RepoScanner.IntegrationTests;

public sealed class GitCandidateSelectionTests
{
    [Fact]
    public async Task ScanSelectsTrackedAndUntrackedNonIgnoredFiles()
    {
        if (!await TemporaryGitRepository.IsGitAvailableAsync())
        {
            return;
        }

        using TemporaryGitRepository repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteTextAsync(
            ".gitignore",
            """
            *.env
            !important.env
            nested/*
            !nested/allowed.txt
            tracked-ignored.txt
            """);
        await repository.WriteSyntheticSecretAsync("tracked.txt", "tracked-value");
        await repository.WriteSyntheticSecretAsync("tracked-ignored.txt", "tracked-ignored-value");
        await repository.WriteSyntheticSecretAsync("untracked.txt", "untracked-value");
        await repository.WriteSyntheticSecretAsync("ignored.env", "ignored-value");
        await repository.WriteSyntheticSecretAsync("important.env", "important-value");
        await repository.WriteSyntheticSecretAsync("nested/ignored.txt", "nested-ignored-value");
        await repository.WriteSyntheticSecretAsync("nested/allowed.txt", "nested-allowed-value");
        await repository.RunGitAsync("add", ".gitignore", "tracked.txt");
        await repository.RunGitAsync("add", "--force", "tracked-ignored.txt");
        RepositoryScanner scanner = RepositoryScanner.CreateDefault();

        ScanResult result = await scanner.ScanAsync(
            new ScanRequest(repository.Path),
            CancellationToken.None);

        string[] findingPaths = result.Findings
            .Select(finding => finding.Location.RelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expectedPaths =
        [
            "important.env",
            Path.Combine("nested", "allowed.txt"),
            "tracked-ignored.txt",
            "tracked.txt",
            "untracked.txt",
        ];
        Array.Sort(expectedPaths, StringComparer.Ordinal);

        Assert.Equal(expectedPaths, findingPaths);
        Assert.Equal(6, result.SelectedFileCount);
        Assert.Equal(6, result.ScannedFileCount);
        Assert.Equal(0, result.SkippedFileCount);
        Assert.Equal(0, result.FailedFileCount);
        Assert.True(result.IsComplete);
        Assert.DoesNotContain(result.Findings, finding => finding.Location.RelativePath == "ignored.env");
        Assert.DoesNotContain(
            result.Findings,
            finding => finding.Location.RelativePath == Path.Combine("nested", "ignored.txt"));
    }

    [Fact]
    public async Task ScanRestrictsGitSelectionToRequestedSubdirectory()
    {
        if (!await TemporaryGitRepository.IsGitAvailableAsync())
        {
            return;
        }

        using TemporaryGitRepository repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteSyntheticSecretAsync("root.txt", "root-value");
        await repository.WriteSyntheticSecretAsync("other/other.txt", "other-value");
        await repository.WriteSyntheticSecretAsync("[scope]/file with spaces.txt", "space-value");
        await repository.WriteSyntheticSecretAsync("[scope]/unicøde.txt", "unicode-value");
        RepositoryScanner scanner = RepositoryScanner.CreateDefault();

        ScanResult result = await scanner.ScanAsync(
            new ScanRequest(repository.GetPath("[scope]")),
            CancellationToken.None);

        string[] findingPaths = result.Findings
            .Select(finding => finding.Location.RelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["file with spaces.txt", "unicøde.txt"], findingPaths);
        Assert.Equal(2, result.SelectedFileCount);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task ScanDoesNotInspectGitHistoryOrObjectStorage()
    {
        if (!await TemporaryGitRepository.IsGitAvailableAsync())
        {
            return;
        }

        using TemporaryGitRepository repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteSyntheticSecretAsync("historical.txt", "historical-value");
        await repository.RunGitAsync("add", "historical.txt");
        await repository.RunGitAsync(
            "-c",
            "user.name=Repo Scanner Tests",
            "-c",
            "user.email=repo-scanner-tests@example.invalid",
            "commit",
            "--quiet",
            "-m",
            "Add historical fixture");
        File.Delete(repository.GetPath("historical.txt"));
        await repository.RunGitAsync("add", "--update");
        await repository.WriteTextAsync("current.txt", "safe content");
        RepositoryScanner scanner = RepositoryScanner.CreateDefault();

        ScanResult result = await scanner.ScanAsync(
            new ScanRequest(repository.Path),
            CancellationToken.None);

        Assert.Empty(result.Findings);
        Assert.Equal(1, result.SelectedFileCount);
        Assert.Equal(1, result.ScannedFileCount);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task ExplicitIgnoredFileIsStillScanned()
    {
        if (!await TemporaryGitRepository.IsGitAvailableAsync())
        {
            return;
        }

        using TemporaryGitRepository repository = await TemporaryGitRepository.CreateAsync();
        await repository.WriteTextAsync(".gitignore", "ignored.txt");
        await repository.WriteSyntheticSecretAsync("ignored.txt", "explicit-value");
        RepositoryScanner scanner = RepositoryScanner.CreateDefault();

        ScanResult result = await scanner.ScanAsync(
            new ScanRequest(repository.GetPath("ignored.txt")),
            CancellationToken.None);

        Assert.Single(result.Findings);
        Assert.Equal(1, result.SelectedFileCount);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task OrdinaryDirectoryDoesNotTraverseGitMetadataDirectory()
    {
        using TemporaryScanDirectory directory = TemporaryScanDirectory.Create();
        await directory.WriteTextAsync("safe.txt", "safe content");
        await directory.WriteTextAsync(
            ".git/objects/synthetic-object",
            $"{SyntheticSecretRule.Marker}object-value");
        RepositoryScanner scanner = RepositoryScanner.CreateDefault();

        ScanResult result = await scanner.ScanAsync(
            new ScanRequest(directory.Path),
            CancellationToken.None);

        Assert.Empty(result.Findings);
        Assert.Equal(1, result.SelectedFileCount);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "RS-D011");
        Assert.True(result.IsComplete);
    }
}
