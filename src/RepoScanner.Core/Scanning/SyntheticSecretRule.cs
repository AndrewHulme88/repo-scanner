namespace RepoScanner.Core;

public sealed class SyntheticSecretRule : IScanRule
{
    public const string RuleId = "RS1000";
    public const string MarkerPrefix = "REPO_SCANNER_TEST_";
    public const string Marker = MarkerPrefix + "SECRET=";

    public string Id => RuleId;

    public IReadOnlyList<Finding> Evaluate(ScanCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        List<Finding> findings = [];
        using StringReader reader = new(candidate.Content);

        int lineNumber = 0;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            int searchStart = 0;

            while (searchStart < line.Length)
            {
                int markerIndex = line.IndexOf(Marker, searchStart, StringComparison.Ordinal);

                if (markerIndex < 0)
                {
                    break;
                }

                int valueStart = markerIndex + Marker.Length;
                int valueLength = GetValueLength(line, valueStart);

                if (valueLength > 0)
                {
                    ReadOnlySpan<char> secret = line.AsSpan(valueStart, valueLength);
                    findings.Add(CreateFinding(candidate.RelativePath, lineNumber, valueStart + 1, secret));
                }

                searchStart = Math.Max(valueStart + valueLength, valueStart + 1);
            }
        }

        return findings.AsReadOnly();
    }

    private static int GetValueLength(string line, int valueStart)
    {
        int valueEnd = valueStart;

        while (valueEnd < line.Length && !IsValueTerminator(line[valueEnd]))
        {
            valueEnd++;
        }

        return valueEnd - valueStart;
    }

    private static bool IsValueTerminator(char character)
    {
        return char.IsWhiteSpace(character) || character is ';' or ',' or '\'' or '"';
    }

    private static Finding CreateFinding(
        string relativePath,
        int line,
        int column,
        ReadOnlySpan<char> secret)
    {
        return new Finding(
            RuleId,
            FindingSeverity.High,
            "Synthetic test secret detected",
            "A synthetic credential marker was found. This rule validates the safe scan pipeline.",
            new FindingLocation(relativePath, line, column),
            RedactedEvidence.FromSecret(secret),
            "Remove the synthetic secret value from the scanned file.");
    }
}
