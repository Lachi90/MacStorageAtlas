using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace MacStorageAtlas.Core;

public sealed class DiskScanner : IDiskScanner
{
    private readonly Func<string, IEnumerable<string>> _enumerateFileSystemEntries;
    private readonly Func<string, long> _logicalSizeReader;
    private readonly Func<string, AllocatedFileMetadata> _allocatedMetadataReader;

    public DiskScanner()
        : this(Directory.EnumerateFileSystemEntries)
    {
    }

    public DiskScanner(IAllocatedFileMetadataReader allocatedMetadataReader)
        : this(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader:
                (allocatedMetadataReader
                    ?? throw new ArgumentNullException(nameof(allocatedMetadataReader))).Read)
    {
    }

    internal DiskScanner(
        Func<string, IEnumerable<string>> enumerateFileSystemEntries,
        Func<string, long>? logicalSizeReader = null,
        Func<string, AllocatedFileMetadata>? allocatedMetadataReader = null)
    {
        _enumerateFileSystemEntries = enumerateFileSystemEntries;
        _logicalSizeReader = logicalSizeReader ?? (path => new FileInfo(path).Length);
        _allocatedMetadataReader = allocatedMetadataReader ?? MissingAllocatedMetadata;
    }

    public async IAsyncEnumerable<ScanProgress> ScanAsync(
        string rootPath,
        ScanOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        options ??= ScanOptions.Default;
        var fullRootPath = Path.GetFullPath(rootPath);
        var rootName = new DirectoryInfo(fullRootPath).Name;
        if (string.IsNullOrEmpty(rootName))
        {
            rootName = fullRootPath;
        }

        var root = new DiskItem(rootName, fullRootPath, isDirectory: true);
        var measurementMode = options.MeasurementMode;
        if (!Enum.IsDefined(measurementMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                measurementMode,
                "The storage measurement mode is not supported.");
        }

        var state = new ScanState(
            root,
            measurementMode,
            options.FollowSymbolicLinks);
        var visitedDirectories = options.FollowSymbolicLinks
            ? new HashSet<string>(PathComparer)
            : null;

        cancellationToken.ThrowIfCancellationRequested();
        state.DirectoriesScanned++;
        yield return state.Progress(fullRootPath);

        await foreach (var progress in ScanDirectoryAsync(
                           root,
                           options,
                           state,
                           visitedDirectories,
                           includeChildren: true,
                           cancellationToken))
        {
            yield return progress;
        }

        cancellationToken.ThrowIfCancellationRequested();
        DiskItemSorter.SortBySizeDescending(root);
        yield return state.Progress(fullRootPath, isCompleted: true);
    }

