namespace RepoScanner.Core;

public sealed record ScanDiagnostic
{
    public ScanDiagnostic(string code, string message, string? relativePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
        RelativePath = relativePath;
    }

    public string Code { get; }

    public string Message { get; }

    public string? RelativePath { get; }
}
