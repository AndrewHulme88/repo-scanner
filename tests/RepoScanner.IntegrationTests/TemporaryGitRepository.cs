using System.ComponentModel;
using System.Diagnostics;

using RepoScanner.Core;

namespace RepoScanner.IntegrationTests;

internal sealed class TemporaryGitRepository : IDisposable
{
    private readonly TemporaryScanDirectory _directory;

    private TemporaryGitRepository(TemporaryScanDirectory directory)
    {
        _directory = directory;
    }

    public string Path => _directory.Path;

    public static async Task<TemporaryGitRepository> CreateAsync()
    {
        TemporaryScanDirectory directory = TemporaryScanDirectory.Create();
        TemporaryGitRepository repository = new(directory);

        try
        {
            await repository.RunGitAsync("init", "--quiet");
            return repository;
        }
        catch
        {
            repository.Dispose();
            throw;
        }
    }

    public static async Task<bool> IsGitAvailableAsync()
    {
        ProcessStartInfo startInfo = CreateStartInfo();
        startInfo.ArgumentList.Add("--version");
        using Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            return false;
        }

        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    public string GetPath(string relativePath) => _directory.GetPath(relativePath);

    public Task WriteTextAsync(string relativePath, string content)
    {
        return _directory.WriteTextAsync(relativePath, content);
    }

    public Task WriteSyntheticSecretAsync(string relativePath, string value)
    {
        return WriteTextAsync(relativePath, $"{SyntheticSecretRule.Marker}{value}");
    }

    public async Task RunGitAsync(params string[] arguments)
    {
        ProcessStartInfo startInfo = CreateStartInfo();
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(Path);

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = startInfo };
        process.Start();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git command failed with exit code {process.ExitCode}: " +
                $"{await standardError}\n{await standardOutput}");
        }

        _ = await standardOutput;
        _ = await standardError;
    }

    public void Dispose()
    {
        _directory.Dispose();
    }

    private static ProcessStartInfo CreateStartInfo()
    {
        ProcessStartInfo startInfo = new("git")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["LC_ALL"] = "C";
        return startInfo;
    }
}
