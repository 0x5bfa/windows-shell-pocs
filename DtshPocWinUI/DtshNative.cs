using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace DtshPocWinUI;

internal static class DtshNative
{
    private static readonly Guid ClsidDetectionAndSharing = new("1FDA955B-61FF-11DA-978C-0008744FAAB7");
    private static readonly Guid ClsidMultiObjectElevationFactory = new("36F0BD14-D84D-468C-B79C-9990F3FA897F");
    private static readonly Guid ClsidOpenControlPanel = new("06622D85-6856-4460-8DE1-A81921B41C4B");
    private static readonly Guid NetworkExplorerElevationGuid = new("7A076CE1-4B31-452A-A4F1-0304C8738100");

    public static IDetectionAndSharing CreateDetectionAndSharing()
    {
        Guid clsid = ClsidDetectionAndSharing;
        Guid iid = typeof(IDetectionAndSharing).GUID;
        int hr = NativeMethods.CoCreateDetectionAndSharing(
            ref clsid,
            null,
            CLSCTX.InProcServer,
            ref iid,
            out IDetectionAndSharing dtsh);

        if (hr < 0)
        {
            throw new COMException("CoCreateInstance(CLSID_DetectionAndSharing) failed.", hr);
        }

        return dtsh;
    }

    public static int InitializeFactory(nint hwnd)
    {
        int hr = CreateElevationFactory(out IMultiObjectElevationFactory factory);
        if (hr < 0)
        {
            return hr;
        }

        Guid elevationGuid = NetworkExplorerElevationGuid;
        return factory.Initialize(hwnd, in elevationGuid);
    }

    public static int CreateElevatedDetectionAndSharing(nint initializeHwnd)
    {
        int hr = InitializeFactory(initializeHwnd, out IMultiObjectElevationFactory? factory);
        if (hr < 0 || factory is null)
        {
            return hr;
        }

        Guid objectClsid = ClsidDetectionAndSharing;
        Guid objectIid = typeof(IDetectionAndSharing).GUID;
        return factory.CreateElevatedObject(in objectClsid, in objectIid, out _);
    }

    public static int TurnOnDtSharing(nint initializeHwnd, nint turnOnHwnd)
    {
        int hr = InitializeFactory(initializeHwnd, out IMultiObjectElevationFactory? factory);
        if (hr < 0 || factory is null)
        {
            return hr;
        }

        Guid objectClsid = ClsidDetectionAndSharing;
        Guid objectIid = typeof(IDetectionAndSharing).GUID;
        hr = factory.CreateElevatedObject(in objectClsid, in objectIid, out IDetectionAndSharing dtsh);
        if (hr < 0)
        {
            return hr;
        }

        hr = NativeMethods.CoAllowSetForegroundWindow(dtsh, 0);
        if (hr < 0)
        {
            return hr;
        }

        return dtsh.TurnOn(turnOnHwnd, DtshType.All, 1);
    }

    public static int OpenNetCenter()
    {
        Guid clsid = ClsidOpenControlPanel;
        Guid iid = typeof(IOpenControlPanel).GUID;
        int hr = NativeMethods.CoCreateOpenControlPanel(
            ref clsid,
            null,
            CLSCTX.InProcServer,
            ref iid,
            out IOpenControlPanel controlPanel);

        if (hr < 0)
        {
            return hr;
        }

        hr = controlPanel.Open("Microsoft.NetworkAndSharingCenter", "Advanced", null);
        return hr < 0 ? hr : 0;
    }

    public static string FormatHr(int hr)
    {
        if (hr >= 0)
        {
            return $"0x{hr:X8}";
        }

        string message = Marshal.GetExceptionForHR(hr)?.Message ?? new Win32Exception(hr).Message;
        return $"0x{hr:X8} ({message})";
    }

    public static string FormatHwnd(nint hwnd)
    {
        return $"0x{hwnd.ToInt64():X}";
    }

    public static string FormatState(DtshState state)
    {
        return state switch
        {
            DtshState.Off => "Off",
            DtshState.On => "On",
            _ => $"Unknown ({(int)state})",
        };
    }

    public static string FormatAction(DtshAction action)
    {
        return action switch
        {
            DtshAction.None => "None",
            _ => $"Unknown ({(int)action})",
        };
    }

