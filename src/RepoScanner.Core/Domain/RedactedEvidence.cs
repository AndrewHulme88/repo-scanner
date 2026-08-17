namespace RepoScanner.Core;

public sealed class RedactedEvidence
{
    private const string RedactedValue = "[REDACTED]";

    private RedactedEvidence(int characterCount)
    {
        CharacterCount = characterCount;
    }

    public int CharacterCount { get; }

    public static RedactedEvidence FromSecret(ReadOnlySpan<char> secret)
    {
        if (secret.IsEmpty)
        {
            throw new ArgumentException("Secret evidence cannot be empty.", nameof(secret));
        }

        return new RedactedEvidence(secret.Length);
    }

    public override string ToString() => RedactedValue;
}
