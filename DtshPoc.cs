#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0-windows
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property AllowUnsafeBlocks=true
#:property DisableRuntimeMarshalling=true
#:property PublishAot=true
#:property InvariantGlobalization=true

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

Console.WriteLine("Dtsh POC");

string command = args.Length > 0 ? args[0].ToLowerInvariant() : "status";

try
{
	return command switch
	{
		"status" => RunStatus(),
		"turn-on" => RunTurnOn(),
		"profile" => RunProfile(args),
		"help" or "-h" or "--help" => RunHelp(),
		_ => RunUnknownCommand(command),
	};
}
catch (Exception ex)
{
	Console.Error.WriteLine(ex);
	return 1;
}

static int RunStatus()
{
	IDetectionAndSharing dtsh = DtshNative.CreateDetectionAndSharing();

	PrintStatus(dtsh, DtshType.NetworkDiscovery);
	PrintStatus(dtsh, DtshType.FileSharing);
	PrintStatus(dtsh, DtshType.All);

	return 0;
}

static int RunTurnOn()
{
	IDetectionAndSharing dtsh = DtshNative.CreateDetectionAndSharing();

	Console.WriteLine("Before:");
	PrintStatus(dtsh, DtshType.NetworkDiscovery);
	PrintStatus(dtsh, DtshType.FileSharing);

	Console.WriteLine();
	Console.WriteLine("Calling CNetworkExplorerFolder::s_TurnOnDTSharing(hwnd: 0)...");
	int hr = DtshNative.TurnOnDtSharing(0);
	PrintHr("s_TurnOnDTSharing", hr);

	Console.WriteLine();
	Console.WriteLine("After:");
	PrintStatus(dtsh, DtshType.NetworkDiscovery);
	PrintStatus(dtsh, DtshType.FileSharing);

	return hr < 0 ? 1 : 0;
}

static int RunProfile(string[] args)
{
	IDetectionAndSharing dtsh = DtshNative.CreateDetectionAndSharing();

	if (args.Length > 1)
	{
		NetFwProfileType2 profile = ParseProfile(args[1]);
		PrintStatusForProfile(dtsh, profile, DtshType.NetworkDiscovery);
		PrintStatusForProfile(dtsh, profile, DtshType.FileSharing);
		PrintStatusForProfile(dtsh, profile, DtshType.All);
		return 0;
	}

	int hr = dtsh.GetCurrentFwProfile(out NetFwProfileType2 currentProfile);
	PrintHr("GetCurrentFwProfile", hr);
	if (hr >= 0)
	{
		Console.WriteLine($"Current profile: {FormatProfile(currentProfile)} ({(int)currentProfile})");
		PrintStatusForProfile(dtsh, currentProfile, DtshType.NetworkDiscovery);
		PrintStatusForProfile(dtsh, currentProfile, DtshType.FileSharing);
		PrintStatusForProfile(dtsh, currentProfile, DtshType.All);
	}

	return hr < 0 ? 1 : 0;
}

static int RunHelp()
{
	Console.WriteLine("""
Usage:
  dotnet run --file DtshPoc.cs -- [status]
  dotnet run --file DtshPoc.cs -- turn-on
  dotnet run --file DtshPoc.cs -- profile [Domain|Private|Public|All]

Commands:
  status   Calls GetStatus(type) for NetworkDiscovery, FileSharing, and All.
  turn-on  Calls the CNetworkExplorerFolder::s_TurnOnDTSharing flow from NetworkExplorer.dll.
  profile  Calls GetCurrentFwProfile and GetStatusForProfile, or queries the specified firewall profile.
""");
	return 0;
}

static int RunUnknownCommand(string command)
{
	Console.Error.WriteLine($"Unknown command: {command}");
	Console.Error.WriteLine("Run `dotnet run --file DtshPoc.cs -- help` for usage.");
	return 2;
}

static void PrintStatus(IDetectionAndSharing dtsh, DtshType type)
{
	int hr = dtsh.GetStatus(type, out DtshState state, out DtshAction action);
	Console.WriteLine($"{type}:");
	PrintHr("  GetStatus", hr);
	if (hr >= 0)
	{
		Console.WriteLine($"  State : {FormatState(state)} ({(int)state})");
		Console.WriteLine($"  Action: {FormatAction(action)} ({(int)action})");
	}
}

