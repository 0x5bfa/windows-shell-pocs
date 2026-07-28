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

return ReplicateFileItemActivationPoc.Run(args);

internal static class ReplicateFileItemActivationPoc
{
	public static int Run(string[] args)
	{
		if (args is ["--help"] or ["-h"] or ["/?"] || args.Length is < 1 or > 2)
		{
			Console.WriteLine("Usage: dotnet run --file ReplicateFileItemActivationPoc.cs -- [--dry-run] <file>");
			Console.WriteLine("       --dry-run builds the Shell context menu but does not invoke Open.");
			return args.Length == 0 || args.Length > 2 ? 2 : 0;
		}

		bool dryRun = args[0].Equals("--dry-run", StringComparison.OrdinalIgnoreCase);
		if ((dryRun && args.Length != 2) || (!dryRun && args.Length != 1))
		{
			Console.Error.WriteLine("Specify exactly one file, optionally prefixed by --dry-run.");
			return 2;
		}
		string path = Path.GetFullPath(dryRun ? args[1] : args[0]);
		if (!File.Exists(path))
		{
			Console.Error.WriteLine($"File not found: {path}");
			return 2;
		}

		ComRuntime.Initialize();
		try
		{
			return FileItemActivation.Execute(path, dryRun);
		}
		finally
		{
			ComRuntime.Uninitialize();
		}
	}
}

internal static class FileItemActivation
{
	public static int Execute(string path, bool dryRun)
	{
		using SafePidlHandle absolutePidl = ShellNative.ParseDisplayName(path);
		Guid shellFolderIid = typeof(IShellFolder).GUID;
		HResult.ThrowIfFailed(ShellNative.SHBindToParent(
			absolutePidl.DangerousGetHandle(),
			ref shellFolderIid,
			out IShellFolder parentFolder,
			out nint childPidl));

		try
		{
			nint childPidlArray = Marshal.AllocHGlobal(IntPtr.Size);
			Marshal.WriteIntPtr(childPidlArray, childPidl);
			try
			{
			Guid contextMenuIid = typeof(IContextMenu).GUID;
			HResult.ThrowIfFailed(parentFolder.GetUIObjectOf(
				0,
				1,
				childPidlArray,
				ref contextMenuIid,
				0,
				out IContextMenu contextMenu));

			using SafeMenuHandle menu = SafeMenuHandle.Create();
			HResult.ThrowIfFailed(contextMenu.QueryContextMenu(
				menu.DangerousGetHandle(),
				0,
				1,
				0x7FFF,
				0));

			Console.WriteLine("Explorer-style file activation");
			Console.WriteLine($"Path: {path}");
			Console.WriteLine("Pipeline: IShellItem/PIDL -> parent IShellFolder -> IContextMenu -> open");

			if (dryRun)
			{
				Console.WriteLine("Dry run: context menu was created; Open was not invoked.");
				return 0;
			}

			using CommandInfo command = CommandInfo.Open();
			HResult.ThrowIfFailed(contextMenu.InvokeCommand(command.DangerousGetHandle()));
			Console.WriteLine("IContextMenu::InvokeCommand(open) succeeded.");
			return 0;
			}
			finally
			{
				Marshal.FreeHGlobal(childPidlArray);
			}
		}
		finally
		{
			if (childPidl != 0)
			{
				ShellNative.ILFree(childPidl);
			}
		}
	}
}

