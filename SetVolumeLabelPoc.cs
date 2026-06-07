#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0-windows
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property AllowUnsafeBlocks=true
#:property DisableRuntimeMarshalling=true
#:property PublishAot=true
#:property InvariantGlobalization=true

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

return SetVolumeLabelPoc.Run(args);

internal static class SetVolumeLabelPoc
{
	public static int Run(string[] args)
	{
		if (args is ["--help"] or ["-h"] or ["/?"])
		{
			Console.WriteLine("Usage: dotnet run --file SetVolumeLabelPoc.cs -- [label]");
			return 0;
		}

		string volumeName = args.Length > 0 ? args[0] : "Local Disk x2";

		ComRuntime.Initialize();

		try
		{
			IMountPointRename mountPointRename = ShellInterop.GetElevatedMountPointRename();
			HResult.ThrowIfFailed(mountPointRename.Rename("C:\\", volumeName));
			return 0;
		}
		finally
		{
			ComRuntime.Uninitialize();
		}
	}
}

internal static class ShellInterop
{
	public static IMountPointRename GetElevatedMountPointRename()
	{
		Guid iid = typeof(IMountPointRename).GUID;
		HResult.ThrowIfFailed(NativeMethods.CoGetMountPointRenameObject(
			"Elevation:Administrator!new:{60173D16-A550-47f0-A14B-C6F9E4DA0831}",
			0,
			ref iid,
			out IMountPointRename mountPointRename));

		return mountPointRename;
	}
}

internal static partial class NativeMethods
{
	[LibraryImport("ole32.dll")]
	internal static partial int CoInitializeEx(nint pvReserved, COINIT coInit);

	[LibraryImport("ole32.dll")]
	internal static partial void CoUninitialize();

	[LibraryImport("ole32.dll", EntryPoint = "CoGetObject", StringMarshalling = StringMarshalling.Utf16)]
	internal static partial int CoGetMountPointRenameObject(
		string displayName,
		nint bindOptions,
		ref Guid riid,
		out IMountPointRename obj);
}

internal static class ComRuntime
{
	private static bool shouldUninitialize;

	public static void Initialize()
	{
		int hr = NativeMethods.CoInitializeEx(0, COINIT.ApartmentThreaded);
		shouldUninitialize = hr is HResult.S_OK or HResult.S_FALSE;
		if (hr != HResult.RPC_E_CHANGED_MODE)
		{
			HResult.ThrowIfFailed(hr);
		}
	}

	public static void Uninitialize()
	{
		if (shouldUninitialize)
		{
			NativeMethods.CoUninitialize();
		}
	}
}

internal static class HResult
{
	public const int S_OK = 0;
	public const int S_FALSE = 1;
	public const int RPC_E_CHANGED_MODE = unchecked((int)0x80010106);

	public static void ThrowIfFailed(int hr)
	{
		if (hr < 0)
		{
			Marshal.ThrowExceptionForHR(hr);
		}
	}
}

[GeneratedComInterface]
[Guid("00000000-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IUnknownObject
{
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("92F8D886-AB61-4113-BD4F-2E894397386F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IMountPointRename : IUnknownObject
{
	[PreserveSig]
	int Rename(string rootPathName, string volumeName);
}

[Flags]
internal enum COINIT : uint
{
	Multithreaded = 0x0,
	ApartmentThreaded = 0x2,
	DisableOle1Dde = 0x4,
	SpeedOverMemory = 0x8,
}
