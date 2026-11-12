using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Scanning;

public sealed class DiskScanner : IDiskScanner
{
    private readonly Func<string, IEnumerable<string>> _enumerateFileSystemEntries;
    private readonly Func<string, long> _logicalSizeReader;
    private readonly Func<string, AllocatedFileMetadata> _allocatedMetadataReader;
    private readonly Func<string, FileAttributes, DiskItemKind, DiskItemMetadata>
        _itemMetadataReader;
    private readonly Func<IReadOnlyList<EquivalentPath>> _equivalentPathReader;

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

    public DiskScanner(
        IAllocatedFileMetadataReader allocatedMetadataReader,
        IEquivalentPathProvider equivalentPathProvider)
        : this(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader:
                (allocatedMetadataReader
                    ?? throw new ArgumentNullException(nameof(allocatedMetadataReader))).Read,
            equivalentPathReader:
                (equivalentPathProvider
                    ?? throw new ArgumentNullException(nameof(equivalentPathProvider)))
                .GetEquivalentPaths)
    {
    }

    internal DiskScanner(
        Func<string, IEnumerable<string>> enumerateFileSystemEntries,
        Func<string, long>? logicalSizeReader = null,
        Func<string, AllocatedFileMetadata>? allocatedMetadataReader = null,
        Func<string, FileAttributes, DiskItemKind, DiskItemMetadata>? itemMetadataReader = null,
        Func<IReadOnlyList<EquivalentPath>>? equivalentPathReader = null)
    {
        _enumerateFileSystemEntries = enumerateFileSystemEntries;
        _logicalSizeReader = logicalSizeReader ?? (path => new FileInfo(path).Length);
        _allocatedMetadataReader = allocatedMetadataReader ?? MissingAllocatedMetadata;
        _itemMetadataReader = itemMetadataReader ?? ReadItemMetadata;
        _equivalentPathReader = equivalentPathReader ?? NoEquivalentPaths;
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
            SelectOmittedAlternates(fullRootPath));
        SetRootMetadata(root);
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
                if (state.TryOmitEquivalentAlternate(currentEntryPath))
                {
                    continue;
                }

                var outcome = PrepareEntry(currentEntryPath, options, state);
                if (outcome.Kind == EntryOutcomeKind.Skipped)
                {
                    continue;
                }

                if (outcome.Kind == EntryOutcomeKind.Failed)
                {
                    yield return state.Progress(currentEntryPath);
                    continue;
                }

                var item = outcome.Item!;
                if (outcome.Kind == EntryOutcomeKind.Directory)
                {
                    if (includeChildren)
                    {
                        directory.AddChild(item);
                    }

                    state.DirectoriesScanned++;
                    var directoryProgress = state.TryProgress(currentEntryPath);
                    if (directoryProgress is not null)
                    {
                        yield return directoryProgress;
                    }

                    await foreach (var progress in ScanDirectoryAsync(
                                       item,
                                       options,
                                       state,
                                       visitedDirectories,
                                       includeChildren && outcome.ExpandPackage,
                                       cancellationToken))
                    {
                        yield return progress;
                    }

                    directory.MeasuredSizeBytes += item.MeasuredSizeBytes;
                    directory.SizeBytes += item.SizeBytes;
                    directory.SharedSizeBytes += item.SharedSizeBytes;
                    continue;
                }

                if (includeChildren)
                {
                    directory.AddChild(item);
                }