internal static partial class ShellNative
{
	[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	internal static partial int SHParseDisplayName(
		string name,
		IUnknownObject? bindContext,
		out nint pidl,
		uint attributesIn,
		out uint attributesOut);

	[LibraryImport("shell32.dll")]
	internal static partial int SHBindToParent(
		nint pidl,
		ref Guid riid,
		out IShellFolder parent,
		out nint child);

	[LibraryImport("shell32.dll")]
	internal static partial void ILFree(nint pidl);

	public static SafePidlHandle ParseDisplayName(string path)
	{
		HResult.ThrowIfFailed(SHParseDisplayName(path, null, out nint pidl, 0, out _));
		return SafePidlHandle.Attach(pidl);
	}
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

internal static partial class NativeMethods
{
	[LibraryImport("ole32.dll")]
	internal static partial int CoInitializeEx(nint pvReserved, COINIT coInit);

	[LibraryImport("ole32.dll")]
	internal static partial void CoUninitialize();

	[LibraryImport("user32.dll")]
	internal static partial nint CreatePopupMenu();

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool DestroyMenu(nint menu);
}

[GeneratedComInterface]
[Guid("00000000-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IUnknownObject
{
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("000214E6-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IShellFolder : IUnknownObject
{
	[PreserveSig]
	int ParseDisplayName(nint hwnd, IUnknownObject? bindContext, string displayName, out uint eaten, out nint pidl, out uint attributes);

	[PreserveSig]
	int EnumObjects(nint hwnd, uint flags, out IUnknownObject enumerator);

	[PreserveSig]
	int BindToObject(nint pidl, IUnknownObject? bindContext, in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int BindToStorage(nint pidl, IUnknownObject? bindContext, in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int CompareIDs(nint lParam, nint pidl1, nint pidl2);

	[PreserveSig]
	int CreateViewObject(nint hwnd, in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int GetAttributesOf(uint count, nint childPidls, ref uint attributes);

	[PreserveSig]
	int GetUIObjectOf(nint hwnd, uint count, nint childPidls, ref Guid riid, nint reserved, out IContextMenu obj);

	[PreserveSig]
	int GetDisplayNameOf(nint pidl, uint flags, out nint name);

	[PreserveSig]
	int SetNameOf(nint hwnd, nint pidl, string name, uint flags, out nint newPidl);
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

[StructLayout(LayoutKind.Sequential)]
internal struct CMINVOKECOMMANDINFOEX
{
	public uint cbSize;
	public uint fMask;
	public nint hwnd;
	public nint lpVerb;
	public nint lpParameters;
	public nint lpDirectory;
	public int nShow;
	public uint dwHotKey;
	public nint hIcon;
	public nint lpTitle;
	public nint lpVerbW;
	public nint lpParametersW;
	public nint lpDirectoryW;
	public nint lpTitleW;
	public POINT ptInvoke;
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
	public int x;
	public int y;
}

internal sealed class CommandInfo : SafeHandleZeroOrMinusOneIsInvalid
{
	private nint verb;
	private nint verbW;

	private CommandInfo()
		: base(true)
	{
	}

	public static CommandInfo Open()
	{
		var result = new CommandInfo();
		result.verb = Marshal.StringToCoTaskMemAnsi("open");
		result.verbW = Marshal.StringToCoTaskMemUni("open");
		var value = new CMINVOKECOMMANDINFOEX
		{
			cbSize = (uint)Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
			fMask = 0x00004000, // CMIC_MASK_UNICODE
			lpVerb = result.verb,
			lpVerbW = result.verbW,
			nShow = 1,
		};
		result.SetHandle(Marshal.AllocHGlobal(Marshal.SizeOf(value)));
		Marshal.StructureToPtr(value, result.handle, false);
		return result;
	}

	protected override bool ReleaseHandle()
	{
		Marshal.DestroyStructure<CMINVOKECOMMANDINFOEX>(handle);
		Marshal.FreeHGlobal(handle);
		Marshal.FreeCoTaskMem(verb);
		Marshal.FreeCoTaskMem(verbW);
		return true;
	}
}

internal sealed class SafeMenuHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	private SafeMenuHandle()
		: base(true)
	{
	}

	public static SafeMenuHandle Create()
	{
		var result = new SafeMenuHandle();
		result.SetHandle(NativeMethods.CreatePopupMenu());
		return result;
	}

	protected override bool ReleaseHandle()
		=> NativeMethods.DestroyMenu(handle);
}

internal sealed class SafePidlHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	private SafePidlHandle()
		: base(true)
	{
	}

	public static SafePidlHandle Attach(nint value)
	{
		var result = new SafePidlHandle();
		result.SetHandle(value);
		return result;
	}

	protected override bool ReleaseHandle()
	{
		ShellNative.ILFree(handle);
		return true;
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

[Flags]
internal enum COINIT : uint
{
	ApartmentThreaded = 0x2,
}
