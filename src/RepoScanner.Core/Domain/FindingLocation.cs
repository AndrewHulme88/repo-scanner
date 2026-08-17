namespace RepoScanner.Core;

public sealed record FindingLocation
{
    public FindingLocation(string relativePath, int line, int column)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);

        RelativePath = relativePath;
        Line = line;
        Column = column;
    }

    public string RelativePath { get; }

    public int Line { get; }

    public int Column { get; }
}