    public static string FormatProfile(NetFwProfileType2 profile)
    {
        int rawProfile = (int)profile;
        int knownProfiles = (int)(NetFwProfileType2.Domain | NetFwProfileType2.Private | NetFwProfileType2.Public);

        if (profile is not NetFwProfileType2.All
            && (rawProfile & knownProfiles) == rawProfile
            && (rawProfile & (rawProfile - 1)) != 0)
        {
            List<string> names = [];
            if ((profile & NetFwProfileType2.Domain) != 0)
            {
                names.Add("Domain");
            }

            if ((profile & NetFwProfileType2.Private) != 0)
            {
                names.Add("Private");
            }

            if ((profile & NetFwProfileType2.Public) != 0)
            {
                names.Add("Public");
            }

            return string.Join(" | ", names);
        }

        return profile switch
        {
            NetFwProfileType2.Domain => "Domain",
            NetFwProfileType2.Private => "Private",
            NetFwProfileType2.Public => "Public",
            NetFwProfileType2.All => "All",
            _ => $"0x{rawProfile:X}",
        };
    }

    private static int CreateElevationFactory(out IMultiObjectElevationFactory factory)
    {
        Guid clsid = ClsidMultiObjectElevationFactory;
        Guid iid = typeof(IMultiObjectElevationFactory).GUID;
        return NativeMethods.CoCreateMultiObjectElevationFactory(
            ref clsid,
            null,
            CLSCTX.InProcServer,
            ref iid,
            out factory);
    }

    private static int InitializeFactory(nint hwnd, out IMultiObjectElevationFactory? factory)
    {
        int hr = CreateElevationFactory(out IMultiObjectElevationFactory createdFactory);
        if (hr < 0)
        {
            factory = null;
            return hr;
        }

        Guid elevationGuid = NetworkExplorerElevationGuid;
        hr = createdFactory.Initialize(hwnd, in elevationGuid);
        factory = hr >= 0 ? createdFactory : null;
        return hr;
    }
}

internal static partial class NativeMethods
{
    [LibraryImport("ole32.dll", EntryPoint = "CoCreateInstance")]
    internal static partial int CoCreateDetectionAndSharing(
        ref Guid rclsid,
        IUnknownObject? outer,
        CLSCTX clsContext,
        ref Guid riid,
        out IDetectionAndSharing obj);

    [LibraryImport("ole32.dll", EntryPoint = "CoCreateInstance")]
    internal static partial int CoCreateMultiObjectElevationFactory(
        ref Guid rclsid,
        IUnknownObject? outer,
        CLSCTX clsContext,
        ref Guid riid,
        out IMultiObjectElevationFactory obj);

    [LibraryImport("ole32.dll", EntryPoint = "CoCreateInstance")]
    internal static partial int CoCreateOpenControlPanel(
        ref Guid rclsid,
        IUnknownObject? outer,
        CLSCTX clsContext,
        ref Guid riid,
        out IOpenControlPanel obj);

    [LibraryImport("ole32.dll")]
    internal static partial int CoAllowSetForegroundWindow(
        IUnknownObject server,
        nint reserved);
}

[GeneratedComInterface]
[Guid("00000000-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IUnknownObject
{
}

[GeneratedComInterface]
[Guid("1FDA955C-61FF-11DA-978C-0008744FAAB7")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IDetectionAndSharing : IUnknownObject
{
    [PreserveSig]
    int GetStatus(
        DtshType type,
        out DtshState state,
        out DtshAction action);

    [PreserveSig]
    int TurnOn(
        nint hwnd,
        DtshType type,
        int value);

    [PreserveSig]
    int GetCurrentFwProfile(
        out NetFwProfileType2 profile);

    [PreserveSig]
    int GetStatusForProfile(
        NetFwProfileType2 profile,
        DtshType type,
        out DtshState state,
        out DtshAction action);

    [PreserveSig]
    int TurnOnForProfile(
        nint hwnd,
        NetFwProfileType2 profile,
        DtshType type,
        int value);
}

[GeneratedComInterface]
[Guid("6FABDA16-031E-47E3-B2A2-2339C05CCB9E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IMultiObjectElevationFactory : IUnknownObject
{
    [PreserveSig]
    int Initialize(
        nint hwnd,
        in Guid context);

    [PreserveSig]
    int Unknown_20();

    [PreserveSig]
    int CreateElevatedObject(
        in Guid clsid,
        in Guid iid,
        out IDetectionAndSharing obj);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("D11AD862-66DE-4DF4-BF6C-1F5621996AF1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IOpenControlPanel : IUnknownObject
{
    [PreserveSig]
    int Open(
        string? name,
        string? page,
        IUnknownObject? site);

    [PreserveSig]
    int GetPath(
        string name,
        nint path,
        uint pathLength);
}

internal enum DtshType
{
    NetworkDiscovery = 0,
    FileSharing = 1,
    All = 3,
}

internal enum DtshState
{
    Off = 0,
    On = 1,
}

internal enum DtshAction
{
    None = 0,
}

[Flags]
internal enum NetFwProfileType2
{
    Domain = 0x1,
    Private = 0x2,
    Public = 0x4,
    All = 0x7fffffff,
}

[Flags]
internal enum CLSCTX : uint
{
    InProcServer = 0x1,
}
