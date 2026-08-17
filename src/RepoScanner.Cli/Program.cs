using RepoScanner.Cli;

using CancellationTokenSource cancellationSource = new();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

return await CliApplication.RunAsync(
    args,
    Console.Out,
    Console.Error,
    cancellationSource.Token);