static void PrintStatusForProfile(IDetectionAndSharing dtsh, NetFwProfileType2 profile, DtshType type)
{
	int hr = dtsh.GetStatusForProfile(profile, type, out DtshState state, out DtshAction action);
	Console.WriteLine($"{FormatProfile(profile)} / {type}:");
	PrintHr("  GetStatusForProfile", hr);
	if (hr >= 0)
	{
		Console.WriteLine($"  State : {FormatState(state)} ({(int)state})");
		Console.WriteLine($"  Action: {FormatAction(action)} ({(int)action})");
	}
}

static void PrintHr(string operation, int hr)
{
	if (hr >= 0)
	{
		Console.WriteLine($"{operation}: S_OK-ish 0x{hr:X8}");
		return;
	}

	string message = new Win32Exception(hr).Message;
	Console.WriteLine($"{operation}: FAILED 0x{hr:X8} ({message})");
}

static NetFwProfileType2 ParseProfile(string text)
{
	return text.ToLowerInvariant() switch
	{
		"domain" => NetFwProfileType2.Domain,
		"private" => NetFwProfileType2.Private,
		"public" => NetFwProfileType2.Public,
		"all" => NetFwProfileType2.All,
		_ when int.TryParse(text, out int raw) => (NetFwProfileType2)raw,
		_ => throw new ArgumentException($"Unknown firewall profile: {text}", nameof(text)),
	};
}

static string FormatState(DtshState state)
{
	return state switch
	{
		DtshState.Off => "Off",
		DtshState.On => "On",
		_ => "Unknown",
	};
}

static string FormatAction(DtshAction action)
{
	return action switch
	{
		DtshAction.None => "None",
		_ => "Unknown",
	};
}

static string FormatProfile(NetFwProfileType2 profile)
{
	int rawProfile = (int)profile;
	int knownProfiles = (int)(NetFwProfileType2.Domain | NetFwProfileType2.Private | NetFwProfileType2.Public);

	if (profile is not NetFwProfileType2.All
		&& (rawProfile & knownProfiles) == rawProfile
		&& (rawProfile & (rawProfile - 1)) != 0)
	{
		var names = new List<string>();
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

internal static class DtshNative
{
	private static readonly Guid ClsidDetectionAndSharing = new("1FDA955B-61FF-11DA-978C-0008744FAAB7");
	private static readonly Guid ClsidMultiObjectElevationFactory = new("36F0BD14-D84D-468C-B79C-9990F3FA897F");
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
			throw new InvalidOperationException(
				$"CoCreateInstance(CLSID_DetectionAndSharing) failed: 0x{hr:X8}",
				Marshal.GetExceptionForHR(hr));
		}

		return dtsh;
	}

	public static int TurnOnDtSharing(nint hwnd)
	{
		Guid clsid = ClsidMultiObjectElevationFactory;
		Guid iid = typeof(IMultiObjectElevationFactory).GUID;
		int hr = NativeMethods.CoCreateMultiObjectElevationFactory(
			ref clsid,
			null,
			CLSCTX.InProcServer,
			ref iid,
			out IMultiObjectElevationFactory factory);

		if (hr >= 0)
		{
			Guid elevationGuid = NetworkExplorerElevationGuid;
			hr = factory.Initialize(hwnd, in elevationGuid);
			if (hr >= 0)
			{
				Guid objectClsid = ClsidDetectionAndSharing;
				Guid objectIid = typeof(IDetectionAndSharing).GUID;
				hr = factory.CreateElevatedObject(in objectClsid, in objectIid, out IDetectionAndSharing dtsh);
				if (hr >= 0)
				{
					hr = NativeMethods.CoAllowSetForegroundWindow(dtsh, 0);
					if (hr >= 0)
					{
						hr = dtsh.TurnOn(hwnd, DtshType.All, 1);
					}
				}
			}
		}

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

[Flags]
internal enum CLSCTX : uint
{
	InProcServer = 0x1,
}
