namespace RepoScanner.Core.Tests;

public sealed class RedactedEvidenceTests
{
    [Fact]
    public void FromSecretRetainsOnlyLengthAndRedactedDisplay()
    {
        const string secret = "synthetic-value-123";

        RedactedEvidence evidence = RedactedEvidence.FromSecret(secret);

        Assert.Equal(secret.Length, evidence.CharacterCount);
        Assert.Equal("[REDACTED]", evidence.ToString());
        Assert.DoesNotContain(secret, evidence.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void FromSecretRejectsEmptyEvidence()
    {
        Assert.Throws<ArgumentException>(() => RedactedEvidence.FromSecret([]));
    }
}
