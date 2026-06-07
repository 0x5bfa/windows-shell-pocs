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

return ParseShellUrlPoc.Run();

internal static class ParseShellUrlPoc
{
	public static int Run()
	{
		ComRuntime.Initialize();

		try
		{
			IUnknownObject shellUrl = ShellInterop.CreateShellUrl();

			Console.WriteLine($"ShellUrl object: {shellUrl.GetType().Name}");
			PrintOptionalInterface<IShellUrl>(shellUrl, nameof(IShellUrl));
			PrintOptionalInterface<IShellUrl2>(shellUrl, nameof(IShellUrl2));
			return 0;
		}
		finally
		{
			ComRuntime.Uninitialize();
		}
	}

	private static void PrintOptionalInterface<T>(IUnknownObject obj, string interfaceName)
		where T : IUnknownObject
	{
		try
		{
			var typedObject = (T)obj;
			Console.WriteLine($"{interfaceName}: {typedObject.GetType().Name}");
		}
		catch (InvalidCastException)
		{
			Console.WriteLine($"{interfaceName}: not supported");
		}
	}
}

internal static class ShellInterop
{
	public static IUnknownObject CreateShellUrl()
	{
		Guid clsid = ShellGuids.CLSID_ShellUrl;
		Guid iid = typeof(IUnknownObject).GUID;
		HResult.ThrowIfFailed(NativeMethods.CoCreateShellUrl(
			ref clsid,
			null,
			CLSCTX.InprocServer,
			ref iid,
			out IUnknownObject shellUrl));

		return shellUrl;
	}
}

internal static class ShellGuids
{
	public static readonly Guid CLSID_ShellUrl = new("4BEC2015-BFA1-42FA-9C0C-59431BBE880E");
}

internal static partial class NativeMethods
{
	[LibraryImport("ole32.dll")]
	internal static partial int CoInitializeEx(nint pvReserved, COINIT coInit);

	[LibraryImport("ole32.dll")]
	internal static partial void CoUninitialize();

	[LibraryImport("ole32.dll", EntryPoint = "CoCreateInstance")]
	internal static partial int CoCreateShellUrl(
		ref Guid rclsid,
		IUnknownObject? outer,
		CLSCTX clsContext,
		ref Guid riid,
		out IUnknownObject obj);
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

[GeneratedComInterface]
[Guid("88DF9332-6ADB-4604-8218-508673EF7F8A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IShellUrl : IUnknownObject
{
}

[GeneratedComInterface]
[Guid("4F33718D-BAE1-4F9B-96F2-D2A16E683346")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IShellUrl2 : IUnknownObject
{
}

[Flags]
internal enum CLSCTX : uint
{
	InprocServer = 0x1,
	InprocHandler = 0x2,
	LocalServer = 0x4,
	RemoteServer = 0x10,
	All = InprocServer | InprocHandler | LocalServer | RemoteServer,
}

[Flags]
internal enum COINIT : uint
{
	Multithreaded = 0x0,
	ApartmentThreaded = 0x2,
	DisableOle1Dde = 0x4,
	SpeedOverMemory = 0x8,
}
