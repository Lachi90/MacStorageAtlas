using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MacStorageAtlas.Platform.Mac;

public static class MacDockIcon
{
    private const string Libobjc = "/usr/lib/libobjc.dylib";

    [DllImport(Libobjc, EntryPoint = "objc_getClass")]
    private static extern IntPtr GetClass(string name);

    [DllImport(Libobjc, EntryPoint = "sel_registerName")]
    private static extern IntPtr GetSelector(string name);

    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendMessage(IntPtr receiver, IntPtr selector);

    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendMessage(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport(Libobjc, EntryPoint = "objc_msgSend")]
    private static extern IntPtr SendMessageStr(
        IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string arg);

    public static void TrySet(byte[] imageBytes)
    {
        if (!OperatingSystem.IsMacOS() || imageBytes is null || imageBytes.Length == 0)
        {
            return;
        }

        try
        {
            var path = Path.Combine(Path.GetTempPath(), "MacStorageAtlas.dockicon.png");
            File.WriteAllBytes(path, imageBytes);
            Apply(path);
        }
        catch
        {
        }
    }

    [SupportedOSPlatform("macos")]
    private static void Apply(string filePath)
    {
        var nsStringClass = GetClass("NSString");
        var nsImageClass = GetClass("NSImage");
        var nsApplicationClass = GetClass("NSApplication");
        if (nsStringClass == IntPtr.Zero || nsImageClass == IntPtr.Zero
            || nsApplicationClass == IntPtr.Zero)
        {
            return;
        }

        var nsPath = SendMessageStr(
            nsStringClass, GetSelector("stringWithUTF8String:"), filePath);
        if (nsPath == IntPtr.Zero)
        {
            return;
        }

        var allocated = SendMessage(nsImageClass, GetSelector("alloc"));
        var image = SendMessage(
            allocated, GetSelector("initWithContentsOfFile:"), nsPath);
        if (image == IntPtr.Zero)
        {
            return;
        }

        var sharedApp = SendMessage(nsApplicationClass, GetSelector("sharedApplication"));
        if (sharedApp == IntPtr.Zero)
        {
            return;
        }

        SendMessage(sharedApp, GetSelector("setApplicationIconImage:"), image);
    }
}
