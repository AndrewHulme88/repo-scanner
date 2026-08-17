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

    public static RepositoryScanner CreatePhaseOneScanner()
    {
        return new RepositoryScanner(
            new PhaseOneCandidateSource(),
            [new SyntheticSecretRule()]);
    }

    public async Task<ScanResult> ScanAsync(
        ScanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Stopwatch stopwatch = Stopwatch.StartNew();
        CandidateSourceResult sourceResult = await _candidateSource.ReadAsync(
            request,
            cancellationToken);

        List<Finding> findings = [];
        List<ScanDiagnostic> diagnostics = [.. sourceResult.Diagnostics];
        bool isComplete = sourceResult.IsComplete;

        foreach (ScanCandidate candidate in sourceResult.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                    isComplete = false;
                }
            }
        }

        stopwatch.Stop();

        return new ScanResult(
            findings
                .OrderBy(finding => finding.Location.RelativePath, StringComparer.Ordinal)
                .ThenBy(finding => finding.Location.Line)
                .ThenBy(finding => finding.Location.Column)
                .ThenBy(finding => finding.RuleId, StringComparer.Ordinal),
            diagnostics,
            request.FailureThreshold,
            sourceResult.Candidates.Count,
            isComplete,
            stopwatch.Elapsed);
    }
}
