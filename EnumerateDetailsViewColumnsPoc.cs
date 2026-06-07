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

return EnumerateDetailsViewColumnsPoc.Run(args);

internal static class EnumerateDetailsViewColumnsPoc
{
	public static int Run(string[] args)
	{
		if (args is ["--help"] or ["-h"] or ["/?"])
		{
			Console.WriteLine("Usage: dotnet run --file EnumerateDetailsViewColumnsPoc.cs -- [path]");
			return 0;
		}

		string path = args.Length > 0 ? args[0] : "C:\\";
		ComRuntime.Initialize();

		try
		{
			IShellItem shellItem = ShellInterop.CreateShellItem(path);
			IShellFolder shellFolder = ShellInterop.BindToHandler<IShellFolder>(shellItem, ShellGuids.BHID_SFObject);

			Guid shellViewIid = typeof(IShellView).GUID;
			HResult.ThrowIfFailed(shellFolder.CreateViewObject(0, in shellViewIid, out IShellView shellView));

			var columnManager = (IColumnManager)shellView;
			HResult.ThrowIfFailed(columnManager.GetColumnCount(CM_ENUM_FLAGS.All, out uint columnCount));

			var propertyKeys = new PROPERTYKEY[columnCount];
			HResult.ThrowIfFailed(columnManager.GetColumns(CM_ENUM_FLAGS.All, propertyKeys, columnCount));

			foreach (PROPERTYKEY propKey in propertyKeys)
			{
				IPropertyDescription propertyDescription = ShellInterop.GetPropertyDescription(propKey);

				propertyDescription.GetDisplayName(out string? displayName);
				propertyDescription.GetDefaultColumnWidth(out uint width);

				Console.WriteLine($"{propKey.fmtid} ({propKey.pid}): name(\"{displayName}\"), width({width})");
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

	public static IPropertyDescription GetPropertyDescription(PROPERTYKEY propertyKey)
	{
		Guid iid = typeof(IPropertyDescription).GUID;
		HResult.ThrowIfFailed(NativeMethods.PSGetPropertyDescription(
			in propertyKey,
			ref iid,
			out IPropertyDescription propertyDescription));

		return propertyDescription;
	}
}

internal static class ShellGuids
{
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

	[LibraryImport("propsys.dll")]
	internal static partial int PSGetPropertyDescription(
		in PROPERTYKEY propertyKey,
		ref Guid riid,
		out IPropertyDescription propertyDescription);
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
	int EnumObjects(nint hwnd, SHCONTF flags, out IUnknownObject enumIdList);

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
[Guid("D8EC27BB-3F3B-4042-B10A-4ACFD924D453")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IColumnManager : IUnknownObject
{
	[PreserveSig]
	int SetColumnInfo(in PROPERTYKEY propertyKey, nint columnInfo);

	[PreserveSig]
	int GetColumnInfo(in PROPERTYKEY propertyKey, nint columnInfo);

	[PreserveSig]
	int GetColumnCount(CM_ENUM_FLAGS flags, out uint count);

	[PreserveSig]
	int GetColumns(
		CM_ENUM_FLAGS flags,
		[Out, MarshalUsing(CountElementName = nameof(count))] PROPERTYKEY[] propertyKeys,
		uint count);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("6F79D558-3E96-4549-A1D1-7D75D2288814")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IPropertyDescription : IUnknownObject
{
	[PreserveSig]
	int GetPropertyKey(out PROPERTYKEY propertyKey);

	[PreserveSig]
	int GetCanonicalName(out string? name);

	[PreserveSig]
	int GetPropertyType(out ushort variantType);

	[PreserveSig]
	int GetDisplayName(out string? name);

	[PreserveSig]
	int GetEditInvitation(out string? invite);

	[PreserveSig]
	int GetTypeFlags(uint mask, out uint flags);

	[PreserveSig]
	int GetViewFlags(out uint flags);

	[PreserveSig]
	int GetDefaultColumnWidth(out uint chars);
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
internal enum SHCONTF : uint
{
	Folders = 0x20,
	NonFolders = 0x40,
	IncludeHidden = 0x80,
	Shareable = 0x400,
}

[Flags]
internal enum CM_ENUM_FLAGS : uint
{
	All = 0x1,
	Visible = 0x2,
}

[StructLayout(LayoutKind.Sequential)]
internal struct PROPERTYKEY
{
	public Guid fmtid;
	public uint pid;
}
