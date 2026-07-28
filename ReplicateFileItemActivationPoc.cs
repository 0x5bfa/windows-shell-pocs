#:sdk Microsoft.NET.Sdk
#:package Microsoft.Windows.CsWin32@0.3.298
#:package System.CommandLine@2.0.10
#:property TargetFramework=net10.0-windows
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property AllowUnsafeBlocks=true
#:property PublishAot=true
#:property InvariantGlobalization=true
#:property CsWin32RunAsBuildTask=true
#:property DisableRuntimeMarshalling=true

using System.CommandLine;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

const uint DefaultCommandIdFirst = 1;
const uint DefaultCommandIdLast = 0x7FFF;
const uint NoDefaultCommand = 0xFFFFFFFF;

// CDefView::_GetExplorerFlag() contributes CMF_EXPLORE in Explorer.
// SHInvokeCommandOnBackgroundThread contributes CMF_OPTIMIZEFORINVOKE.
// SHInvokeCommandOnContextMenu2 adds CMF_DEFAULTONLY for a null/default verb.
const uint ExplorerDefaultQueryFlags =
	0x00000001 | // CMF_DEFAULTONLY
	0x00000004 | // CMF_EXPLORE
	0x00000800;  // CMF_OPTIMIZEFORINVOKE

// Normal, unmodified activation as assembled by
// SHInvokeCommandOnBackgroundThread + SHInvokeCommandOnContextMenu2.
const uint ExplorerDefaultInvokeMask =
	0x00000100 | // CMIC_MASK_NOASYNC
	0x00004000 | // CMIC_MASK_UNICODE
	0x04000000 | // CMIC_MASK_FLAG_LOG_USAGE
	0x20000000;  // CMIC_MASK_PTINVOKE

var fileArgument = new Argument<FileInfo>("file")
{
	Description = "The file item to activate."
};
var rootCommand = new RootCommand(
	"Invokes the selected Shell item's context-menu default command like Explorer.")
{
	fileArgument
};

rootCommand.SetAction(parseResult =>
{
	FileInfo file = parseResult.GetValue(fileArgument)
		?? throw new ArgumentNullException(nameof(fileArgument));

	ActivateFileItem(file.FullName);
	return 0;
});

return rootCommand.Parse(args).Invoke();

[STAThread]
static unsafe void ActivateFileItem(string path)
{
	HRESULT initializeResult = PInvoke.CoInitializeEx(
		null,
		COINIT.COINIT_APARTMENTTHREADED);
	bool uninitialize = initializeResult == HRESULT.S_OK ||
		initializeResult == HRESULT.S_FALSE;

	// initializeResult.ThrowOnFailure();

	try
	{
		PInvoke.SHCreateItemFromParsingName(
			path,
			null,
			out IShellItem shellItem).ThrowOnFailure();

		PInvoke.SHCreateShellItemArrayFromShellItem(
			shellItem,
			out IShellItemArray selection).ThrowOnFailure();

		Guid selectionIid = typeof(IShellItemArray).GUID;
		global::Windows.Win32.PInvoke.CoMarshalInterThreadInterfaceInStream(
			selectionIid,
			selection,
			out global::Windows.Win32.System.Com.IStream selectionStream).ThrowOnFailure();

		HWND ownerWindow = PInvoke.GetForegroundWindow();
		uint messagePosition = PInvoke.GetMessagePos();
		Exception? workerException = null;
		using EventWaitHandle completion = new(
			false,
			EventResetMode.ManualReset);

		Thread worker = new(() =>
		{
			try
			{
				InvokeSelectionOnBackgroundThread(
					selectionStream,
					path,
					ownerWindow,
					messagePosition);
			}
			catch (Exception exception)
			{
				workerException = exception;
			}
			finally
			{
				completion.Set();
			}
		});

		worker.SetApartmentState(ApartmentState.STA);
		worker.Start();

		Span<HANDLE> completionHandle =
		[
			(HANDLE)completion.SafeWaitHandle.DangerousGetHandle()
		];
		const COWAIT_FLAGS waitFlags =
			COWAIT_FLAGS.COWAIT_DISPATCH_CALLS |
			COWAIT_FLAGS.COWAIT_DISPATCH_WINDOW_MESSAGES;

		global::Windows.Win32.PInvoke.CoWaitForMultipleHandles(
			(uint)waitFlags,
			PInvoke.INFINITE,
			completionHandle,
			out _).ThrowOnFailure();

		worker.Join();
		GC.KeepAlive(selectionStream);

		if (workerException is not null)
		{
			throw new InvalidOperationException(
				"The background Shell invocation failed.",
				workerException);
		}
	}
	finally
	{
		if (uninitialize)
		{
			PInvoke.CoUninitialize();
		}
	}
}

