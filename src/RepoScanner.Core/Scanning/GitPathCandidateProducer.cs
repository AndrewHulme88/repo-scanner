using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace RepoScanner.Core;

internal static class GitPathCandidateProducer
{
    private const int MaximumGitPathCharacters = 32_768;

    private static readonly string[] RepositoryOverrideEnvironmentVariables =
    [
        "GIT_ALTERNATE_OBJECT_DIRECTORIES",
        "GIT_COMMON_DIR",
        "GIT_DIR",
        "GIT_INDEX_FILE",
        "GIT_NAMESPACE",
        "GIT_OBJECT_DIRECTORY",
        "GIT_PREFIX",
        "GIT_WORK_TREE",
    ];

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static async Task<bool> TryProduceAsync(
        string scanRoot,
        ChannelWriter<string> pathWriter,
        ChannelWriter<CandidateSourceItem> resultWriter,
        CancellationToken cancellationToken)
    {
        string? gitExecutablePath = ResolveGitExecutable();

        if (gitExecutablePath is null)
        {
            await resultWriter.WriteAsync(
                CandidateSourceItem.Information(
                    new ScanDiagnostic(
                        "RS-D009",
                        "Git is unavailable; ordinary directory selection was used.")),
                cancellationToken);
            return false;
        }

        GitProbeResult probe = await ProbeAsync(
            gitExecutablePath,
            scanRoot,
            cancellationToken);

        if (probe.Status == GitProbeStatus.Unavailable)
        {
            await resultWriter.WriteAsync(
                CandidateSourceItem.Information(
                    new ScanDiagnostic(
                        "RS-D009",
                        "Git is unavailable; ordinary directory selection was used.")),
                cancellationToken);
            return false;
        }

        if (probe.Status == GitProbeStatus.NotRepository)
        {
            return false;
        }

        if (probe.Status == GitProbeStatus.Failed)
        {
            await resultWriter.WriteAsync(
                CandidateSourceItem.Information(
                    new ScanDiagnostic(
                        "RS-D010",
                        "Git repository detection failed; candidate selection is incomplete."),
                    affectsCompleteness: true),
                cancellationToken);
            return true;
        }

        string repositoryRoot = probe.RepositoryRoot!;
        string scope = Path.GetRelativePath(repositoryRoot, scanRoot)
            .Replace(Path.DirectorySeparatorChar, '/');

        ProcessStartInfo startInfo = CreateStartInfo(gitExecutablePath);
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(repositoryRoot);
        startInfo.ArgumentList.Add("--literal-pathspecs");
        startInfo.ArgumentList.Add("ls-files");
        startInfo.ArgumentList.Add("-z");
        startInfo.ArgumentList.Add("--cached");
        startInfo.ArgumentList.Add("--others");
        startInfo.ArgumentList.Add("--exclude-standard");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(scope);

        using Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            await resultWriter.WriteAsync(
                CandidateSourceItem.Information(
                    new ScanDiagnostic(
                        "RS-D010",
                        "Git became unavailable during candidate selection."),
                    affectsCompleteness: true),
                cancellationToken);
            return true;
        }

        bool pathStreamValid;

        try
        {
            Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            pathStreamValid = await ReadPathsAsync(
                process.StandardOutput,
                repositoryRoot,
                scanRoot,
                pathWriter,
                resultWriter,
                cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            _ = await standardError;
        }
        catch (OperationCanceledException)
        {
            TryTerminate(process);
            throw;
        }

        if (process.ExitCode != 0 || !pathStreamValid)
        {
            await resultWriter.WriteAsync(
                CandidateSourceItem.Information(
                    new ScanDiagnostic(
                        "RS-D010",
                        "Git candidate selection failed or returned unsupported path data."),
                    affectsCompleteness: true),
                cancellationToken);
        }

        return true;
    }

    private static async Task<GitProbeResult> ProbeAsync(
        string gitExecutablePath,
        string scanRoot,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = CreateStartInfo(gitExecutablePath);
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(scanRoot);
        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("--show-toplevel");

        using Process process = new() { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            return new GitProbeResult(GitProbeStatus.Unavailable, null);
        }

        string output;
        string error;

        try
        {
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            output = await standardOutput;
            error = await standardError;
        }
        catch (OperationCanceledException)
        {
            TryTerminate(process);
            throw;
        }

        if (process.ExitCode == 0)
        {
            string repositoryRoot = RemoveSingleLineEnding(output);

            if (string.IsNullOrWhiteSpace(repositoryRoot) || !Path.IsPathFullyQualified(repositoryRoot))
            {
                return new GitProbeResult(GitProbeStatus.Failed, null);
            }

            return new GitProbeResult(
                GitProbeStatus.Repository,
                Path.GetFullPath(repositoryRoot));
        }

        return error.Contains("not a git repository", StringComparison.OrdinalIgnoreCase)
            ? new GitProbeResult(GitProbeStatus.NotRepository, null)
            : new GitProbeResult(GitProbeStatus.Failed, null);
    }

