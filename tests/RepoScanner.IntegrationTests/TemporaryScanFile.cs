namespace RepoScanner.IntegrationTests;

internal sealed class TemporaryScanFile : IDisposable
{
    private TemporaryScanFile(string directoryPath, string path)
    {
        DirectoryPath = directoryPath;
        Path = path;
    }

    public string DirectoryPath { get; }

    public string Path { get; }

    public static async Task<TemporaryScanFile> CreateAsync(string content)
    {
        string directoryPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"repo-scanner-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);

        string filePath = System.IO.Path.Combine(directoryPath, "sample.txt");
        await File.WriteAllTextAsync(filePath, content);

        return new TemporaryScanFile(directoryPath, filePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
