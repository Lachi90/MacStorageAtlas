using System.IO;
using System.Runtime.CompilerServices;

namespace MacStorageAtlas.Core;

public sealed class DiskScanner : IDiskScanner
{
    private readonly Func<string, string[]> _getFileSystemEntries;

    public DiskScanner()
        : this(Directory.GetFileSystemEntries)
    {
    }

    internal DiskScanner(Func<string, string[]> getFileSystemEntries)
    {
        _getFileSystemEntries = getFileSystemEntries;
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
        var state = new ScanState(root);
        var visitedDirectories = new HashSet<string>(PathComparer);

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
        HashSet<string> visitedDirectories,
        bool includeChildren,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        string[]? entries = null;
        recoverableError = null;
        try
        {
            entries = _getFileSystemEntries(directory.Path);
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

        foreach (var entryPath in entries!)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();

            FileAttributes attributes = default;
            recoverableError = null;
            try
            {
                attributes = File.GetAttributes(entryPath);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                recoverableError = exception;
            }

            if (recoverableError is not null)
            {
                state.AddError(entryPath, recoverableError);
                yield return state.Progress(entryPath);
                continue;
            }

            if (!options.IncludeHiddenFiles && IsHidden(entryPath, attributes))
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
                var child = new DiskItem(Path.GetFileName(entryPath), entryPath, isDirectory: true);
                if (includeChildren)
                {
                    directory.AddChild(child);
                }

                state.DirectoriesScanned++;
                yield return state.Progress(entryPath);

                var expandPackage = options.TreatPackagesAsDirectories || !IsPackage(entryPath);
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

                directory.SizeBytes += child.SizeBytes;
                continue;
            }

            long length = 0;
            recoverableError = null;
            try
            {
                length = new FileInfo(entryPath).Length;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                recoverableError = exception;
            }

            if (recoverableError is not null)
            {
                state.AddError(entryPath, recoverableError);
                yield return state.Progress(entryPath);
                continue;
            }

            var file = new DiskItem(Path.GetFileName(entryPath), entryPath, isDirectory: false)
            {
                SizeBytes = length
            };
            if (includeChildren)
            {
                directory.AddChild(file);
            }

            directory.SizeBytes += length;
            state.FilesScanned++;
            state.BytesScanned += length;
            yield return state.Progress(entryPath);
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

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private sealed class ScanState(DiskItem root)
    {
        private readonly List<ScanError> _errors = [];

        public long FilesScanned { get; set; }

        public long DirectoriesScanned { get; set; }

        public long BytesScanned { get; set; }

        public void AddError(string path, Exception exception) =>
            _errors.Add(new ScanError(path, exception.Message, exception.GetType().Name));

        public ScanProgress Progress(string currentPath, bool isCompleted = false) =>
            new(
                currentPath,
                FilesScanned,
                DirectoriesScanned,
                BytesScanned,
                root,
                _errors.ToArray(),
                isCompleted);
    }
}
