#:sdk Microsoft.NET.Sdk
#:package Microsoft.Windows.CsWin32@0.3.298
#:property TargetFramework=net10.0-windows
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property AllowUnsafeBlocks=true
#:property PublishTrimmed=false
#:property NoWarn=IL2050

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Com;

if (args is ["--help"] or ["-h"] or ["/?"])
{
    Console.WriteLine("Usage: dotnet run --file FileExplorerAppExtensionPoc.cs -- <file>");
    return 0;
}

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Specify one existing file.");
    return 2;
}

return FileExplorerAppExtensionPoc.Run(Path.GetFullPath(args[0]));

internal static class FileExplorerAppExtensionPoc
{
    private static readonly Guid StaticsIid = new("104C1AFF-F09F-5AA1-945F-78737EE0FE45");
    private static readonly Guid PropertyValueIid = new("4BD682DD-7554-40E9-9A9B-82654BF08D3C");
    private static readonly Guid MapViewStringObjectIid = new("E480CE40-A338-4ADA-ADCF-272272E48CB9");
    private const uint FileExplorerVerbInfoFlag = 1;

    public static int Run(string filePath)
    {
        unsafe { PInvoke.CoInitializeEx(null, COINIT.COINIT_MULTITHREADED); }
        using HString runtimeClass = HString.Create("Windows.Internal.FileExplorerAppExtension");
        int hr = WinRT.RoGetActivationFactory(runtimeClass.Handle, StaticsIid, out nint statics);
        if (hr < 0)
        {
            Console.Error.WriteLine($"RoGetActivationFactory failed: 0x{hr:X8}");
            return 1;
        }

        try
        {
            using HString type = HString.Create(Path.GetExtension(filePath).ToLowerInvariant());
            nint extensions = 0;
            uint extensionCount = 0;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                hr = CallGetExtensions(statics, type.Handle, FileExplorerVerbInfoFlag, out extensions);
                if (hr < 0)
                {
                    Console.Error.WriteLine($"GetExtensions failed: 0x{hr:X8}");
                    return 1;
                }

                hr = CallSize(extensions, out extensionCount);
                if (hr < 0)
                {
                    Com.Release(extensions);
                    Console.Error.WriteLine($"Extensions.Size failed: 0x{hr:X8}");
                    return 1;
                }

                if (extensionCount != 0 || attempt == 19)
                    break;

                Com.Release(extensions);
                Thread.Sleep(250);
            }

            try
            {
                var commands = new Dictionary<(Guid Clsid, string VerbId), string>();
                foreach (nint extension in VectorItems(extensions))
                {
                    try
                    {
                        if (CallGetVerbs(extension, FileExplorerVerbInfoFlag, out nint verbs) < 0)
                            continue;

                        try
                        {
                            foreach (nint valueSet in VectorItems(verbs))
                            {
                                try
                                {
                                    string id = LookupString(valueSet, "Id");
                                    Guid clsid = LookupGuid(valueSet, "Verb");
                                    if (clsid == Guid.Empty)
                                        continue;
                                    commands.TryAdd((clsid, id), id);
                                }
                                finally { Com.Release(valueSet); }
                            }
                        }
                        finally { Com.Release(verbs); }
                    }
                    finally { Com.Release(extension); }
                }

                int number = 0;
                foreach (var command in commands.OrderBy(x => x.Key.VerbId, StringComparer.OrdinalIgnoreCase)
                                                 .ThenBy(x => x.Key.Clsid))
                    Console.WriteLine($"{++number}. {command.Key.Clsid:D} \"{command.Value}\"");

                if (number == 0)
                    Console.Error.WriteLine($"Extensions: {extensionCount}; verbs: 0");
            }
            finally { Com.Release(extensions); }
        }
        finally { Com.Release(statics); }

