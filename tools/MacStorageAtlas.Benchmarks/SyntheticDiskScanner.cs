using System.Runtime.CompilerServices;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Benchmarks;

public sealed class SyntheticDiskScanner : IDiskScanner
{
    private const long BytesPerFile = 1024;
    private const int DefaultFilesPerDirectory = 4096;
    private const int MaximumProgressEntries = 4096;
    private readonly long _fileCount;
    private readonly int _filesPerDirectory;

    public SyntheticDiskScanner(long fileCount, int filesPerDirectory = DefaultFilesPerDirectory)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fileCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(filesPerDirectory);

        _fileCount = fileCount;
        _filesPerDirectory = filesPerDirectory;
    }

    public string RootPath { get; } = "/synthetic/mac-storage-atlas";

    public long PathsMaterializedBeforeScan => 0;

    public async IAsyncEnumerable<ScanProgress> ScanAsync(
        string rootPath,
        ScanOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= ScanOptions.Default;
        var root = new DiskItem("synthetic", RootPath, isDirectory: true);
        var filesScanned = 0L;
        var directoriesScanned = 1L;
        var bytesScanned = 0L;
        var progressEntries = 0L;

        yield return Progress(
            root,
            RootPath,
            filesScanned,
            directoriesScanned,
            bytesScanned,
            options,
            isCompleted: false);

        DiskItem? directory = null;
        for (var index = 0L; index < _fileCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (index % _filesPerDirectory == 0)
            {
                directory = new DiskItem(
                    $"group-{index / _filesPerDirectory:D6}",
                    $"{RootPath}/group-{index / _filesPerDirectory:D6}",
                    isDirectory: true);
                root.AddChild(directory);
                directoriesScanned++;
            }

            var countedSize = options.MeasurementMode == StorageMeasurementMode.Logical
                ? BytesPerFile
                : BytesPerFile;
            var file = new DiskItem(
                $"file-{index:D9}.bin",
                $"{directory!.Path}/file-{index:D9}.bin",
                isDirectory: false)
            {
                SizeBytes = countedSize,
                MeasuredSizeBytes = countedSize
            };
            directory.AddChild(file);
            directory.SizeBytes += countedSize;
            directory.MeasuredSizeBytes += countedSize;
            root.SizeBytes += countedSize;
            root.MeasuredSizeBytes += countedSize;
            filesScanned++;
            bytesScanned += countedSize;
            progressEntries++;

            if (progressEntries >= MaximumProgressEntries)
            {
                progressEntries = 0;
                await Task.Yield();
                yield return Progress(
                    root,
                    file.Path,
                    filesScanned,
                    directoriesScanned,
                    bytesScanned,
                    options,
                    isCompleted: false);
            }
        }

        yield return Progress(
            root,
            RootPath,
            filesScanned,
            directoriesScanned,
            bytesScanned,
            options,
            isCompleted: true);
    }

    private static ScanProgress Progress(
        DiskItem root,
        string currentPath,
        long filesScanned,
        long directoriesScanned,
        long bytesScanned,
        ScanOptions options,
        bool isCompleted) =>
        new(
            currentPath,
            filesScanned,
            directoriesScanned,
            bytesScanned,
            root,
            Errors: [],
            isCompleted,
            options.MeasurementMode,
            CloneAccountingCoverage.Unavailable);
}
