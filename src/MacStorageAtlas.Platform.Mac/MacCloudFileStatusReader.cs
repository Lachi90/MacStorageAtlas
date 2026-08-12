using System.Runtime.InteropServices;
using MacStorageAtlas.Core.Insights;

namespace MacStorageAtlas.Platform.Mac;

internal interface IMacCloudFileStatusReader
{
    DuplicateContentAvailability? GetContentAvailability(string path);
}

internal sealed class MacCloudFileStatusReader : IMacCloudFileStatusReader
{
    private static readonly Lazy<IntPtr> FoundationHandle = new(() => NativeLibrary.Load(
        "/System/Library/Frameworks/Foundation.framework/Foundation"));
    private static readonly Lazy<IntPtr> NsAutoreleasePoolClass = new(
        () => objc_getClass("NSAutoreleasePool"));
    private static readonly Lazy<IntPtr> NsStringClass = new(() => objc_getClass("NSString"));
    private static readonly Lazy<IntPtr> NsUrlClass = new(() => objc_getClass("NSURL"));
    private static readonly Lazy<IntPtr> AllocSelector = new(
        () => sel_registerName("alloc"));
    private static readonly Lazy<IntPtr> InitSelector = new(
        () => sel_registerName("init"));
    private static readonly Lazy<IntPtr> DrainSelector = new(
        () => sel_registerName("drain"));
    private static readonly Lazy<IntPtr> StringWithUtf8StringSelector = new(
        () => sel_registerName("stringWithUTF8String:"));
    private static readonly Lazy<IntPtr> FileUrlWithPathSelector = new(
        () => sel_registerName("fileURLWithPath:"));
    private static readonly Lazy<IntPtr> GetResourceValueSelector = new(
        () => sel_registerName("getResourceValue:forKey:error:"));
    private static readonly Lazy<IntPtr> BoolValueSelector = new(
        () => sel_registerName("boolValue"));
    private static readonly Lazy<IntPtr> IsEqualSelector = new(
        () => sel_registerName("isEqual:"));

    public DuplicateContentAvailability? GetContentAvailability(string path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        var pool = CreateAutoreleasePool();
        try
        {
            if (!TryGetNSStringConstant("NSURLIsUbiquitousItemKey", out var isUbiquitousKey)
                || !TryGetNSStringConstant(
                    "NSURLUbiquitousItemDownloadingStatusKey",
                    out var downloadingStatusKey)
                || !TryGetNSStringConstant(
                    "NSURLUbiquitousItemDownloadingStatusNotDownloaded",
                    out var notDownloadedStatus))
            {
                return null;
            }

            var url = FileUrl(path);
            if (url == IntPtr.Zero)
            {
                return null;
            }

            if (!TryGetResourceValue(url, isUbiquitousKey, out var isUbiquitousValue)
                || isUbiquitousValue == IntPtr.Zero
                || !objc_msgSend_bool(isUbiquitousValue, BoolValueSelector.Value))
            {
                return DuplicateContentAvailability.Local;
            }

            if (!TryGetResourceValue(url, downloadingStatusKey, out var status)
                || status == IntPtr.Zero)
            {
                return null;
            }

            return objc_msgSend_bool_arg(status, IsEqualSelector.Value, notDownloadedStatus)
                ? DuplicateContentAvailability.NotLocal
                : DuplicateContentAvailability.Local;
        }
        finally
        {
            DrainAutoreleasePool(pool);
        }
    }

    private static IntPtr CreateAutoreleasePool()
    {
        var allocated = objc_msgSend_intptr(NsAutoreleasePoolClass.Value, AllocSelector.Value);
        return allocated == IntPtr.Zero
            ? IntPtr.Zero
            : objc_msgSend_intptr(allocated, InitSelector.Value);
    }

    private static void DrainAutoreleasePool(IntPtr pool)
    {
        if (pool != IntPtr.Zero)
        {
            objc_msgSend_void(pool, DrainSelector.Value);
        }
    }

    private static IntPtr FileUrl(string path)
    {
        var pathString = objc_msgSend_string(
            NsStringClass.Value,
            StringWithUtf8StringSelector.Value,
            path);
        return pathString == IntPtr.Zero
            ? IntPtr.Zero
            : objc_msgSend_intptr(NsUrlClass.Value, FileUrlWithPathSelector.Value, pathString);
    }

    private static bool TryGetResourceValue(IntPtr url, IntPtr key, out IntPtr value)
    {
        var error = IntPtr.Zero;
        return objc_msgSend_getResourceValue(
            url,
            GetResourceValueSelector.Value,
            out value,
            key,
            ref error);
    }

    private static bool TryGetNSStringConstant(string name, out IntPtr value)
    {
        value = IntPtr.Zero;
        if (!NativeLibrary.TryGetExport(FoundationHandle.Value, name, out var symbol)
            || symbol == IntPtr.Zero)
        {
            return false;
        }

        value = Marshal.ReadIntPtr(symbol);
        return value != IntPtr.Zero;
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_intptr(
        IntPtr receiver,
        IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_string(
        IntPtr receiver,
        IntPtr selector,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_intptr(
        IntPtr receiver,
        IntPtr selector,
        IntPtr value);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_getResourceValue(
        IntPtr receiver,
        IntPtr selector,
        out IntPtr value,
        IntPtr key,
        ref IntPtr error);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool(
        IntPtr receiver,
        IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_bool_arg(
        IntPtr receiver,
        IntPtr selector,
        IntPtr value);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void(
        IntPtr receiver,
        IntPtr selector);
}
