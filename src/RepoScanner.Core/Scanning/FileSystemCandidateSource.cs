using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;

namespace RepoScanner.Core;

public sealed class FileSystemCandidateSource : IScanCandidateSource
{
    private const int MinimumChannelCapacity = 4;

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly Encoding StrictUtf16LittleEndian = new UnicodeEncoding(
        bigEndian: false,
        byteOrderMark: true,
        throwOnInvalidBytes: true);

    private static readonly Encoding StrictUtf16BigEndian = new UnicodeEncoding(
        bigEndian: true,
        byteOrderMark: true,
        throwOnInvalidBytes: true);

    private static readonly Encoding StrictUtf32LittleEndian = new UTF32Encoding(
        bigEndian: false,
        byteOrderMark: true,
        throwOnInvalidCharacters: true);

    private static readonly Encoding StrictUtf32BigEndian = new UTF32Encoding(
        bigEndian: true,
        byteOrderMark: true,
        throwOnInvalidCharacters: true);

    public async IAsyncEnumerable<CandidateSourceItem> ReadAsync(
        ScanRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        FileAttributes rootAttributes = GetRootAttributes(request.Path);

        if (rootAttributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new ArgumentException(
                "The scan root cannot be a symbolic link or reparse point.",
                nameof(request));
        }

        if (!rootAttributes.HasFlag(FileAttributes.Directory))
        {
            yield return await ReadFileAsync(
                request.Path,
                Path.GetFileName(request.Path),
                request.MaximumFileSizeBytes,
                cancellationToken);
            yield break;
        }

        int pathChannelCapacity = Math.Max(
            MinimumChannelCapacity,
            request.MaximumConcurrency * 2);
        Channel<string> paths = Channel.CreateBounded<string>(
            new BoundedChannelOptions(pathChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true,
            });
        Channel<CandidateSourceItem> results = Channel.CreateBounded<CandidateSourceItem>(
            new BoundedChannelOptions(request.MaximumConcurrency)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });

        Task producer = ProducePathsAsync(
            request.Path,
            paths.Writer,
            results.Writer,
            cancellationToken);
        Task[] workers = Enumerable.Range(0, request.MaximumConcurrency)
            .Select(_ => ReadFilesAsync(
                request.Path,
                request.MaximumFileSizeBytes,
                paths.Reader,
                results.Writer,
                cancellationToken))
            .ToArray();
        Task completion = CompleteResultsAsync(producer, workers, results.Writer);

