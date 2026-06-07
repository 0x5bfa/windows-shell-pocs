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

return PinFolderToQuickAccessPoc.Run(args);

internal static class PinFolderToQuickAccessPoc
{
	public static int Run(string[] args)
	{
		if (args is ["--help"] or ["-h"] or ["/?"])
		{
			Console.WriteLine("Usage: dotnet run --file PinFolderToQuickAccessPoc.cs -- [folder]");
			return 0;
		}

		string folderPath = args.Length > 0 ? args[0] : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

		ComRuntime.Initialize();

		try
		{
			IExecuteCommand executeCommand = ShellInterop.CreatePinToFrequentExecuteCommand();
			var objectWithSelection = (IObjectWithSelection)executeCommand;

			IShellItem shellItem = ShellInterop.CreateShellItem(folderPath);
			IShellItemArray shellItemArray = ShellInterop.CreateShellItemArray(shellItem);

			HResult.ThrowIfFailed(objectWithSelection.SetSelection(shellItemArray));
			HResult.ThrowIfFailed(executeCommand.Execute());
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
	public static IShellItem CreateShellItem(string parsingName)
	{
		Guid iid = typeof(IShellItem).GUID;
		HResult.ThrowIfFailed(NativeMethods.SHCreateItemFromParsingName(
			parsingName,
			null,
			ref iid,
			out IShellItem shellItem));

		return shellItem;
	}

	public static IShellItemArray CreateShellItemArray(IShellItem shellItem)
	{
		Guid iid = typeof(IShellItemArray).GUID;
		HResult.ThrowIfFailed(NativeMethods.SHCreateShellItemArrayFromShellItem(
			shellItem,
			ref iid,
			out IShellItemArray shellItemArray));

		return shellItemArray;
	}

	public static IExecuteCommand CreatePinToFrequentExecuteCommand()
	{
		Guid clsid = ShellGuids.CLSID_PinToFrequentExecute;
		Guid iid = typeof(IExecuteCommand).GUID;
		HResult.ThrowIfFailed(NativeMethods.CoCreateExecuteCommand(
			ref clsid,
			null,
			CLSCTX.InprocServer,
			ref iid,
			out IExecuteCommand executeCommand));

		return executeCommand;
	}
}

internal static class ShellGuids
{
	public static readonly Guid CLSID_PinToFrequentExecute = new("B455F46E-E4AF-4035-B0A4-CF18D2F6F28E");
}

internal static partial class NativeMethods
{
	[LibraryImport("ole32.dll")]
	internal static partial int CoInitializeEx(nint pvReserved, COINIT coInit);

	[LibraryImport("ole32.dll")]
	internal static partial void CoUninitialize();

	[LibraryImport("ole32.dll", EntryPoint = "CoCreateInstance")]
	internal static partial int CoCreateExecuteCommand(
		ref Guid rclsid,
		IUnknownObject? outer,
		CLSCTX clsContext,
		ref Guid riid,
		out IExecuteCommand obj);

	[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	internal static partial int SHCreateItemFromParsingName(
		string path,
		IUnknownObject? bindContext,
		ref Guid riid,
		out IShellItem obj);

	[LibraryImport("shell32.dll")]
	internal static partial int SHCreateShellItemArrayFromShellItem(
		IShellItem shellItem,
		ref Guid riid,
		out IShellItemArray shellItemArray);
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
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IShellItem : IUnknownObject
{
}

[GeneratedComInterface]
[Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IShellItemArray : IUnknownObject
{
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("7F9185B0-CB92-43C5-80A9-92277A4F7B54")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IExecuteCommand : IUnknownObject
{
	[PreserveSig]
	int SetKeyState(uint keyState);

	[PreserveSig]
	int SetParameters(string? parameters);

	[PreserveSig]
	int SetPosition(nint point);

	[PreserveSig]
	int SetShowWindow(int showWindow);

	[PreserveSig]
	int SetNoShowUI([MarshalAs(UnmanagedType.Bool)] bool noShowUi);

	[PreserveSig]
	int SetDirectory(string? directory);

	[PreserveSig]
	int Execute();
}

[GeneratedComInterface]
[Guid("1C9CD5BB-98E9-4491-A60F-31AACC72B83C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IObjectWithSelection : IUnknownObject
{
	[PreserveSig]
	int SetSelection(IShellItemArray shellItemArray);

	[PreserveSig]
	int GetSelection(in Guid riid, out IUnknownObject obj);
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
