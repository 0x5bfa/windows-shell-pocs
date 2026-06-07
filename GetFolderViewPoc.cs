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

return GetFolderViewPoc.Run(args);

internal static class GetFolderViewPoc
{
	public static int Run(string[] args)
	{
		if (args is ["--help"] or ["-h"] or ["/?"])
		{
			Console.WriteLine("Usage: dotnet run --file GetFolderViewPoc.cs -- [folder]");
			return 0;
		}

		string folderPath = args.Length > 0 ? args[0] : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		ComRuntime.Initialize();

		try
		{
			IShellItem shellItem = ShellInterop.CreateShellItem(folderPath);
			IShellFolder shellFolder = ShellInterop.BindToHandler<IShellFolder>(shellItem, ShellGuids.BHID_SFObject);

			Guid shellViewIid = typeof(IShellView).GUID;
			HResult.ThrowIfFailed(shellFolder.CreateViewObject(0, in shellViewIid, out IShellView shellView));
			Console.WriteLine($"IShellView: {shellView.GetType().Name}");

			try
			{
				var folderView = (IFolderView)shellView;
				Console.WriteLine($"IFolderView: {folderView.GetType().Name}");
			}
			catch (InvalidCastException)
			{
				Console.WriteLine("IFolderView: not supported");
			}

			IEnumShellItems enumShellItems = ShellInterop.BindToHandler<IEnumShellItems>(shellItem, ShellGuids.BHID_EnumItems);
			while (enumShellItems.Next(1, out IShellItem childShellItem, out uint fetched) == HResult.S_OK && fetched == 1)
			{
				HResult.ThrowIfFailed(childShellItem.GetDisplayName(SIGDN.ParentRelativeForUI, out string? name));
				Console.WriteLine(name);
			}

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

	public static T BindToHandler<T>(IShellItem shellItem, Guid handlerId)
		where T : IUnknownObject
	{
		Guid iid = typeof(T).GUID;
		HResult.ThrowIfFailed(shellItem.BindToHandler(null, in handlerId, in iid, out IUnknownObject obj));

		return (T)obj;
	}
}

internal static class ShellGuids
{
	public static readonly Guid BHID_EnumItems = new("94F60519-2850-4924-AA5A-D15E84868039");
	public static readonly Guid BHID_SFObject = new("3981E224-F559-11D3-8E3A-00C04F6837D5");
}

internal static partial class NativeMethods
{
	[LibraryImport("ole32.dll")]
	internal static partial int CoInitializeEx(nint pvReserved, COINIT coInit);

	[LibraryImport("ole32.dll")]
	internal static partial void CoUninitialize();

	[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	internal static partial int SHCreateItemFromParsingName(
		string path,
		IUnknownObject? bindContext,
		ref Guid riid,
		out IShellItem obj);
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
[Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IShellItem : IUnknownObject
{
	[PreserveSig]
	int BindToHandler(IUnknownObject? bindContext, in Guid handlerId, in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int GetParent(out IShellItem parent);

	[PreserveSig]
	int GetDisplayName(SIGDN sigdnName, out string? name);

	[PreserveSig]
	int GetAttributes(uint attributeMask, out uint attributes);

	[PreserveSig]
	int Compare(IShellItem other, uint hint, out int order);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("000214E6-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IShellFolder : IUnknownObject
{
	[PreserveSig]
	int ParseDisplayName(nint hwnd, IUnknownObject? bindContext, string displayName, out uint eaten, out nint pidl, ref uint attributes);

	[PreserveSig]
	int EnumObjects(nint hwnd, uint flags, out IUnknownObject enumIdList);

	[PreserveSig]
	int BindToObject(nint pidl, IUnknownObject? bindContext, in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int BindToStorage(nint pidl, IUnknownObject? bindContext, in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int CompareIDs(nint lParam, nint pidl1, nint pidl2);

	[PreserveSig]
	int CreateViewObject(nint hwndOwner, in Guid riid, out IShellView obj);
}

[GeneratedComInterface]
[Guid("000214E3-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IShellView : IUnknownObject
{
}

[GeneratedComInterface]
[Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IFolderView : IUnknownObject
{
}

[GeneratedComInterface]
[Guid("70629033-E363-4A28-A567-0DB78006E6D7")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IEnumShellItems : IUnknownObject
{
	[PreserveSig]
	int Next(uint count, out IShellItem item, out uint fetched);

	[PreserveSig]
	int Skip(uint count);

	[PreserveSig]
	int Reset();

	[PreserveSig]
	int Clone(out IEnumShellItems enumShellItems);
}

[Flags]
internal enum COINIT : uint
{
	Multithreaded = 0x0,
	ApartmentThreaded = 0x2,
	DisableOle1Dde = 0x4,
	SpeedOverMemory = 0x8,
}

internal enum SIGDN : int
{
	ParentRelativeForUI = unchecked((int)0x80094001),
}