    private static async Task<bool> ReadPathsAsync(
        StreamReader reader,
        string repositoryRoot,
        string scanRoot,
        ChannelWriter<string> pathWriter,
        ChannelWriter<CandidateSourceItem> resultWriter,
        CancellationToken cancellationToken)
    {
        char[] buffer = new char[4_096];
        StringBuilder pathBuilder = new();
        bool currentPathTooLong = false;
        bool streamValid = true;

        try
        {
            while (true)
            {
                int charactersRead = await reader.ReadAsync(buffer, cancellationToken);

                if (charactersRead == 0)
                {
                    break;
                }

                for (int index = 0; index < charactersRead; index++)
                {
                    char character = buffer[index];

                    if (character == '\0')
                    {
                        if (currentPathTooLong)
                        {
                            streamValid = false;
                        }
                        else if (pathBuilder.Length > 0)
                        {
                            bool accepted = await TryQueueGitPathAsync(
                                pathBuilder.ToString(),
                                repositoryRoot,
                                scanRoot,
                                pathWriter,
                                resultWriter,
                                cancellationToken);
                            streamValid &= accepted;
                        }

                        pathBuilder.Clear();
                        currentPathTooLong = false;
                        continue;
                    }

                    if (!currentPathTooLong)
                    {
                        if (pathBuilder.Length == MaximumGitPathCharacters)
                        {
                            currentPathTooLong = true;
                            pathBuilder.Clear();
                        }
                        else
                        {
                            pathBuilder.Append(character);
                        }
                    }
                }
            }
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        return streamValid && pathBuilder.Length == 0 && !currentPathTooLong;
    }

    private static async Task<bool> TryQueueGitPathAsync(
        string gitPath,
        string repositoryRoot,
        string scanRoot,
        ChannelWriter<string> pathWriter,
        ChannelWriter<CandidateSourceItem> resultWriter,
        CancellationToken cancellationToken)
    {
        string platformPath = gitPath.Replace('/', Path.DirectorySeparatorChar);
        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, platformPath));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }

        string relativeToScanRoot = Path.GetRelativePath(scanRoot, fullPath);

        if (Path.IsPathRooted(relativeToScanRoot) ||
            relativeToScanRoot.Equals("..", StringComparison.Ordinal) ||
            relativeToScanRoot.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            return true;
        }

        FileAttributes attributes;

        try
        {
            attributes = File.GetAttributes(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await resultWriter.WriteAsync(
                CandidateSourceItem.Information(
                    new ScanDiagnostic(
                        "RS-D008",
                        "A Git-selected filesystem entry could not be inspected.",
                        relativeToScanRoot),
                    affectsCompleteness: true),
                cancellationToken);
            return true;
        }

        bool isDirectory = attributes.HasFlag(FileAttributes.Directory);
        bool isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);

        if (isReparsePoint)
        {
            CandidateSourceItem item = isDirectory
                ? CandidateSourceItem.Information(
                    new ScanDiagnostic(
                        "RS-D003",
                        "A Git-selected symbolic-link directory was not followed.",
                        relativeToScanRoot))
                : CandidateSourceItem.Skipped(
                    new ScanDiagnostic(
                        "RS-D003",
                        "A Git-selected symbolic-link file was not followed.",
                        relativeToScanRoot));
            await resultWriter.WriteAsync(item, cancellationToken);
            return true;
        }

        if (isDirectory)
        {
            await resultWriter.WriteAsync(
                CandidateSourceItem.Information(
                    new ScanDiagnostic(
                        "RS-D012",
                        "A Git-selected directory, such as a submodule, was not traversed.",
                        relativeToScanRoot)),
                cancellationToken);
            return true;
        }

        await pathWriter.WriteAsync(fullPath, cancellationToken);
        return true;
    }

    private static ProcessStartInfo CreateStartInfo(string gitExecutablePath)
    {
        ProcessStartInfo startInfo = new(gitExecutablePath)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = StrictUtf8,
            StandardOutputEncoding = StrictUtf8,
            UseShellExecute = false,
        };
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["LC_ALL"] = "C";

        foreach (string variableName in RepositoryOverrideEnvironmentVariables)
        {
            startInfo.Environment.Remove(variableName);
        }

        return startInfo;
    }

    private static string? ResolveGitExecutable()
    {
        string? pathValue = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        string[] executableNames = OperatingSystem.IsWindows()
            ? GetWindowsExecutableNames()
            : ["git"];

        foreach (string directory in pathValue.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            {
                continue;
            }

            foreach (string executableName in executableNames)
            {
                string candidatePath;

                try
                {
                    candidatePath = Path.Combine(directory, executableName);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (File.Exists(candidatePath))
                {
                    return Path.GetFullPath(candidatePath);
                }
            }
        }

        return null;
    }

    private static string[] GetWindowsExecutableNames()
    {
        string? pathExtensions = Environment.GetEnvironmentVariable("PATHEXT");
        string[] extensions = string.IsNullOrWhiteSpace(pathExtensions)
            ? [".EXE"]
            : pathExtensions.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return extensions
            .Select(extension => $"git{extension}")
            .Prepend("git.exe")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            // Cancellation remains authoritative even if the process exited concurrently.
        }
    }

    private static string RemoveSingleLineEnding(string value)
    {
        if (value.EndsWith("\r\n", StringComparison.Ordinal))
        {
            return value[..^2];
        }

        return value.EndsWith('\n') ? value[..^1] : value;
    }

    private enum GitProbeStatus
    {
        Unavailable,
        NotRepository,
        Repository,
        Failed,
    }

    private sealed record GitProbeResult(GitProbeStatus Status, string? RepositoryRoot);
}
