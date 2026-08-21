using System.Runtime.InteropServices;
using MacStorageAtlas.Core.Platform;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacFileRevealService : IFileRevealService
{
    private readonly IFileRevealPresenter _presenter;

    public MacFileRevealService()
        : this(new NativeFileRevealPresenter())
    {
    }

    internal MacFileRevealService(IFileRevealPresenter presenter)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
    }

    public bool Reveal(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            (!File.Exists(path) && !Directory.Exists(path)))
        {
            return false;
        }

        try
        {
            return _presenter.Reveal(path);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

internal interface IFileRevealPresenter
{
    bool Reveal(string path);
}

internal sealed class NativeFileRevealPresenter : IFileRevealPresenter
{
    public bool Reveal(string path) => FinderWorkspace.Reveal(path);
}

internal static class FinderWorkspace
{
    private const string Libobjc = "/usr/lib/libobjc.dylib";
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";
    private const string AppKitFramework =
        "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const int RtldNow = 2;
    private const string RevealSelectorName = "activateFileViewerSelectingURLs:";
    private static bool _isLoaded;

    public static bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsMacOS() || !EnsureLoaded())
            {
                return false;
            }

            var workspaceClass = GetClass("NSWorkspace");
            return workspaceClass != IntPtr.Zero
                && GetInstanceMethod(workspaceClass, GetSelector(RevealSelectorName))
                    != IntPtr.Zero;
        }
    }

    public static bool Reveal(string path)
    {
        if (!OperatingSystem.IsMacOS() || !EnsureLoaded())
        {
            return false;
        }

        var pool = CreateAutoreleasePool();

        try
        {
            var workspaceClass = GetClass("NSWorkspace");
            var stringClass = GetClass("NSString");
            var urlClass = GetClass("NSURL");
            var arrayClass = GetClass("NSArray");
            if (workspaceClass == IntPtr.Zero
                || stringClass == IntPtr.Zero
                || urlClass == IntPtr.Zero
                || arrayClass == IntPtr.Zero)
            {
                return false;
            }

            var nativePath = SendMessageStr(
                stringClass,
                GetSelector("stringWithUTF8String:"),
                path);
            if (nativePath == IntPtr.Zero)
            {
                return false;
            }

            var url = SendMessage(urlClass, GetSelector("fileURLWithPath:"), nativePath);
            if (url == IntPtr.Zero)
            {
                return false;
            }

            var urls = SendMessage(arrayClass, GetSelector("arrayWithObject:"), url);
            var workspace = SendMessage(workspaceClass, GetSelector("sharedWorkspace"));
            if (urls == IntPtr.Zero || workspace == IntPtr.Zero)
            {
                return false;
            }

            SendVoid(workspace, GetSelector(RevealSelectorName), urls);
            return true;
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

        _isLoaded = Dlopen(AppKitFramework, RtldNow) != IntPtr.Zero;
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

    [DllImport(LibSystem, EntryPoint = "dlopen")]
    private static extern IntPtr Dlopen(string path, int mode);

    [DllImport(Libobjc, EntryPoint = "objc_getClass")]
    private static extern IntPtr GetClass(string name);

    [DllImport(Libobjc, EntryPoint = "sel_registerName")]
    private static extern IntPtr GetSelector(string name);

    [DllImport(Libobjc, EntryPoint = "class_getInstanceMethod")]
    private static extern IntPtr GetInstanceMethod(IntPtr cls, IntPtr selector);

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
    private static extern void SendVoid(
        IntPtr receiver,
        IntPtr selector,
        IntPtr arg);

    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendMessageStr(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string arg);
}
