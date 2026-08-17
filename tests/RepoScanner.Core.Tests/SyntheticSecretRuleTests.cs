namespace RepoScanner.Core.Tests;

public sealed class SyntheticSecretRuleTests
{
    [Fact]
    public void EvaluateReturnsSafeFindingForSyntheticMarker()
    {
        const string secret = "not-a-real-credential";
        ScanCandidate candidate = new(
            "/temporary/config.txt",
            "config.txt",
            $"name=value\n{SyntheticSecretRule.Marker}{secret}\n");
        SyntheticSecretRule rule = new();

        Finding finding = Assert.Single(rule.Evaluate(candidate));

        Assert.Equal(SyntheticSecretRule.RuleId, finding.RuleId);
        Assert.Equal(FindingSeverity.High, finding.Severity);
        Assert.Equal(new FindingLocation("config.txt", 2, 26), finding.Location);
        Assert.Equal(secret.Length, finding.Evidence.CharacterCount);
        Assert.DoesNotContain(secret, finding.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(SyntheticSecretRule.Marker)]
    [InlineData("repo_scanner_test_secret=not-a-real-credential")]
    [InlineData("REPO_SCANNER_TEST_SECRET =not-a-real-credential")]
    public void EvaluateDoesNotMatchInvalidMarkers(string content)
    {
        ScanCandidate candidate = new("/temporary/sample.txt", "sample.txt", content);
        SyntheticSecretRule rule = new();

        Assert.Empty(rule.Evaluate(candidate));
    }
}