    private async IAsyncEnumerable<ScanProgress> ScanDirectoryAsync(
        DiskItem directory,
        ScanOptions options,
        ScanState state,
        HashSet<string>? visitedDirectories,
        bool includeChildren,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (visitedDirectories is not null)
        {
            string? identity = null;
            Exception? recoverableError = null;
            try
            {
                identity = GetDirectoryIdentity(directory.Path);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                recoverableError = exception;
            }

            if (recoverableError is not null)
            {
                state.AddError(directory.Path, recoverableError);
                yield return state.Progress(directory.Path);
                yield break;
            }

            if (!visitedDirectories.Add(identity!))
            {
                yield break;
            }
        }

        var enumerator = CreateEntryEnumerator(directory.Path, state, out var enumerationErrorProgress);
        if (enumerationErrorProgress is not null)
        {
            yield return enumerationErrorProgress;
            yield break;
        }

        if (enumerator is null)
        {
            yield break;
        }

        using (enumerator)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hasNext = TryMoveNext(enumerator, directory.Path, state, out var entryPath, out enumerationErrorProgress);
                if (enumerationErrorProgress is not null)
                {
                    yield return enumerationErrorProgress;
                    yield break;
                }

                if (!hasNext)
                {
                    break;
                }
                var currentEntryPath = entryPath!;

                FileAttributes attributes = default;
                Exception? recoverableError = null;
                try
                {
                    attributes = File.GetAttributes(currentEntryPath);
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    recoverableError = exception;
                }

                if (recoverableError is not null)
                {
                    state.AddError(currentEntryPath, recoverableError);
                    yield return state.Progress(currentEntryPath);
                    continue;
                }

                if (!options.IncludeHiddenFiles && IsHidden(currentEntryPath, attributes))
                {
                    continue;
                }

                var isDirectory = attributes.HasFlag(FileAttributes.Directory);
                var isSymbolicLink = attributes.HasFlag(FileAttributes.ReparsePoint);
                if (isSymbolicLink && !options.FollowSymbolicLinks)
                {
                    continue;
                }

                if (isDirectory)
                {
                    var child = new DiskItem(Path.GetFileName(currentEntryPath), currentEntryPath, isDirectory: true);
                    if (includeChildren)
                    {
                        directory.AddChild(child);
                    }

                    state.DirectoriesScanned++;
                    var directoryProgress = state.TryProgress(currentEntryPath);
                    if (directoryProgress is not null)
                    {
                        yield return directoryProgress;
                    }

                    var expandPackage = options.TreatPackagesAsDirectories || !IsPackage(currentEntryPath);
                    await foreach (var progress in ScanDirectoryAsync(
                                       child,
                                       options,
                                       state,
                                       visitedDirectories,
                                       includeChildren && expandPackage,
                                       cancellationToken))
                    {
                        yield return progress;
                    }

                    directory.MeasuredSizeBytes += child.MeasuredSizeBytes;
                    directory.SizeBytes += child.SizeBytes;
                    continue;
                }

                long measuredSize = 0;
                long countedSize = 0;
                var isSizeCountedElsewhere = false;
                recoverableError = null;
                try
                {
                    if (options.MeasurementMode == StorageMeasurementMode.Logical)
                    {
                        measuredSize = _logicalSizeReader(currentEntryPath);
                        countedSize = measuredSize;
                    }
                    else
                    {
                        var metadata = _allocatedMetadataReader(currentEntryPath);
                        measuredSize = metadata.AllocatedSizeBytes;
                        isSizeCountedElsewhere =
                            options.MeasurementMode == StorageMeasurementMode.HardlinkAwareAllocated
                            && state.IsIdentityCounted(metadata);
                        countedSize = isSizeCountedElsewhere ? 0 : measuredSize;
                    }
                }
                catch (Exception exception) when (IsRecoverable(exception))
                {
                    recoverableError = exception;
                }

                if (recoverableError is not null)
                {
                    state.AddError(currentEntryPath, recoverableError);
                    yield return state.Progress(currentEntryPath);
                    continue;
                }

                var file = new DiskItem(Path.GetFileName(currentEntryPath), currentEntryPath, isDirectory: false)
                {
                    SizeBytes = countedSize,
                    MeasuredSizeBytes = measuredSize,
                    IsSizeCountedElsewhere = isSizeCountedElsewhere
                };
                if (includeChildren)
                {
                    directory.AddChild(file);
                }

                directory.MeasuredSizeBytes += measuredSize;
                directory.SizeBytes += countedSize;
                state.FilesScanned++;
                state.BytesScanned += countedSize;
                var fileProgress = state.TryProgress(currentEntryPath);
                if (fileProgress is not null)
                {
                    yield return fileProgress;
                }
            }
        }
    }

    private IEnumerator<string>? CreateEntryEnumerator(
        string directoryPath,
        ScanState state,
        out ScanProgress? errorProgress)
    {
        errorProgress = null;
        try
        {
            return _enumerateFileSystemEntries(directoryPath).GetEnumerator();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            state.AddError(directoryPath, exception);
            errorProgress = state.Progress(directoryPath);
            return null;
        }
    }

    private static bool TryMoveNext(
        IEnumerator<string> enumerator,
        string directoryPath,
        ScanState state,
        out string? entryPath,
        out ScanProgress? errorProgress)
    {
        entryPath = null;
        errorProgress = null;
        try
        {
            if (!enumerator.MoveNext())
            {
                return false;
            }

            entryPath = enumerator.Current;
            return true;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            state.AddError(directoryPath, exception);
            errorProgress = state.Progress(directoryPath);
            return false;
        }
    }

    private static string GetDirectoryIdentity(string path)
    {
        var directory = new DirectoryInfo(path);
        var target = directory.LinkTarget is null
            ? directory
            : directory.ResolveLinkTarget(returnFinalTarget: true) ?? directory;

        return Path.TrimEndingDirectorySeparator(target.FullName);
    }

    private static bool IsHidden(string path, FileAttributes attributes) =>
        attributes.HasFlag(FileAttributes.Hidden) ||
        Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal);

    private static bool IsPackage(string path) =>
        string.Equals(Path.GetExtension(path), ".app", StringComparison.OrdinalIgnoreCase);

    private static bool IsRecoverable(Exception exception) =>
        exception is UnauthorizedAccessException or IOException;

    private static AllocatedFileMetadata MissingAllocatedMetadata(string path) =>
        throw new IOException(
            $"Allocated metadata is unavailable for '{path}' on this scanner.");

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private sealed class ScanState(
        DiskItem root,
        StorageMeasurementMode measurementMode,
        bool followSymbolicLinks)
    {
        private static readonly TimeSpan MinimumProgressInterval = TimeSpan.FromMilliseconds(150);
        private const long MaximumEntriesBetweenProgress = 4_096;

        private readonly List<ScanError> _errors = [];
        private readonly HashSet<FileIdentity>? _countedFileIdentities =
            measurementMode == StorageMeasurementMode.HardlinkAwareAllocated
                ? []
                : null;
        private IReadOnlyList<ScanError> _errorSnapshot = [];
        private long _entriesSinceLastProgress;
        private long _lastProgressTimestamp = Stopwatch.GetTimestamp();
        private bool _errorsChanged;
        private bool _reportedFirstEntry;

        public long FilesScanned { get; set; }

        public long DirectoriesScanned { get; set; }

        public long BytesScanned { get; set; }

        public bool IsIdentityCounted(AllocatedFileMetadata metadata)
        {
            if (_countedFileIdentities is null)
            {
                return false;
            }

            if (!followSymbolicLinks && metadata.LinkCount <= 1)
            {
                return false;
            }

            return !_countedFileIdentities.Add(metadata.Identity);
        }

        public void AddError(string path, Exception exception)
        {
            _errors.Add(new ScanError(path, exception.Message, exception.GetType().Name));
            _errorsChanged = true;
        }

        public ScanProgress? TryProgress(string currentPath)
        {
            _entriesSinceLastProgress++;
            if (!_reportedFirstEntry)
            {
                _reportedFirstEntry = true;
                return Progress(currentPath);
            }

            if (_entriesSinceLastProgress < MaximumEntriesBetweenProgress
                && Stopwatch.GetElapsedTime(_lastProgressTimestamp) < MinimumProgressInterval)
            {
                return null;
            }

            return Progress(currentPath);
        }

        public ScanProgress Progress(string currentPath, bool isCompleted = false) =>
            CreateProgress(currentPath, isCompleted);

        private ScanProgress CreateProgress(string currentPath, bool isCompleted)
        {
            _entriesSinceLastProgress = 0;
            _lastProgressTimestamp = Stopwatch.GetTimestamp();

            if (_errorsChanged)
            {
                _errorSnapshot = _errors.ToArray();
                _errorsChanged = false;
            }

            return new ScanProgress(
                currentPath,
                FilesScanned,
                DirectoriesScanned,
                BytesScanned,
                root,
                _errorSnapshot,
                isCompleted,
                measurementMode);
        }
    }
}
