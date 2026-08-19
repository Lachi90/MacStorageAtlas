using System.Runtime.InteropServices;
using MacStorageAtlas.Core.Platform;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacQuickLookService : IQuickLookService
{
    private readonly IQuickLookPresenter _presenter;

    public MacQuickLookService()
        : this(new QuickLookPresenter())
    {
    }

    internal MacQuickLookService(IQuickLookPresenter presenter)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
    }

    public bool Preview(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            (!File.Exists(path) && !Directory.Exists(path)))
        {
            return false;
        }

        try
        {
            return _presenter.Preview(path);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

internal interface IQuickLookPresenter
{
    bool Preview(string path);
}

internal sealed class QuickLookPresenter : IQuickLookPresenter
{
    public bool Preview(string path) => QuickLookUi.Preview(path);
}

internal static class QuickLookUi
{
    private const string Libobjc = "/usr/lib/libobjc.dylib";
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";
    private const string QuickLookUiFramework =
        "/System/Library/Frameworks/QuickLookUI.framework/QuickLookUI";
    private const int RtldNow = 2;
    private static readonly object SyncRoot = new();
    private static readonly NumberOfItemsDelegate NumberOfItemsCallback =
        NumberOfPreviewItems;
    private static readonly PreviewItemDelegate PreviewItemCallback =
        PreviewItemAtIndex;
    private static readonly IntPtr NumberOfItemsPointer =
        Marshal.GetFunctionPointerForDelegate(NumberOfItemsCallback);
    private static readonly IntPtr PreviewItemPointer =
        Marshal.GetFunctionPointerForDelegate(PreviewItemCallback);
    private static IntPtr _dataSource;
    private static IntPtr _previewItem;
    private static bool _isLoaded;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint NumberOfItemsDelegate(
        IntPtr self,
        IntPtr selector,
        IntPtr panel);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr PreviewItemDelegate(
        IntPtr self,
        IntPtr selector,
        IntPtr panel,
        nint index);

    public static bool Preview(string path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        lock (SyncRoot)
        {
            if (!EnsureLoaded())
            {
                return false;
            }

            var panelClass = GetClass("QLPreviewPanel");
            var nsUrlClass = GetClass("NSURL");
            var nsStringClass = GetClass("NSString");
            if (panelClass == IntPtr.Zero ||
                nsUrlClass == IntPtr.Zero ||
                nsStringClass == IntPtr.Zero)
            {
                return false;
            }

            var nsPath = SendMessageStr(
                nsStringClass,
                GetSelector("stringWithUTF8String:"),
                path);
            var url = SendMessage(
                nsUrlClass,
                GetSelector("fileURLWithPath:"),
                nsPath);
            if (url == IntPtr.Zero)
            {
                return false;
            }

            SetPreviewItem(Retain(url));

            var panel = SendMessage(panelClass, GetSelector("sharedPreviewPanel"));
            var dataSource = GetDataSource();
            if (panel == IntPtr.Zero || dataSource == IntPtr.Zero)
            {
                return false;
            }

            SendMessage(panel, GetSelector("setDataSource:"), dataSource);
            SendVoid(panel, GetSelector("reloadData"));
            SendVoid(panel, GetSelector("refreshCurrentPreviewItem"));
            SendVoid(panel, GetSelector("makeKeyAndOrderFront:"), IntPtr.Zero);
            return true;
        }
    }

    private static bool EnsureLoaded()
    {
        if (_isLoaded)
        {
            return true;
        }

        _isLoaded = Dlopen(QuickLookUiFramework, RtldNow) != IntPtr.Zero;
        return _isLoaded;
    }

    private static IntPtr GetDataSource()
    {
        if (_dataSource != IntPtr.Zero)
        {
            return _dataSource;
        }

        var nsObjectClass = GetClass("NSObject");
        if (nsObjectClass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var dataSourceClass = AllocateClassPair(
            nsObjectClass,
            "MacStorageAtlasQuickLookDataSource",
            0);
        if (dataSourceClass != IntPtr.Zero)
        {
            AddMethod(
                dataSourceClass,
                GetSelector("numberOfPreviewItemsInPreviewPanel:"),
                NumberOfItemsPointer,
                "q@:@");
            AddMethod(
                dataSourceClass,
                GetSelector("previewPanel:previewItemAtIndex:"),
                PreviewItemPointer,
                "@@:@q");
            RegisterClassPair(dataSourceClass);
        }

        var registeredClass = GetClass("MacStorageAtlasQuickLookDataSource");
        if (registeredClass == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var allocated = SendMessage(registeredClass, GetSelector("alloc"));
        _dataSource = SendMessage(allocated, GetSelector("init"));
        return _dataSource;
    }

    private static void SetPreviewItem(IntPtr value)
    {
        if (_previewItem != IntPtr.Zero)
        {
            Release(_previewItem);
        }

        _previewItem = value;
    }

    private static nint NumberOfPreviewItems(
        IntPtr self,
        IntPtr selector,
        IntPtr panel) =>
        _previewItem == IntPtr.Zero ? 0 : 1;

    private static IntPtr PreviewItemAtIndex(
        IntPtr self,
        IntPtr selector,
        IntPtr panel,
        nint index) =>
        index == 0 ? _previewItem : IntPtr.Zero;

    private static IntPtr Retain(IntPtr value) =>
        SendMessage(value, GetSelector("retain"));

    private static void Release(IntPtr value) =>
        SendVoid(value, GetSelector("release"));

    [DllImport(LibSystem, EntryPoint = "dlopen")]
    private static extern IntPtr Dlopen(string path, int mode);

    [DllImport(Libobjc, EntryPoint = "objc_getClass")]
    private static extern IntPtr GetClass(string name);

    [DllImport(Libobjc, EntryPoint = "sel_registerName")]
    private static extern IntPtr GetSelector(string name);

    [DllImport(Libobjc, EntryPoint = "objc_allocateClassPair")]
    private static extern IntPtr AllocateClassPair(
        IntPtr superclass,
        string name,
        nuint extraBytes);

    [DllImport(Libobjc, EntryPoint = "objc_registerClassPair")]
    private static extern void RegisterClassPair(IntPtr value);

    [DllImport(Libobjc, EntryPoint = "class_addMethod")]
    private static extern bool AddMethod(
        IntPtr cls,
        IntPtr name,
        IntPtr implementation,
        string types);

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
