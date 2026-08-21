using System.Runtime.InteropServices;
using MacStorageAtlas.Core.Cleanup;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacTrashService : ITrashService
{
    private readonly ITrashItemMover _mover;

    public MacTrashService()
        : this(new NativeTrashItemMover())
    {
    }

    internal MacTrashService(ITrashItemMover mover)
    {
        _mover = mover ?? throw new ArgumentNullException(nameof(mover));
    }

    public async Task MoveToTrashAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException("The selected item no longer exists.", path);
        }

        var result = await Task
            .Run(() => _mover.Move(path), cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(result.FailureReason)
                    ? "macOS could not move the selected item to Trash."
                    : result.FailureReason);
        }
    }
}

internal readonly record struct TrashItemMoveResult(bool Succeeded, string? FailureReason)
{
    public static TrashItemMoveResult Success { get; } = new(true, null);

    public static TrashItemMoveResult Failure(string? failureReason) =>
        new(false, failureReason);
}

internal interface ITrashItemMover
{
    TrashItemMoveResult Move(string path);
}

internal sealed class NativeTrashItemMover : ITrashItemMover
{
    public TrashItemMoveResult Move(string path) => TrashFileManager.MoveToTrash(path);
}

internal static class TrashFileManager
{
    private const string Libobjc = "/usr/lib/libobjc.dylib";
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";
    private const string FoundationFramework =
        "/System/Library/Frameworks/Foundation.framework/Foundation";
    private const string UnavailableMessage =
        "macOS could not move the selected item to Trash.";
    private const int RtldNow = 2;
    private static bool _isLoaded;

    public static TrashItemMoveResult MoveToTrash(string path)
    {
        if (!OperatingSystem.IsMacOS() || !EnsureLoaded())
        {
            return TrashItemMoveResult.Failure(UnavailableMessage);
        }

        var pool = CreateAutoreleasePool();

        try
        {
            var fileManagerClass = GetClass("NSFileManager");
            var stringClass = GetClass("NSString");
            var urlClass = GetClass("NSURL");
            if (fileManagerClass == IntPtr.Zero
                || stringClass == IntPtr.Zero
                || urlClass == IntPtr.Zero)
            {
                return TrashItemMoveResult.Failure(UnavailableMessage);
            }

            var nativePath = SendMessageStr(
                stringClass,
                GetSelector("stringWithUTF8String:"),
                path);
            if (nativePath == IntPtr.Zero)
            {
                return TrashItemMoveResult.Failure(UnavailableMessage);
            }

            var url = SendMessage(urlClass, GetSelector("fileURLWithPath:"), nativePath);
            var fileManager = SendMessage(fileManagerClass, GetSelector("defaultManager"));
            if (url == IntPtr.Zero || fileManager == IntPtr.Zero)
            {
                return TrashItemMoveResult.Failure(UnavailableMessage);
            }

            var error = IntPtr.Zero;
            var moved = SendTrashItemMessage(
                fileManager,
                GetSelector("trashItemAtURL:resultingItemURL:error:"),
                url,
                IntPtr.Zero,
                ref error);

            return moved != 0
                ? TrashItemMoveResult.Success
                : TrashItemMoveResult.Failure(DescribeError(error) ?? UnavailableMessage);
        }
        finally
        {
            DrainAutoreleasePool(pool);
        }
    }

    private static bool EnsureLoaded()
    {
        if (_isLoaded)
        {
            return true;
        }

        _isLoaded = Dlopen(FoundationFramework, RtldNow) != IntPtr.Zero;
        return _isLoaded;
    }

    private static IntPtr CreateAutoreleasePool()
    {
        var poolClass = GetClass("NSAutoreleasePool");
        if (poolClass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var allocated = SendMessage(poolClass, GetSelector("alloc"));
        return allocated == IntPtr.Zero
            ? IntPtr.Zero
            : SendMessage(allocated, GetSelector("init"));
    }

    private static void DrainAutoreleasePool(IntPtr pool)
    {
        if (pool != IntPtr.Zero)
        {
            SendVoid(pool, GetSelector("drain"));
        }
    }

    private static string? DescribeError(IntPtr error)
    {
        if (error == IntPtr.Zero)
        {
            return null;
        }

        var description = SendMessage(error, GetSelector("localizedDescription"));
        if (description == IntPtr.Zero)
        {
            return null;
        }

        var utf8 = SendMessage(description, GetSelector("UTF8String"));
        return utf8 == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(utf8);
    }

    [DllImport(LibSystem, EntryPoint = "dlopen")]
    private static extern IntPtr Dlopen(string path, int mode);

    [DllImport(Libobjc, EntryPoint = "objc_getClass")]
    private static extern IntPtr GetClass(string name);

    [DllImport(Libobjc, EntryPoint = "sel_registerName")]
    private static extern IntPtr GetSelector(string name);

    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendMessage(IntPtr receiver, IntPtr selector);

    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendMessage(
        IntPtr receiver,
        IntPtr selector,
        IntPtr arg);

    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(IntPtr receiver, IntPtr selector);

    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendMessageStr(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string arg);

    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    private static extern byte SendTrashItemMessage(
        IntPtr receiver,
        IntPtr selector,
        IntPtr url,
        IntPtr resultingUrl,
        ref IntPtr error);
}