static unsafe void InvokeSelectionOnBackgroundThread(
	global::Windows.Win32.System.Com.IStream selectionStream,
	string path,
	HWND ownerWindow,
	uint messagePosition)
{
	HRESULT initializeResult = PInvoke.CoInitializeEx(
		null,
		COINIT.COINIT_APARTMENTTHREADED);
	bool uninitialize = initializeResult == HRESULT.S_OK ||
		initializeResult == HRESULT.S_FALSE;

	// initializeResult.ThrowOnFailure();

	try
	{
		global::Windows.Win32.PInvoke
			.CoGetInterfaceAndReleaseStream<IShellItemArray>(
				selectionStream,
				out IShellItemArray selection).ThrowOnFailure();

		selection.BindToHandler<IContextMenu>(
			null,
			PInvoke.BHID_SFUIObject,
			out IContextMenu contextMenu).ThrowOnFailure();

		InvokeDefaultContextMenuCommand(
			contextMenu,
			path,
			ownerWindow,
			messagePosition);
	}
	finally
	{
		if (uninitialize)
		{
			PInvoke.CoUninitialize();
		}
	}
}

static unsafe void InvokeDefaultContextMenuCommand(
	IContextMenu contextMenu,
	string path,
	HWND ownerWindow,
	uint messagePosition)
{
	HMENU menu = PInvoke.CreatePopupMenu();
	if (menu.IsNull)
	{
		Marshal.ThrowExceptionForHR(
			Marshal.GetHRForLastWin32Error());
	}

	try
	{
		contextMenu.QueryContextMenu(
			menu,
			0,
			DefaultCommandIdFirst,
			DefaultCommandIdLast,
			ExplorerDefaultQueryFlags).ThrowOnFailure();

		uint commandId = PInvoke.GetMenuDefaultItem(menu, 0, 0);
		if (commandId == NoDefaultCommand)
		{
			Marshal.ThrowExceptionForHR(unchecked((int)0x80004005));
		}

		Console.WriteLine($"Path: {path}");
		Console.WriteLine($"Default command ID: {commandId}");

		nuint commandOrdinal = commandId - DefaultCommandIdFirst;
		string? workingDirectory = Path.GetDirectoryName(path);
		nint ansiWorkingDirectory = workingDirectory is null
			? 0
			: Marshal.StringToCoTaskMemAnsi(workingDirectory);

		try
		{
			Point invokePoint = new(
				unchecked((short)(messagePosition & 0xFFFF)),
				unchecked((short)(messagePosition >> 16)));

			fixed (char* workingDirectoryPointer = workingDirectory)
			{
				CMINVOKECOMMANDINFOEX invoke = default;
				invoke.cbSize = (uint)sizeof(CMINVOKECOMMANDINFOEX);
				invoke.fMask = ExplorerDefaultInvokeMask;
				invoke.hwnd = ownerWindow;
				invoke.lpVerb = (PCSTR)(byte*)commandOrdinal;
				invoke.lpDirectory = (PCSTR)(byte*)ansiWorkingDirectory;
				invoke.nShow = 1; // SW_SHOWNORMAL
				invoke.lpDirectoryW = workingDirectoryPointer;
				invoke.ptInvoke = invokePoint;

				ref CMINVOKECOMMANDINFO baseInvoke =
					ref Unsafe.As<CMINVOKECOMMANDINFOEX, CMINVOKECOMMANDINFO>(
						ref invoke);

				contextMenu.InvokeCommand(baseInvoke).ThrowOnFailure();
			}
		}
		finally
		{
			if (ansiWorkingDirectory != 0)
			{
				Marshal.FreeCoTaskMem(ansiWorkingDirectory);
			}
		}
	}
	finally
	{
		PInvoke.DestroyMenu(menu);
	}
}
