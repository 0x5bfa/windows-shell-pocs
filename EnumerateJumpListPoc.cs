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

return EnumerateJumpListPoc.Run(args);

internal static class EnumerateJumpListPoc
{
	private const string AppUserModelId = "ShellBrowserConsoleApp";

	public static int Run(string[] args)
	{
		if (args is ["--help"] or ["-h"] or ["/?"])
		{
			Console.WriteLine("Usage: dotnet run --file EnumerateJumpListPoc.cs -- [file]");
			return 0;
		}

		ComRuntime.Initialize();

		try
		{
			ICustomDestinationList customDestinationList = ShellInterop.CreateCustomDestinationList();

			HResult.ThrowIfFailed(NativeMethods.SetCurrentProcessExplicitAppUserModelID(AppUserModelId));
			HResult.ThrowIfFailed(customDestinationList.SetAppID(AppUserModelId));

			Guid objectArrayIid = typeof(IObjectArray).GUID;
			HResult.ThrowIfFailed(customDestinationList.BeginList(out uint minSlots, in objectArrayIid, out IUnknownObject removedItems));
			HResult.ThrowIfFailed(((IObjectArray)removedItems).GetCount(out uint removedCount));

			Console.WriteLine($"Min slots: {minSlots}");
			Console.WriteLine($"Removed destinations: {removedCount}");

			string path = args.Length > 0
				? args[0]
				: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Microsoft_Sample_1.txt");

			if (File.Exists(path))
			{
				IObjectCollection collection = ShellInterop.CreateObjectCollection();
				collection.AddObject(ShellInterop.CreateShellItem(path));

				HResult.ThrowIfFailed(customDestinationList.AppendCategory("Recent items", collection));
			}
			else
			{
				Console.WriteLine($"Append skipped; file not found: {path}");
			}

			HResult.ThrowIfFailed(customDestinationList.CommitList());
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

	public static ICustomDestinationList CreateCustomDestinationList()
	{
		Guid clsid = ShellGuids.CLSID_CustomDestinationList;
		Guid iid = typeof(ICustomDestinationList).GUID;
		HResult.ThrowIfFailed(NativeMethods.CoCreateCustomDestinationList(
			ref clsid,
			null,
			CLSCTX.InprocServer,
			ref iid,
			out ICustomDestinationList customDestinationList));

		return customDestinationList;
	}

	public static IObjectCollection CreateObjectCollection()
	{
		Guid clsid = ShellGuids.CLSID_EnumerableObjectCollection;
		Guid iid = typeof(IObjectCollection).GUID;
		HResult.ThrowIfFailed(NativeMethods.CoCreateObjectCollection(
			ref clsid,
			null,
			CLSCTX.InprocServer,
			ref iid,
			out IObjectCollection objectCollection));

		return objectCollection;
	}
}

internal static class ShellGuids
{
	public static readonly Guid CLSID_CustomDestinationList = new("77F10CF0-3DB5-4966-B520-B7C54FD35ED6");
	public static readonly Guid CLSID_EnumerableObjectCollection = new("2D3468C1-36A7-43B6-AC24-D3F02FD9607A");
}

internal static partial class NativeMethods
{
	[LibraryImport("ole32.dll")]
	internal static partial int CoInitializeEx(nint pvReserved, COINIT coInit);

	[LibraryImport("ole32.dll")]
	internal static partial void CoUninitialize();

	[LibraryImport("ole32.dll", EntryPoint = "CoCreateInstance")]
	internal static partial int CoCreateCustomDestinationList(
		ref Guid rclsid,
		IUnknownObject? outer,
		CLSCTX clsContext,
		ref Guid riid,
		out ICustomDestinationList obj);

	[LibraryImport("ole32.dll", EntryPoint = "CoCreateInstance")]
	internal static partial int CoCreateObjectCollection(
		ref Guid rclsid,
		IUnknownObject? outer,
		CLSCTX clsContext,
		ref Guid riid,
		out IObjectCollection obj);

	[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	internal static partial int SHCreateItemFromParsingName(
		string path,
		IUnknownObject? bindContext,
		ref Guid riid,
		out IShellItem obj);

	[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	internal static partial int SetCurrentProcessExplicitAppUserModelID(string appId);
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

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("6332DEBF-87B5-4670-90C0-5E57B408A49E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ICustomDestinationList : IUnknownObject
{
	[PreserveSig]
	int SetAppID(string appId);

	[PreserveSig]
	int BeginList(out uint minSlots, in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int AppendCategory(string category, IObjectArray objectArray);

	[PreserveSig]
	int AppendKnownCategory(uint category);

	[PreserveSig]
	int AddUserTasks(IObjectArray objectArray);

	[PreserveSig]
	int CommitList();

	[PreserveSig]
	int GetRemovedDestinations(in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int DeleteList(string appId);

	[PreserveSig]
	int AbortList();
}

[GeneratedComInterface]
[Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IObjectArray : IUnknownObject
{
	[PreserveSig]
	int GetCount(out uint count);

	[PreserveSig]
	int GetAt(uint index, in Guid riid, out IUnknownObject obj);
}

[GeneratedComInterface]
[Guid("5632B1A4-E38A-400A-928A-D4CD63230295")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IObjectCollection : IObjectArray
{
	[PreserveSig]
	int AddObject(IUnknownObject obj);

	[PreserveSig]
	int AddFromArray(IObjectArray source);

	[PreserveSig]
	int RemoveObjectAt(uint index);

	[PreserveSig]
	int Clear();
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
