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
using Microsoft.Win32.SafeHandles;

return EnumerateOpenWithMenuItemsPoc.Run(args);

internal static class EnumerateOpenWithMenuItemsPoc
{
	public static int Run(string[] args)
	{
		if (args is ["--help"] or ["-h"] or ["/?"])
		{
			Console.WriteLine("Usage: dotnet run --file EnumerateOpenWithMenuItemsPoc.cs -- [file]");
			return 0;
		}

		string path = args.Length > 0
			? args[0]
			: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Branding.svg");

		if (!File.Exists(path))
		{
			Console.Error.WriteLine($"File not found: {path}");
			return 2;
		}

		ComRuntime.Initialize();

		try
		{
			IContextMenu openWithContextMenu = ShellInterop.CreateOpenWithMenu();
			var openWithContextMenu2 = (IContextMenu2)openWithContextMenu;
			var shellExtInit = (IShellExtInit)openWithContextMenu;

			IShellItem shellItem = ShellInterop.CreateShellItem(path);
			IDataObject dataObject = ShellInterop.BindToHandler<IDataObject>(shellItem, ShellGuids.BHID_DataObject);

			HResult.ThrowIfFailed(shellExtInit.Initialize(0, dataObject, 0));

			nint menu = NativeMethods.CreatePopupMenu();
			try
			{
				HResult.ThrowIfFailed(openWithContextMenu.QueryContextMenu(menu, 0, 1, 256, 0));

				nint subMenu = NativeMethods.GetSubMenu(menu, 0);
				if (subMenu == 0)
				{
					return 0;
				}

				HResult.ThrowIfFailed(openWithContextMenu2.HandleMenuMsg(NativeMethods.WM_INITMENUPOPUP, (nuint)subMenu, 0));

				int count = NativeMethods.GetMenuItemCount(subMenu);
				if (count < 0)
				{
					return 0;
				}

				for (uint index = 0; index < count; index++)
				{
					using MenuTextBuffer buffer = MenuTextBuffer.Allocate(256);
					var menuItemInfo = new MENUITEMINFOW
					{
						cbSize = (uint)Marshal.SizeOf<MENUITEMINFOW>(),
						fMask = MENU_ITEM_MASK.String | MENU_ITEM_MASK.Id | MENU_ITEM_MASK.State,
						dwTypeData = buffer.DangerousGetHandle(),
						cch = buffer.Capacity,
					};

					if (NativeMethods.GetMenuItemInfo(subMenu, index, true, ref menuItemInfo))
					{
						Console.WriteLine($"{menuItemInfo.wID}: \"{buffer}\" ({menuItemInfo.fState})");
					}
				}
			}
			finally
			{
				NativeMethods.DestroyMenu(menu);
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

	public static IContextMenu CreateOpenWithMenu()
	{
		Guid clsid = ShellGuids.CLSID_OpenWithMenu;
		Guid iid = typeof(IContextMenu).GUID;
		HResult.ThrowIfFailed(NativeMethods.CoCreateOpenWithMenu(
			ref clsid,
			null,
			CLSCTX.InprocServer,
			ref iid,
			out IContextMenu contextMenu));

		return contextMenu;
	}
}

internal static class ShellGuids
{
	public static readonly Guid BHID_DataObject = new("B8C0BD9F-ED24-455C-83E6-D5390C4FE8C4");
	public static readonly Guid CLSID_OpenWithMenu = new("09799AFB-AD67-11D1-ABCD-00C04FC30936");
}

internal static partial class NativeMethods
{
	public const uint WM_INITMENUPOPUP = 0x0117;

	[LibraryImport("ole32.dll")]
	internal static partial int CoInitializeEx(nint pvReserved, COINIT coInit);

	[LibraryImport("ole32.dll")]
	internal static partial void CoUninitialize();

	[LibraryImport("ole32.dll", EntryPoint = "CoCreateInstance")]
	internal static partial int CoCreateOpenWithMenu(
		ref Guid rclsid,
		IUnknownObject? outer,
		CLSCTX clsContext,
		ref Guid riid,
		out IContextMenu obj);

	[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	internal static partial int SHCreateItemFromParsingName(
		string path,
		IUnknownObject? bindContext,
		ref Guid riid,
		out IShellItem obj);

	[LibraryImport("user32.dll")]
	internal static partial nint CreatePopupMenu();

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool DestroyMenu(nint menu);

	[LibraryImport("user32.dll")]
	internal static partial nint GetSubMenu(nint menu, int position);

	[LibraryImport("user32.dll")]
	internal static partial int GetMenuItemCount(nint menu);

	[LibraryImport("user32.dll", EntryPoint = "GetMenuItemInfoW")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool GetMenuItemInfo(
		nint menu,
		uint item,
		[MarshalAs(UnmanagedType.Bool)] bool byPosition,
		ref MENUITEMINFOW menuItemInfo);
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

internal sealed class MenuTextBuffer : SafeHandleZeroOrMinusOneIsInvalid
{
	private MenuTextBuffer(uint capacity)
		: base(ownsHandle: true)
	{
		Capacity = capacity;
		SetHandle(Marshal.AllocHGlobal(checked((int)(capacity * sizeof(char)))));
	}

	public uint Capacity { get; }

	public static MenuTextBuffer Allocate(uint capacity)
		=> new(capacity);

	public override string ToString()
		=> Marshal.PtrToStringUni(handle) ?? string.Empty;

	protected override bool ReleaseHandle()
	{
		Marshal.FreeHGlobal(handle);
		return true;
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
	int GetDisplayName(uint sigdnName, out string? name);

	[PreserveSig]
	int GetAttributes(uint attributeMask, out uint attributes);

	[PreserveSig]
	int Compare(IShellItem other, uint hint, out int order);
}

[GeneratedComInterface]
[Guid("000214E4-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IContextMenu : IUnknownObject
{
	[PreserveSig]
	int QueryContextMenu(nint menu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint flags);

	[PreserveSig]
	int InvokeCommand(nint commandInfo);

	[PreserveSig]
	int GetCommandString(nuint idCommand, uint type, nint reserved, nint name, uint nameChars);
}

[GeneratedComInterface]
[Guid("000214F4-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IContextMenu2 : IContextMenu
{
	[PreserveSig]
	int HandleMenuMsg(uint message, nuint wParam, nint lParam);
}

[GeneratedComInterface]
[Guid("000214E8-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IShellExtInit : IUnknownObject
{
	[PreserveSig]
	int Initialize(nint folderPidl, IDataObject dataObject, nint progIdKey);
}

[GeneratedComInterface]
[Guid("0000010E-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IDataObject : IUnknownObject
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

[Flags]
internal enum MENU_ITEM_MASK : uint
{
	State = 0x1,
	Id = 0x2,
	String = 0x40,
}

[Flags]
internal enum MENU_ITEM_STATE : uint
{
	Unchecked = 0x0,
	Checked = 0x8,
	Disabled = 0x3,
	Enabled = 0x0,
}

[StructLayout(LayoutKind.Sequential)]
internal struct MENUITEMINFOW
{
	public uint cbSize;
	public MENU_ITEM_MASK fMask;
	public uint fType;
	public MENU_ITEM_STATE fState;
	public uint wID;
	public nint hSubMenu;
	public nint hbmpChecked;
	public nint hbmpUnchecked;
	public nuint dwItemData;
	public nint dwTypeData;
	public uint cch;
	public nint hbmpItem;
}
