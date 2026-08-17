namespace RepoScanner.Core;

public sealed class Finding
{
    public Finding(
        string ruleId,
        FindingSeverity severity,
        string title,
        string explanation,
        FindingLocation location,
        RedactedEvidence evidence,
        string remediation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(remediation);

        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(severity),
                severity,
                "Finding severity must be defined.");
        }

        RuleId = ruleId;
        Severity = severity;
        Title = title;
        Explanation = explanation;
        Location = location;
        Evidence = evidence;
        Remediation = remediation;
    }

    public string RuleId { get; }

    public FindingSeverity Severity { get; }

    public string Title { get; }

    public string Explanation { get; }

    public FindingLocation Location { get; }

    public RedactedEvidence Evidence { get; }

    public string Remediation { get; }

    public override string ToString()
    {
        return $"{Severity} {RuleId} at {Location.RelativePath}:" +
            $"{Location.Line}:{Location.Column} ({Evidence})";
    }
}
