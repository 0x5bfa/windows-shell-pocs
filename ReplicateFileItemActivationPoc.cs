#:sdk Microsoft.NET.Sdk
#:package Microsoft.Windows.CsWin32@0.3.298
#:property TargetFramework=net10.0-windows
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property AllowUnsafeBlocks=true
#:property PublishAot=false
#:property InvariantGlobalization=true
#:property CsWin32RunAsBuildTask=true
#:property DisableRuntimeMarshalling=true

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;

return ReplicateFileItemActivationPoc.Run(args);

internal static class ReplicateFileItemActivationPoc
{
	public unsafe static int Run(string[] args)
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

		int hr = PInvoke.CoInitializeEx(null, COINIT.COINIT_APARTMENTTHREADED);
		bool shouldUninitialize = hr == HRESULT.S_OK || hr == HRESULT.S_FALSE;

		if (hr != HRESULT.RPC_E_CHANGED_MODE)
		{
			// Throw an exception
		}

		try
		{
			return FileItemActivation.Execute(path, dryRun);
		}
		finally
		{
			if (shouldUninitialize)
			{
				PInvoke.CoUninitialize();
			}
		}
	}
}

internal static unsafe class FileItemActivation
{
	public static int Execute(string path, bool dryRun)
	{
		PInvoke.SHParseDisplayName(path, null, out ITEMIDLIST* pidl, 0, out _);
		PInvoke.SHBindToParent(*pidl, typeof(IShellFolder).GUID, out var parentFolderObj, out ITEMIDLIST* childPidlPointer);

		nint childPidl = (nint)childPidlPointer;
		var parentFolder = (IShellFolder)parentFolderObj;

		try
		{
			nint childPidlArray = Marshal.AllocHGlobal(IntPtr.Size);
			Marshal.WriteIntPtr(childPidlArray, childPidl);
			try
			{
				parentFolder.GetUIObjectOf(HWND.Null, 1, (ITEMIDLIST**)childPidlArray, typeof(IContextMenu).GUID, out var contextMenuObj);
				var contextMenu = (IContextMenu)contextMenuObj;
	
				var menu = PInvoke.CreatePopupMenu();
				contextMenu.QueryContextMenu(menu, 0, 1, 0x7FFF, 0);

				Console.WriteLine("Explorer-style file activation");
				Console.WriteLine($"Path: {path}");
				Console.WriteLine("Pipeline: IShellItem/PIDL -> parent IShellFolder -> IContextMenu -> open");

				if (dryRun)
				{
					Console.WriteLine("Dry run: context menu was created; Open was not invoked.");
					return 0;
				}

				var verb = Marshal.StringToCoTaskMemAnsi("open");
				var verbW = Marshal.StringToCoTaskMemUni("open");
				CMINVOKECOMMANDINFO value = default;
				value.cbSize = (uint)sizeof(CMINVOKECOMMANDINFO);
				value.fMask = 0x00004000; // CMIC_MASK_UNICODE
				value.lpVerb = (PCSTR)(byte*)verb;
				value.nShow = 1;

				contextMenu.InvokeCommand(value);
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
				PInvoke.ILFree((ITEMIDLIST*)childPidl);
			}
		}
	}
}