        try
        {
            await foreach (CandidateSourceItem item in results.Reader.ReadAllAsync(
                cancellationToken))
            {
                yield return item;
            }
        }
        finally
        {
            await completion;
        }
    }

    private static FileAttributes GetRootAttributes(string rootPath)
    {
        try
        {
            return File.GetAttributes(rootPath);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ArgumentException("The scan path does not exist.", nameof(rootPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ArgumentException("The scan path could not be accessed.", nameof(rootPath));
        }
    }

    private static async Task ProducePathsAsync(
        string rootPath,
        ChannelWriter<string> pathWriter,
        ChannelWriter<CandidateSourceItem> resultWriter,
        CancellationToken cancellationToken)
    {
        try
        {
            Stack<string> pendingDirectories = new();
            pendingDirectories.Push(rootPath);

            while (pendingDirectories.TryPop(out string? directoryPath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                IEnumerator<string> entries;

                try
                {
                    entries = Directory.EnumerateFileSystemEntries(directoryPath).GetEnumerator();
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    string relativePath = Path.GetRelativePath(rootPath, directoryPath);
                    await resultWriter.WriteAsync(
                        CandidateSourceItem.Information(
                            new ScanDiagnostic(
                                "RS-D007",
                                "A directory could not be enumerated.",
                                relativePath),
                            affectsCompleteness: true),
                        cancellationToken);
                    continue;
                }

                using (entries)
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string entryPath;

                        try
                        {
                            if (!entries.MoveNext())
                            {
                                break;
                            }

                            entryPath = entries.Current;
                        }
                        catch (Exception exception) when (
                            exception is IOException or UnauthorizedAccessException)
                        {
                            string relativePath = Path.GetRelativePath(rootPath, directoryPath);
                            await resultWriter.WriteAsync(
                                CandidateSourceItem.Information(
                                    new ScanDiagnostic(
                                        "RS-D007",
                                        "A directory could not be completely enumerated.",
                                        relativePath),
                                    affectsCompleteness: true),
                                cancellationToken);
                            break;
                        }

                        FileAttributes attributes;

                        try
                        {
                            attributes = File.GetAttributes(entryPath);
                        }
                        catch (Exception exception) when (
                            exception is IOException or UnauthorizedAccessException)
                        {
                            string relativePath = Path.GetRelativePath(rootPath, entryPath);
                            await resultWriter.WriteAsync(
                                CandidateSourceItem.Information(
                                    new ScanDiagnostic(
                                        "RS-D008",
                                        "A selected filesystem entry could not be inspected.",
                                        relativePath),
                                    affectsCompleteness: true),
                                cancellationToken);
                            continue;
                        }

                        bool isDirectory = attributes.HasFlag(FileAttributes.Directory);
                        bool isReparsePoint = attributes.HasFlag(FileAttributes.ReparsePoint);

                        if (isReparsePoint)
                        {
                            string relativePath = Path.GetRelativePath(rootPath, entryPath);
                            CandidateSourceItem item = isDirectory
                                ? CandidateSourceItem.Information(
                                    new ScanDiagnostic(
                                        "RS-D003",
                                        "A symbolic-link or reparse-point directory was not followed.",
                                        relativePath))
                                : CandidateSourceItem.Skipped(
                                    new ScanDiagnostic(
                                        "RS-D003",
                                        "A symbolic-link or reparse-point file was not followed.",
                                        relativePath));
                            await resultWriter.WriteAsync(item, cancellationToken);
                            continue;
                        }

                        if (isDirectory)
                        {
                            pendingDirectories.Push(entryPath);
                        }
                        else
                        {
                            await pathWriter.WriteAsync(entryPath, cancellationToken);
                        }
                    }
                }
            }

            pathWriter.TryComplete();
        }
        catch (Exception exception)
        {
            pathWriter.TryComplete(exception);
            throw;
        }
    }

    private static async Task ReadFilesAsync(
        string rootPath,
        int maximumFileSizeBytes,
        ChannelReader<string> pathReader,
        ChannelWriter<CandidateSourceItem> resultWriter,
        CancellationToken cancellationToken)
    {
        await foreach (string filePath in pathReader.ReadAllAsync(cancellationToken))
        {
            string relativePath = Path.GetRelativePath(rootPath, filePath);
            CandidateSourceItem item = await ReadFileAsync(
                filePath,
                relativePath,
                maximumFileSizeBytes,
                cancellationToken);
            await resultWriter.WriteAsync(item, cancellationToken);
        }
    }

    private static async Task<CandidateSourceItem> ReadFileAsync(
        string fullPath,
        string relativePath,
        int maximumFileSizeBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            FileInfo fileInfo = new(fullPath);

            if (fileInfo.Length > maximumFileSizeBytes)
            {
                return CandidateSourceItem.Skipped(
                    new ScanDiagnostic(
                        "RS-D004",
                        $"A file larger than the {maximumFileSizeBytes}-byte limit was skipped.",
                        relativePath));
            }

            int initialBufferSize = Math.Min(
                maximumFileSizeBytes + 1,
                Math.Max(4_096, checked((int)fileInfo.Length + 1)));
            byte[] bytes = GC.AllocateUninitializedArray<byte>(initialBufferSize);
            int bytesRead = 0;

            await using (FileStream stream = new(
                fullPath,
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                    Share = FileShare.ReadWrite | FileShare.Delete,
                }))
            {
                while (true)
                {
                    if (bytesRead == bytes.Length)
                    {
                        if (bytes.Length == maximumFileSizeBytes + 1)
                        {
                            break;
                        }

                        int expandedLength = Math.Min(
                            maximumFileSizeBytes + 1,
                            bytes.Length * 2);
                        Array.Resize(ref bytes, expandedLength);
                    }

                    int read = await stream.ReadAsync(
                        bytes.AsMemory(bytesRead, bytes.Length - bytesRead),
                        cancellationToken);

                    if (read == 0)
                    {
                        break;
                    }

                    bytesRead += read;
                }
            }

            if (bytesRead > maximumFileSizeBytes)
            {
                return CandidateSourceItem.Skipped(
                    new ScanDiagnostic(
                        "RS-D004",
                        $"A file larger than the {maximumFileSizeBytes}-byte limit was skipped.",
                        relativePath));
            }

            EncodingSelection encoding = SelectEncoding(bytes, bytesRead);

            if (encoding.IsBinary)
            {
                return CandidateSourceItem.Skipped(
                    new ScanDiagnostic(
                        "RS-D005",
                        "A binary file was skipped.",
                        relativePath));
            }

            string content;

            try
            {
                content = encoding.Encoding!.GetString(
                    bytes,
                    encoding.PreambleLength,
                    bytesRead - encoding.PreambleLength);
            }
            catch (DecoderFallbackException)
            {
                return CandidateSourceItem.Skipped(
                    new ScanDiagnostic(
                        "RS-D006",
                        "A file with unsupported or invalid text encoding was skipped.",
                        relativePath));
            }

            return CandidateSourceItem.Ready(
                new ScanCandidate(fullPath, relativePath, content));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CandidateSourceItem.Failed(
                new ScanDiagnostic(
                    "RS-D001",
                    "A selected file could not be read.",
                    relativePath));
        }
    }

    private static EncodingSelection SelectEncoding(byte[] bytes, int length)
    {
        if (HasPrefix(bytes, length, 0x00, 0x00, 0xFE, 0xFF))
        {
            return new EncodingSelection(StrictUtf32BigEndian, 4, IsBinary: false);
        }

        if (HasPrefix(bytes, length, 0xFF, 0xFE, 0x00, 0x00))
        {
            return new EncodingSelection(StrictUtf32LittleEndian, 4, IsBinary: false);
        }

        if (HasPrefix(bytes, length, 0xEF, 0xBB, 0xBF))
        {
            return new EncodingSelection(StrictUtf8, 3, IsBinary: false);
        }

        if (HasPrefix(bytes, length, 0xFE, 0xFF))
        {
            return new EncodingSelection(StrictUtf16BigEndian, 2, IsBinary: false);
        }

        if (HasPrefix(bytes, length, 0xFF, 0xFE))
        {
            return new EncodingSelection(StrictUtf16LittleEndian, 2, IsBinary: false);
        }

        bool containsNull = Array.IndexOf(bytes, (byte)0, 0, length) >= 0;
        return containsNull
            ? new EncodingSelection(null, 0, IsBinary: true)
            : new EncodingSelection(StrictUtf8, 0, IsBinary: false);
    }

    private static bool HasPrefix(byte[] bytes, int length, params byte[] prefix)
    {
        if (length < prefix.Length)
        {
            return false;
        }

        for (int index = 0; index < prefix.Length; index++)
        {
            if (bytes[index] != prefix[index])
            {
                return false;
            }
        }

        return true;
    }

    private static async Task CompleteResultsAsync(
        Task producer,
        Task[] workers,
        ChannelWriter<CandidateSourceItem> resultWriter)
    {
        try
        {
            await Task.WhenAll([producer, .. workers]);
            resultWriter.TryComplete();
        }
        catch (Exception exception)
        {
            resultWriter.TryComplete(exception);
        }
    }

    private readonly record struct EncodingSelection(
        Encoding? Encoding,
        int PreambleLength,
        bool IsBinary);
}
