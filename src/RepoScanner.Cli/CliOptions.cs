using RepoScanner.Core;

namespace RepoScanner.Cli;

internal sealed record CliOptions(string Path, FindingSeverity FailureThreshold)
{
    public static CliParseResult Parse(string[] args)
    {
        if (args.Length == 0 || args.Any(IsHelpOption))
        {
            return CliParseResult.Help();
        }

        if (!string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            return CliParseResult.Error($"Unknown command '{args[0]}'.");
        }

        string? path = null;
        FindingSeverity threshold = FindingSeverity.High;

        for (int index = 1; index < args.Length; index++)
        {
            string argument = args[index];

            if (string.Equals(argument, "--fail-on", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Length)
                {
                    return CliParseResult.Error("Option '--fail-on' requires a severity.");
                }

                if (!TryParseSeverity(args[index], out FindingSeverity parsedThreshold))
                {
                    return CliParseResult.Error(
                        $"Invalid severity '{args[index]}'. Use low, medium, high, or critical.");
                }

                threshold = parsedThreshold;
                continue;
            }

            if (argument.StartsWith('-'))
            {
                return CliParseResult.Error($"Unknown option '{argument}'.");
            }

            if (path is not null)
            {
                return CliParseResult.Error("Only one scan path may be specified.");
            }

            path = argument;
        }

        return CliParseResult.Success(
            new CliOptions(path ?? Directory.GetCurrentDirectory(), threshold));
    }

    private static bool IsHelpOption(string argument)
    {
        return argument is "-h" or "--help";
    }

    private static bool TryParseSeverity(string value, out FindingSeverity severity)
    {
        if (string.Equals(value, "low", StringComparison.OrdinalIgnoreCase))
        {
            severity = FindingSeverity.Low;
            return true;
        }

        if (string.Equals(value, "medium", StringComparison.OrdinalIgnoreCase))
        {
            severity = FindingSeverity.Medium;
            return true;
        }

        if (string.Equals(value, "high", StringComparison.OrdinalIgnoreCase))
        {
            severity = FindingSeverity.High;
            return true;
        }

        if (string.Equals(value, "critical", StringComparison.OrdinalIgnoreCase))
        {
            severity = FindingSeverity.Critical;
            return true;
        }

        severity = default;
        return false;
    }
}

internal sealed record CliParseResult(
    CliOptions? Options,
    string? ErrorMessage,
    bool ShowHelp)
{
    public static CliParseResult Success(CliOptions options) => new(options, null, false);

    public static CliParseResult Error(string message) => new(null, message, false);

    public static CliParseResult Help() => new(null, null, true);
}
