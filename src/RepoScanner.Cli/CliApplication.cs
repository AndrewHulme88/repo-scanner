using RepoScanner.Core;

namespace RepoScanner.Cli;

internal static class CliApplication
{
    private const int SuccessExitCode = 0;
    private const int FindingsExitCode = 1;
    private const int OperationalFailureExitCode = 2;

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        CliParseResult parseResult = CliOptions.Parse(args);

        if (parseResult.ShowHelp)
        {
            await WriteHelpAsync(output);
            return SuccessExitCode;
        }

        if (parseResult.ErrorMessage is not null)
        {
            await error.WriteLineAsync(parseResult.ErrorMessage);
            await error.WriteLineAsync();
            await WriteHelpAsync(error);
            return OperationalFailureExitCode;
        }

        CliOptions options = parseResult.Options!;

        try
        {
            ScanRequest request = new(options.Path, options.FailureThreshold);
            RepositoryScanner scanner = RepositoryScanner.CreatePhaseOneScanner();
            ScanResult result = await scanner.ScanAsync(request, cancellationToken);

            await WriteResultAsync(result, output);

            if (!result.IsComplete)
            {
                return OperationalFailureExitCode;
            }

            return result.HasFindingsAtOrAboveThreshold
                ? FindingsExitCode
                : SuccessExitCode;
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Scan cancelled. No clean result was produced.");
            return OperationalFailureExitCode;
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message);
            return OperationalFailureExitCode;
        }
    }

    private static async Task WriteResultAsync(ScanResult result, TextWriter output)
    {
        foreach (Finding finding in result.Findings)
        {
            await output.WriteLineAsync(
                $"{finding.Severity} {finding.RuleId} " +
                $"{finding.Location.RelativePath}:{finding.Location.Line}:{finding.Location.Column}");
            await output.WriteLineAsync($"  {finding.Title}");
            await output.WriteLineAsync($"  {finding.Explanation}");
            await output.WriteLineAsync($"  Evidence: {finding.Evidence}");
            await output.WriteLineAsync($"  {finding.Remediation}");
        }

        foreach (ScanDiagnostic diagnostic in result.Diagnostics)
        {
            string location = diagnostic.RelativePath is null
                ? string.Empty
                : $" ({diagnostic.RelativePath})";
            await output.WriteLineAsync(
                $"Diagnostic {diagnostic.Code}{location}: {diagnostic.Message}");
        }

        await output.WriteLineAsync(
            $"Scanned {result.ScannedFileCount} file(s); " +
            $"found {result.Findings.Count} issue(s); " +
            $"complete: {result.IsComplete.ToString().ToLowerInvariant()}; " +
            $"elapsed: {result.Elapsed.TotalMilliseconds:F0} ms.");
    }

    private static Task WriteHelpAsync(TextWriter writer)
    {
        return writer.WriteAsync(
            """
            Repo Scanner

            Usage:
              repo-scanner scan [path] [--fail-on <severity>]

            Arguments:
              path                  File or directory to scan. Defaults to the current directory.

            Options:
              --fail-on <severity>  low, medium, high, or critical. Defaults to high.
              -h, --help            Show help.

            Phase 1 limitation: directory scans inspect files directly inside the selected
            directory only. Safe recursive traversal is implemented in Phase 2.

            """);
    }
}
