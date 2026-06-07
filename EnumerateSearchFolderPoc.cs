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

return EnumerateSearchFolderPoc.Run();

internal static class EnumerateSearchFolderPoc
{
	private const string SearchFolderPath = "Shell:::{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}";

	public static int Run()
	{
		ComRuntime.Initialize();

		try
		{
			IShellItem searchFolder = ShellInterop.CreateShellItem(SearchFolderPath);
			IEnumShellItems enumShellItems = ShellInterop.BindToHandler<IEnumShellItems>(searchFolder, ShellGuids.BHID_EnumItems);

			int count = 0;
			while (enumShellItems.Next(1, out IShellItem childShellItem, out uint fetched) == HResult.S_OK && fetched == 1)
			{
				HResult.ThrowIfFailed(childShellItem.GetDisplayName(SIGDN.ParentRelativeForUI, out string? name));
				childShellItem.GetAttributes(SFGAO_FLAGS.Hidden, out SFGAO_FLAGS attributes);

				Console.WriteLine($"{name} ({attributes})");
				count++;
			}

			Console.WriteLine();
			Console.WriteLine($"Count: {count}");
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
	int GetAttributes(SFGAO_FLAGS attributeMask, out SFGAO_FLAGS attributes);

	[PreserveSig]
	int Compare(IShellItem other, uint hint, out int order);
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

[Flags]
internal enum SFGAO_FLAGS : uint
{
	None = 0,
	Hidden = 0x00080000,
}
