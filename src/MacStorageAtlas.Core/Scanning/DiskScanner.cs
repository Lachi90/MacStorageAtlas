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
    private readonly Func<string, CloudAvailabilityReading> _cloudAvailabilityReader;

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

    public DiskScanner(
        IAllocatedFileMetadataReader allocatedMetadataReader,
        ICloudAvailabilityReader cloudAvailabilityReader)
        : this(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader:
                (allocatedMetadataReader
                    ?? throw new ArgumentNullException(nameof(allocatedMetadataReader))).Read,
            cloudAvailabilityReader:
                (cloudAvailabilityReader
                    ?? throw new ArgumentNullException(nameof(cloudAvailabilityReader)))
                .ReadCloudAvailability)
    {
    }

    public DiskScanner(
        IAllocatedFileMetadataReader allocatedMetadataReader,
        IEquivalentPathProvider equivalentPathProvider,
        ICloudAvailabilityReader cloudAvailabilityReader)
        : this(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader:
                (allocatedMetadataReader
                    ?? throw new ArgumentNullException(nameof(allocatedMetadataReader))).Read,
            equivalentPathReader:
                (equivalentPathProvider
                    ?? throw new ArgumentNullException(nameof(equivalentPathProvider)))
                .GetEquivalentPaths,
            cloudAvailabilityReader:
                (cloudAvailabilityReader
                    ?? throw new ArgumentNullException(nameof(cloudAvailabilityReader)))
                .ReadCloudAvailability)
    {
    }

    internal DiskScanner(
        Func<string, IEnumerable<string>> enumerateFileSystemEntries,
        Func<string, long>? logicalSizeReader = null,
        Func<string, AllocatedFileMetadata>? allocatedMetadataReader = null,
        Func<string, FileAttributes, DiskItemKind, DiskItemMetadata>? itemMetadataReader = null,
        Func<IReadOnlyList<EquivalentPath>>? equivalentPathReader = null,
        Func<string, CloudAvailabilityReading>? cloudAvailabilityReader = null)
    {
        _enumerateFileSystemEntries = enumerateFileSystemEntries;
        _logicalSizeReader = logicalSizeReader ?? (path => new FileInfo(path).Length);
        _allocatedMetadataReader = allocatedMetadataReader ?? MissingAllocatedMetadata;
        _itemMetadataReader = itemMetadataReader ?? ReadItemMetadata;
        _equivalentPathReader = equivalentPathReader ?? NoEquivalentPaths;
        _cloudAvailabilityReader = cloudAvailabilityReader ?? UnknownCloudAvailability;
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
            ? new HashSet<string>(ScanPath.Comparer)
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

    public IAsyncEnumerable<ScanProgress> ScanSubtreeAsync(
        string scanRootPath,
        string subtreePath,
        ScanOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scanRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(subtreePath);

        options ??= ScanOptions.Default;
        if (!Enum.IsDefined(options.MeasurementMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MeasurementMode,
                "The storage measurement mode is not supported.");
        }

        if (options.MeasurementMode == StorageMeasurementMode.SharedAwareAllocated)
        {
            throw new NotSupportedException(
                "A folder cannot be rescanned on its own in shared-aware allocated mode, "
                + "because hardlinks and clones are attributed across the whole scan.");
        }

        if (options.FollowSymbolicLinks)
        {
            throw new NotSupportedException(
                "A folder cannot be rescanned on its own while symbolic links are followed, "
                + "because folders reached through links depend on the paths visited before them.");
        }

        var fullRootPath = ScanPath.Normalize(scanRootPath);
        var fullSubtreePath = ScanPath.Normalize(subtreePath);
        if (!ScanPath.IsWithin(fullSubtreePath, fullRootPath))
        {
            throw new ArgumentException(
                "The folder is not inside the scan root.",
                nameof(subtreePath));
        }

        if (ScanPath.Comparer.Equals(fullSubtreePath, fullRootPath))
        {
            return ScanAsync(scanRootPath, options, cancellationToken);
        }

        var omittedAlternates = SelectOmittedAlternates(Path.GetFullPath(scanRootPath));
        if (omittedAlternates.Keys.Any(alternate => ScanPath.IsWithin(fullSubtreePath, alternate))
            || !IsTraversedByFullScan(fullRootPath, fullSubtreePath, options))
        {
            throw new ArgumentException(
                "A full scan of the scan root does not include the folder.",
                nameof(subtreePath));
        }

        return ScanSubtreeCoreAsync(
            fullSubtreePath,
            options,
            omittedAlternates,
            cancellationToken);
    }

    private async IAsyncEnumerable<ScanProgress> ScanSubtreeCoreAsync(
        string subtreePath,
        ScanOptions options,
        IReadOnlyDictionary<string, EquivalentPath> omittedAlternates,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var placeholder = new DiskItem(ItemName(subtreePath), subtreePath, isDirectory: true);
        var state = new ScanState(placeholder, options.MeasurementMode, omittedAlternates);

        if (!Directory.Exists(subtreePath) && !File.Exists(subtreePath))
        {
            yield return state.Progress(subtreePath, isCompleted: true) with
            {
                IsRootPresent = false
            };
            yield break;
        }

        var outcome = PrepareEntry(subtreePath, options, state);
        if (outcome.Kind != EntryOutcomeKind.Directory)
        {
            yield return state.Progress(subtreePath, isCompleted: true) with
            {
                IsRootPresent = false
            };
            yield break;
        }

        var subtree = outcome.Item!;
        state.Root = subtree;
        state.DirectoriesScanned++;
        yield return state.Progress(subtreePath);

        await foreach (var progress in ScanDirectoryAsync(
                           subtree,
                           options,
                           state,
                           visitedDirectories: null,
                           includeChildren: outcome.ExpandPackage,
                           cancellationToken))
        {
            yield return progress;
        }

        cancellationToken.ThrowIfCancellationRequested();
        DiskItemSorter.SortBySizeDescending(subtree);
        yield return state.Progress(subtreePath, isCompleted: true);
    }

    private static bool IsTraversedByFullScan(
        string rootPath,
        string subtreePath,
        ScanOptions options)
    {
        var ancestor = Path.GetDirectoryName(subtreePath);
        while (ancestor is not null
               && !ScanPath.Comparer.Equals(Path.TrimEndingDirectorySeparator(ancestor), rootPath))
        {
            if (!options.TreatPackagesAsDirectories && IsPackage(ancestor))
            {
                return false;
            }

            if (!options.IncludeHiddenFiles
                && Path.GetFileName(ancestor).StartsWith(".", StringComparison.Ordinal))
            {
                return false;
            }

            if (TryGetAttributes(ancestor) is { } attributes
                && ((!options.IncludeHiddenFiles && (attributes & FileAttributes.Hidden) != 0)
                    || (attributes & FileAttributes.ReparsePoint) != 0))
            {
                return false;
            }

            ancestor = Path.GetDirectoryName(ancestor);
        }

        return ancestor is not null;
    }

    private static FileAttributes? TryGetAttributes(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return null;
        }
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

        if (directory.IsUnlistedCloudFolder)
        {
            directory.SetCloudTotals(CloudAvailabilityTotals.ForUnlistedCloudFolder());
            state.AddUnlistedCloudFolder(directory.Path);
            yield break;
        }

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
                    directory.AddCloudTotals(item.CloudTotals);
                    continue;
                }

                if (includeChildren)
                {
                    directory.AddChild(item);
                }

                directory.MeasuredSizeBytes += item.MeasuredSizeBytes;
                directory.SizeBytes += item.SizeBytes;
                directory.SharedSizeBytes += item.SharedSizeBytes;
                directory.AddCloudTotals(item.CloudTotals);
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
                item.Metadata = item.Metadata with
                {
                    CloudAvailability = ReadCloudAvailability(entryPath).Availability
                };
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
            var reading = ReadCloudAvailability(file.Path);
            var logicalSize = reading.Availability != CloudAvailability.Unknown
                              && file.Metadata.Kind != DiskItemKind.SymbolicLink
                ? reading.LogicalSizeBytes
                : _logicalSizeReader(file.Path);
            file.MeasuredSizeBytes = logicalSize;
            file.SizeBytes = logicalSize;
            ApplyFileCloudAvailability(file, reading);
            return;
        }

        var metadata = _allocatedMetadataReader(file.Path);
        if (measurementMode == StorageMeasurementMode.SharedAwareAllocated)
        {
            var accounting = state.Account(metadata);
            file.MeasuredSizeBytes = metadata.AllocatedSizeBytes;
            file.SizeBytes = accounting.CountedSizeBytes;
            file.SharedSizeBytes = accounting.SharedSizeBytes;
        }
        else
        {
            file.MeasuredSizeBytes = metadata.AllocatedSizeBytes;
            file.SizeBytes = metadata.AllocatedSizeBytes;
        }

        ApplyFileCloudAvailability(
            file,
            metadata.CloudAvailability ?? ReadCloudAvailability(file.Path));
    }

    private static void ApplyFileCloudAvailability(
        DiskItem file,
        CloudAvailabilityReading reading)
    {
        file.Metadata = file.Metadata with { CloudAvailability = reading.Availability };
        if (reading.Availability == CloudAvailability.CloudOnly)
        {
            file.SetCloudTotals(
                CloudAvailabilityTotals.ForFile(
                    CloudAvailability.CloudOnly,
                    reading.LogicalSizeBytes));
        }
    }

    private CloudAvailabilityReading ReadCloudAvailability(string path)
    {
        try
        {
            return _cloudAvailabilityReader(path);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return CloudAvailabilityReading.Unknown;
        }
    }

    private static CloudAvailabilityReading UnknownCloudAvailability(string path) =>
        CloudAvailabilityReading.Unknown;

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
        var omittedAlternates = new Dictionary<string, EquivalentPath>(ScanPath.Comparer);
        IReadOnlyList<EquivalentPath> equivalentPaths;
        try
        {
            equivalentPaths = _equivalentPathReader();
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return omittedAlternates;
        }

        var normalizedRoot = ScanPath.Normalize(rootPath);
        foreach (var equivalentPath in equivalentPaths)
        {
            if (string.IsNullOrWhiteSpace(equivalentPath.VisiblePath)
                || string.IsNullOrWhiteSpace(equivalentPath.AlternatePath)
                || !Path.IsPathFullyQualified(equivalentPath.VisiblePath)
                || !Path.IsPathFullyQualified(equivalentPath.AlternatePath))
            {
                continue;
            }

            var visiblePath = ScanPath.Normalize(equivalentPath.VisiblePath);
            var alternatePath = ScanPath.Normalize(equivalentPath.AlternatePath);
            if (ScanPath.Comparer.Equals(alternatePath, normalizedRoot)
                || !ScanPath.IsWithin(alternatePath, normalizedRoot)
                || !ScanPath.IsWithin(visiblePath, normalizedRoot)
                || ScanPath.IsWithin(visiblePath, alternatePath)
                || ScanPath.IsWithin(alternatePath, visiblePath))
            {
                continue;
            }

            omittedAlternates.TryAdd(
                alternatePath,
                new EquivalentPath(visiblePath, alternatePath));
        }

        return omittedAlternates;
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
                ItemKind(root.Path, attributes, isDirectory: true)) with
            {
                CloudAvailability = ReadCloudAvailability(root.Path).Availability
            };
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

    private sealed class ScanState(
        DiskItem root,
        StorageMeasurementMode measurementMode,
        IReadOnlyDictionary<string, EquivalentPath> omittedAlternates)
    {
        public DiskItem Root { get; set; } = root;

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
        private readonly List<string> _unlistedCloudFolders = [];
        private IReadOnlyList<string> _unlistedCloudFolderSnapshot = [];
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

        public void AddUnlistedCloudFolder(string path)
        {
            _unlistedCloudFolders.Add(path);
            _unlistedCloudFolderSnapshot = _unlistedCloudFolders.ToArray();
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
                Root,
                _errorSnapshot,
                isCompleted,
                measurementMode,
                _cloneAccountingCoverage)
            {
                OmittedEquivalentPaths = _omittedEquivalentPathSnapshot,
                UnlistedCloudFolders = _unlistedCloudFolderSnapshot
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
