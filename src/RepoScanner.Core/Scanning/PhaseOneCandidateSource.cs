namespace RepoScanner.Core;

public sealed class PhaseOneCandidateSource : IScanCandidateSource
{
    public async Task<CandidateSourceResult> ReadAsync(
        ScanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (File.Exists(request.Path))
        {
            string relativePath = Path.GetFileName(request.Path);

            try
            {
                ScanCandidate candidate = await ReadFileAsync(
                    request.Path,
                    relativePath,
                    cancellationToken);

                return new CandidateSourceResult([candidate], [], isComplete: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new CandidateSourceResult(
                    [],
                    [new ScanDiagnostic("RS-D001", "The selected file could not be read.", relativePath)],
                    isComplete: false);
            }
        }

        if (!Directory.Exists(request.Path))
        {
            throw new ArgumentException("The scan path does not exist.", nameof(request));
        }

        List<ScanCandidate> candidates = [];
        List<ScanDiagnostic> diagnostics = [];
        bool isComplete = true;

        string[] filePaths;

        try
        {
            filePaths = Directory.GetFiles(request.Path, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(filePaths, StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException("The scan directory could not be enumerated.", nameof(request));
        }

        foreach (string filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string relativePath = Path.GetRelativePath(request.Path, filePath);

            try
            {
                candidates.Add(await ReadFileAsync(filePath, relativePath, cancellationToken));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new ScanDiagnostic(
                    "RS-D001",
                    "A selected file could not be read.",
                    relativePath));
                isComplete = false;
            }
        }

        return new CandidateSourceResult(candidates, diagnostics, isComplete);
    }

    private static async Task<ScanCandidate> ReadFileAsync(
        string fullPath,
        string relativePath,
        CancellationToken cancellationToken)
    {
        string content = await File.ReadAllTextAsync(fullPath, cancellationToken);
        return new ScanCandidate(fullPath, relativePath, content);
    }
}