                directory.MeasuredSizeBytes += item.MeasuredSizeBytes;
                directory.SizeBytes += item.SizeBytes;
                directory.SharedSizeBytes += item.SharedSizeBytes;
                state.FilesScanned++;
                state.BytesScanned += item.SizeBytes;
                var fileProgress = state.TryProgress(currentEntryPath);
                if (fileProgress is not null)
                {
                    yield return fileProgress;
                }
            }
        }
    }

    private EntryOutcome PrepareEntry(
        string entryPath,
        ScanOptions options,
        ScanState state)
    {
        try
        {
            var attributes = File.GetAttributes(entryPath);
            if (!options.IncludeHiddenFiles && IsHidden(entryPath, attributes))
            {
                return EntryOutcome.Skipped;
            }

            var isDirectory = (attributes & FileAttributes.Directory) != 0;
            var isSymbolicLink = (attributes & FileAttributes.ReparsePoint) != 0;
            if (isSymbolicLink && !options.FollowSymbolicLinks)
            {
                return EntryOutcome.Skipped;
            }

            var kind = ItemKind(entryPath, attributes, isDirectory);
            var item = new DiskItem(ItemName(entryPath), entryPath, isDirectory)
            {
                Metadata = _itemMetadataReader(entryPath, attributes, kind)
            };

            if (isDirectory)
            {
                return new EntryOutcome(
                    EntryOutcomeKind.Directory,
                    item,
                    options.TreatPackagesAsDirectories || !IsPackage(entryPath));
            }

            MeasureFile(item, options.MeasurementMode, state);
            return new EntryOutcome(EntryOutcomeKind.File, item);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            state.AddError(entryPath, exception);
            return EntryOutcome.Failed;
        }
    }

    private void MeasureFile(
        DiskItem file,
        StorageMeasurementMode measurementMode,
        ScanState state)
    {
        if (measurementMode == StorageMeasurementMode.Logical)
        {
            var logicalSize = _logicalSizeReader(file.Path);
            file.MeasuredSizeBytes = logicalSize;
            file.SizeBytes = logicalSize;
            return;
        }

        var metadata = _allocatedMetadataReader(file.Path);
        if (measurementMode == StorageMeasurementMode.SharedAwareAllocated)
        {
            var accounting = state.Account(metadata);
            file.MeasuredSizeBytes = metadata.AllocatedSizeBytes;
            file.SizeBytes = accounting.CountedSizeBytes;
            file.SharedSizeBytes = accounting.SharedSizeBytes;
            return;
        }

        file.MeasuredSizeBytes = metadata.AllocatedSizeBytes;
        file.SizeBytes = metadata.AllocatedSizeBytes;
    }

    private static string ItemName(string path)
    {
        var name = Path.GetFileName(path);
        return string.IsNullOrEmpty(name) ? path : name;
    }

    private enum EntryOutcomeKind
    {
        Skipped,
        Failed,
        Directory,
        File
    }

    private readonly record struct EntryOutcome(
        EntryOutcomeKind Kind,
        DiskItem? Item = null,
        bool ExpandPackage = false)
    {
        public static EntryOutcome Skipped { get; } = new(EntryOutcomeKind.Skipped);

        public static EntryOutcome Failed { get; } = new(EntryOutcomeKind.Failed);
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

    private Dictionary<string, EquivalentPath> SelectOmittedAlternates(string rootPath)
    {
        var omittedAlternates = new Dictionary<string, EquivalentPath>(PathComparer);
        IReadOnlyList<EquivalentPath> equivalentPaths;
        try
        {
            equivalentPaths = _equivalentPathReader();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return omittedAlternates;
        }

        var normalizedRoot = NormalizePath(rootPath);
        foreach (var equivalentPath in equivalentPaths)
        {
            if (string.IsNullOrWhiteSpace(equivalentPath.VisiblePath)
                || string.IsNullOrWhiteSpace(equivalentPath.AlternatePath)
                || !Path.IsPathFullyQualified(equivalentPath.VisiblePath)
                || !Path.IsPathFullyQualified(equivalentPath.AlternatePath))
            {
                continue;
            }

            var visiblePath = NormalizePath(equivalentPath.VisiblePath);
            var alternatePath = NormalizePath(equivalentPath.AlternatePath);
            if (PathComparer.Equals(alternatePath, normalizedRoot)
                || !IsWithin(alternatePath, normalizedRoot)
                || !IsWithin(visiblePath, normalizedRoot)
                || IsWithin(visiblePath, alternatePath)
                || IsWithin(alternatePath, visiblePath))
            {
                continue;
            }

            omittedAlternates.TryAdd(
                alternatePath,
                new EquivalentPath(visiblePath, alternatePath));
        }

        return omittedAlternates;
    }

    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsWithin(string path, string ancestorPath)
    {
        if (PathComparer.Equals(path, ancestorPath))
        {
            return true;
        }

        var prefix = Path.EndsInDirectorySeparator(ancestorPath)
            ? ancestorPath
            : ancestorPath + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, PathComparison);
    }

    private static IReadOnlyList<EquivalentPath> NoEquivalentPaths() => [];

    private static string GetDirectoryIdentity(string path)
    {
        var directory = new DirectoryInfo(path);
        var target = directory.LinkTarget is null
            ? directory
            : directory.ResolveLinkTarget(returnFinalTarget: true) ?? directory;

        return Path.TrimEndingDirectorySeparator(target.FullName);
    }

    private static bool IsHidden(string path, FileAttributes attributes) =>
        (attributes & FileAttributes.Hidden) != 0 ||
        Path.GetFileName(path).StartsWith(".", StringComparison.Ordinal);

    private static bool IsPackage(string path) =>
        string.Equals(Path.GetExtension(path), ".app", StringComparison.OrdinalIgnoreCase);

    private void SetRootMetadata(DiskItem root)
    {
        try
        {
            var attributes = File.GetAttributes(root.Path);
            root.Metadata = _itemMetadataReader(
                root.Path,
                attributes,
                ItemKind(root.Path, attributes, isDirectory: true));
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
        }
    }

    private static DiskItemKind ItemKind(
        string path,
        FileAttributes attributes,
        bool isDirectory)
    {
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            return DiskItemKind.SymbolicLink;
        }

        if (isDirectory && IsPackage(path))
        {
            return DiskItemKind.ApplicationBundle;
        }

        return isDirectory ? DiskItemKind.Directory : DiskItemKind.File;
    }

    private static DiskItemMetadata ReadItemMetadata(
        string path,
        FileAttributes attributes,
        DiskItemKind kind)
    {
        var info = (attributes & FileAttributes.Directory) != 0
            ? (FileSystemInfo)new DirectoryInfo(path)
            : new FileInfo(path);

        return new DiskItemMetadata(
            kind,
            attributes,
            KnownUtc(info.CreationTimeUtc),
            KnownUtc(info.LastWriteTimeUtc),
            LastAccessTimeUtc: null);
    }

    private static DateTimeOffset? KnownUtc(DateTime value)
    {
        if (value <= DateTime.FromFileTimeUtc(0))
        {
            return null;
        }

        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is UnauthorizedAccessException or IOException
            or DirectoryNotFoundException
            or FileNotFoundException;

    private static AllocatedFileMetadata MissingAllocatedMetadata(string path) =>
        throw new IOException(
            $"Allocated metadata is unavailable for '{path}' on this scanner.");

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private sealed class ScanState(
        DiskItem root,
        StorageMeasurementMode measurementMode,
        IReadOnlyDictionary<string, EquivalentPath> omittedAlternates)
    {
        private static readonly TimeSpan MinimumProgressInterval = TimeSpan.FromMilliseconds(150);
        private const long MaximumEntriesBetweenProgress = 4_096;

        private readonly List<ScanError> _errors = [];
        private readonly HashSet<FileIdentity>? _countedFileIdentities =
            measurementMode == StorageMeasurementMode.SharedAwareAllocated
                ? []
                : null;
        private readonly Dictionary<SharedDataIdentity, SharedDataAccounting>?
            _countedSharedData =
                measurementMode == StorageMeasurementMode.SharedAwareAllocated
                    ? []
                    : null;
        private IReadOnlyList<ScanError> _errorSnapshot = [];
        private readonly List<EquivalentPath> _omittedEquivalentPaths = [];
        private IReadOnlyList<EquivalentPath> _omittedEquivalentPathSnapshot = [];
        private long _entriesSinceLastProgress;
        private long _lastProgressTimestamp = Stopwatch.GetTimestamp();
        private bool _errorsChanged;
        private bool _reportedFirstEntry;
        private CloneAccountingCoverage _cloneAccountingCoverage =
            CloneAccountingCoverage.Unavailable;
        private bool _hasCloneAccountingCoverage;

        public long FilesScanned { get; set; }

        public long DirectoriesScanned { get; set; }

        public long BytesScanned { get; set; }

        public FileAccounting Account(AllocatedFileMetadata metadata)
        {
            if (_countedFileIdentities is null || _countedSharedData is null)
            {
                return new FileAccounting(
                    metadata.AllocatedSizeBytes,
                    SharedSizeBytes: 0);
            }

            AccumulateCoverage(metadata.CloneAccountingCoverage);

            if (metadata.AllocatedSizeBytes < 0)
            {
                throw new IOException("Allocated size cannot be negative.");
            }

            if (!_countedFileIdentities.Add(metadata.Identity))
            {
                return new FileAccounting(
                    CountedSizeBytes: 0,
                    metadata.AllocatedSizeBytes);
            }

            var dataAllocatedSize = metadata.DataAllocatedSizeBytes;
            if (dataAllocatedSize is < 0
                || dataAllocatedSize > metadata.AllocatedSizeBytes)
            {
                MarkCoveragePartial();
                return new FileAccounting(
                    metadata.AllocatedSizeBytes,
                    SharedSizeBytes: 0);
            }

            if (metadata.SharedDataIdentity is not { } sharedDataIdentity)
            {
                if (metadata.CloneAccountingCoverage == CloneAccountingCoverage.Available
                    && dataAllocatedSize is null)
                {
                    MarkCoveragePartial();
                }

                return new FileAccounting(
                    metadata.AllocatedSizeBytes,
                    SharedSizeBytes: 0);
            }

            if (metadata.CloneAccountingCoverage != CloneAccountingCoverage.Available
                || dataAllocatedSize is null)
            {
                MarkCoveragePartial();
                return new FileAccounting(
                    metadata.AllocatedSizeBytes,
                    SharedSizeBytes: 0);
            }

            if (!_countedSharedData.TryGetValue(sharedDataIdentity, out var sharedData))
            {
                _countedSharedData.Add(
                    sharedDataIdentity,
                    new SharedDataAccounting(dataAllocatedSize.Value, IsValid: true));
                return new FileAccounting(
                    metadata.AllocatedSizeBytes,
                    SharedSizeBytes: 0);
            }

            if (!sharedData.IsValid
                || sharedData.DataAllocatedSizeBytes != dataAllocatedSize.Value)
            {
                _countedSharedData[sharedDataIdentity] =
                    sharedData with { IsValid = false };
                MarkCoveragePartial();
                return new FileAccounting(
                    metadata.AllocatedSizeBytes,
                    SharedSizeBytes: 0);
            }

            return new FileAccounting(
                metadata.AllocatedSizeBytes - dataAllocatedSize.Value,
                dataAllocatedSize.Value);
        }

        public bool TryOmitEquivalentAlternate(string path)
        {
            if (omittedAlternates.Count == 0
                || !omittedAlternates.TryGetValue(
                    Path.TrimEndingDirectorySeparator(path),
                    out var equivalentPath))
            {
                return false;
            }

            if (!_omittedEquivalentPaths.Contains(equivalentPath))
            {
                _omittedEquivalentPaths.Add(equivalentPath);
                _omittedEquivalentPathSnapshot = _omittedEquivalentPaths.ToArray();
            }

            return true;
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
                measurementMode,
                _cloneAccountingCoverage)
            {
                OmittedEquivalentPaths = _omittedEquivalentPathSnapshot
            };
        }

        private void AccumulateCoverage(CloneAccountingCoverage coverage)
        {
            if (!_hasCloneAccountingCoverage)
            {
                _cloneAccountingCoverage = coverage;
                _hasCloneAccountingCoverage = true;
                return;
            }

            if (_cloneAccountingCoverage != coverage)
            {
                _cloneAccountingCoverage = CloneAccountingCoverage.Partial;
            }
        }

        private void MarkCoveragePartial()
        {
            _cloneAccountingCoverage = CloneAccountingCoverage.Partial;
            _hasCloneAccountingCoverage = true;
        }

        private readonly record struct SharedDataAccounting(
            long DataAllocatedSizeBytes,
            bool IsValid);

        public readonly record struct FileAccounting(
            long CountedSizeBytes,
            long SharedSizeBytes);
    }
}
