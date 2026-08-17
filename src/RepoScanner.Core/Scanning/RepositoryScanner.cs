using System.Diagnostics;

namespace RepoScanner.Core;

public sealed class RepositoryScanner
{
    private readonly IScanCandidateSource _candidateSource;
    private readonly IScanRule[] _rules;

    public RepositoryScanner(
        IScanCandidateSource candidateSource,
        IEnumerable<IScanRule> rules)
    {
        ArgumentNullException.ThrowIfNull(candidateSource);
        ArgumentNullException.ThrowIfNull(rules);

        _candidateSource = candidateSource;
        _rules = rules.ToArray();

        if (_rules.Length == 0)
        {
            throw new ArgumentException("At least one scan rule is required.", nameof(rules));
        }
    }

    public static RepositoryScanner CreateDefault()
    {
        return new RepositoryScanner(
            new FileSystemCandidateSource(),
            [new SyntheticSecretRule()]);
    }

    public async Task<ScanResult> ScanAsync(
        ScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Stopwatch stopwatch = Stopwatch.StartNew();
        List<Finding> findings = [];
        List<ScanDiagnostic> diagnostics = [];
        int selectedFileCount = 0;
        int scannedFileCount = 0;
        int skippedFileCount = 0;
        int failedFileCount = 0;
        bool sourceComplete = true;

        await foreach (CandidateSourceItem item in _candidateSource.ReadAsync(
            request,
            cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (item.Diagnostic is not null)
            {
                diagnostics.Add(item.Diagnostic);
            }

            if (item.AffectsCompleteness)
            {
                sourceComplete = false;
            }

            if (item.Disposition == CandidateDisposition.None)
            {
                continue;
            }

            selectedFileCount++;

            if (item.Disposition == CandidateDisposition.Skipped)
            {
                skippedFileCount++;
                continue;
            }

            if (item.Disposition == CandidateDisposition.Failed)
            {
                failedFileCount++;
                continue;
            }

            ScanCandidate candidate = item.Candidate!;
            bool ruleFailed = false;

            foreach (IScanRule rule in _rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    findings.AddRange(rule.Evaluate(candidate));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    diagnostics.Add(new ScanDiagnostic(
                        "RS-D002",
                        $"Rule {rule.Id} could not evaluate a selected file.",
                        candidate.RelativePath));
                    ruleFailed = true;
                }
            }

            if (ruleFailed)
            {
                failedFileCount++;
            }
            else
            {
                scannedFileCount++;
            }
        }

        stopwatch.Stop();

        return new ScanResult(
            findings
                .OrderBy(finding => finding.Location.RelativePath, StringComparer.Ordinal)
                .ThenBy(finding => finding.Location.Line)
                .ThenBy(finding => finding.Location.Column)
                .ThenBy(finding => finding.RuleId, StringComparer.Ordinal),
            diagnostics
                .OrderBy(diagnostic => diagnostic.RelativePath, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal),
            request.FailureThreshold,
            selectedFileCount,
            scannedFileCount,
            skippedFileCount,
            failedFileCount,
            sourceComplete && failedFileCount == 0,
            stopwatch.Elapsed);
    }
}