        return 0;
    }

    private static IEnumerable<nint> VectorItems(nint vector)
    {
        int hr = CallSize(vector, out uint count);
        Marshal.ThrowExceptionForHR(hr);
        for (uint i = 0; i < count; i++)
        {
            hr = CallGetAt(vector, i, out nint item);
            if (hr >= 0)
                yield return item;
        }
    }

    private static string LookupString(nint valueSet, string key)
    {
        using HString hkey = HString.Create(key);
        if (Com.QueryInterface(valueSet, MapViewStringObjectIid, out nint map) < 0)
            return string.Empty;
        try
        {
            if (CallLookup(map, hkey.Handle, out nint boxed) < 0)
                return string.Empty;
            try
            {
                if (Com.QueryInterface(boxed, PropertyValueIid, out nint value) < 0)
                    return string.Empty;
                try
                {
                    if (CallGetString(value, out nint text) < 0)
                        return string.Empty;
                    try { return HString.Read(text); }
                    finally { WinRT.DeleteString(text); }
                }
                finally { Com.Release(value); }
            }
            finally { Com.Release(boxed); }
        }
        finally { Com.Release(map); }
    }

    private static Guid LookupGuid(nint valueSet, string key)
    {
        using HString hkey = HString.Create(key);
        if (Com.QueryInterface(valueSet, MapViewStringObjectIid, out nint map) < 0)
            return Guid.Empty;
        try
        {
            if (CallLookup(map, hkey.Handle, out nint boxed) < 0)
                return Guid.Empty;
            try
            {
                if (Com.QueryInterface(boxed, PropertyValueIid, out nint value) < 0)
                    return Guid.Empty;
                try
                {
                    return CallGetGuid(value, out Guid result) < 0 ? Guid.Empty : result;
                }
                finally { Com.Release(value); }
            }
            finally { Com.Release(boxed); }
        }
        finally { Com.Release(map); }
    }

    private static T Slot<T>(nint instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * nint.Size));

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetExtensions(nint self, nint type, uint flags, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetVerbs(nint self, uint flags, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Size(nint self, out uint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetAt(nint self, uint index, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Lookup(nint self, nint key, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetString(nint self, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetGuid(nint self, out Guid result);

    private static int CallGetExtensions(nint p, nint type, uint flags, out nint result) => Slot<GetExtensions>(p, 6)(p, type, flags, out result);
    private static int CallGetVerbs(nint p, uint flags, out nint result) => Slot<GetVerbs>(p, 9)(p, flags, out result);
    private static int CallSize(nint p, out uint result) => Slot<Size>(p, 7)(p, out result);
    private static int CallGetAt(nint p, uint i, out nint result) => Slot<GetAt>(p, 6)(p, i, out result);
    private static int CallLookup(nint p, nint key, out nint result) => Slot<Lookup>(p, 6)(p, key, out result);
    private static int CallGetString(nint p, out nint result) => Slot<GetString>(p, 19)(p, out result);
    private static int CallGetGuid(nint p, out Guid result) => Slot<GetGuid>(p, 20)(p, out result);
}

internal sealed class HString : IDisposable
{
    public nint Handle { get; private set; }
    private HString(nint handle) => Handle = handle;
    public static HString Create(string value)
    {
        Marshal.ThrowExceptionForHR(WinRT.CreateString(value, out nint handle));
        return new HString(handle);
    }
    public static string Read(nint handle)
    {
        nint buffer = WinRT.GetRawBuffer(handle, out uint length);
        return Marshal.PtrToStringUni(buffer, checked((int)length)) ?? string.Empty;
    }
    public void Dispose()
    {
        if (Handle != 0)
        {
            WinRT.DeleteString(Handle);
            Handle = 0;
        }
    }
}

internal static class Com
{
    public static int QueryInterface(nint instance, Guid iid, out nint result)
    {
        result = 0;
        return Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance)))(instance, iid, out result);
    }
    public static void Release(nint instance)
    {
        if (instance != 0)
            Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), 2 * nint.Size))(instance);
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int QueryInterfaceDelegate(nint self, Guid iid, out nint result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint ReleaseDelegate(nint self);
}

internal static class WinRT
{
    public static int CreateString(string value, out nint handle) =>
        WindowsCreateString(value, (uint)value.Length, out handle);

    public static int RoGetActivationFactory(nint className, Guid iid, out nint factory)
    {
        factory = 0;
        return WindowsRoGetActivationFactory(className, ref iid, out factory);
    }

    public static void DeleteString(nint handle)
    {
        if (handle != 0)
            WindowsDeleteString(handle);
    }

    public static nint GetRawBuffer(nint handle, out uint length) =>
        WindowsGetStringRawBuffer(handle, out length);

    [DllImport("combase.dll", EntryPoint = "WindowsCreateString", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string value, uint length, out nint handle);

    [DllImport("combase.dll", EntryPoint = "RoGetActivationFactory")]
    private static extern int WindowsRoGetActivationFactory(nint className, ref Guid iid, out nint factory);

    [DllImport("combase.dll", EntryPoint = "WindowsDeleteString")]
    private static extern int WindowsDeleteString(nint handle);

    [DllImport("combase.dll", EntryPoint = "WindowsGetStringRawBuffer")]
    private static extern nint WindowsGetStringRawBuffer(nint handle, out uint length);
}
