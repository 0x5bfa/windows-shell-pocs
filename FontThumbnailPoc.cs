#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0-windows
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property AllowUnsafeBlocks=true
#:property DisableRuntimeMarshalling=true
#:property PublishAot=true
#:property InvariantGlobalization=true
#:package System.Drawing.Common@10.0.0

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.Win32.SafeHandles;

return FontThumbnailPoc.Run(args);

internal static class FontThumbnailPoc
{
	public static int Run(string[] args)
	{
		if (args is ["--help"] or ["-h"] or ["/?"])
		{
			Console.WriteLine("""
Usage:
  dotnet run --file FontThumbnailPoc.cs -- [font family/name or font file path] [output.png] [size]

Examples:
  dotnet run --file FontThumbnailPoc.cs -- Arial .\Arial.png 256
  dotnet run --file FontThumbnailPoc.cs -- "Arial Rounded MT Bold" .\ArialRounded.png 256
  dotnet run --file FontThumbnailPoc.cs -- C:\Windows\Fonts\arial.ttf .\arial-file.png 256
""");
			return 0;
		}

		string input = args.Length > 0 ? args[0] : "Arial";
		int requestedSize = args.Length > 2 && int.TryParse(args[2], out int parsedSize)
			? Math.Clamp(parsedSize, 16, 1024)
			: 256;
		string outputPath = Path.GetFullPath(args.Length > 1 ? args[1] : $"{SanitizeFileName(input)}.png");

		ComRuntime.Initialize();

		try
		{
			string fontsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
			string? itemLocation;
			nint hbitmap;

			if (TryResolveFontFile(input, fontsFolder, out string? fontFilePath))
			{
				Console.WriteLine("Path: CLSID_FontThumbnail -> IInitializeWithFile -> IExtractImage");
				Console.WriteLine($"Input file: {fontFilePath}");
				hbitmap = FontThumbnailInterop.ExtractFromFontFile(fontFilePath!, requestedSize, out itemLocation);
			}
			else
			{
				Console.WriteLine("Path: C:\\Windows\\Fonts IShellFolder -> GetUIObjectOf(IID_IExtractImage)");
				Console.WriteLine($"Font shell item: {input}");
				hbitmap = FontThumbnailInterop.ExtractFromFontShellItem(fontsFolder, input, requestedSize, out itemLocation);
			}

			try
			{
				using Bitmap bitmap = Bitmap.FromHbitmap(hbitmap);
				bitmap.Save(outputPath, ImageFormat.Png);
			}
			finally
			{
				NativeMethods.DeleteObject(hbitmap);
			}

			Console.WriteLine($"Requested size: {requestedSize}x{requestedSize}");
			if (!string.IsNullOrEmpty(itemLocation))
			{
				Console.WriteLine($"Provider location: {itemLocation}");
			}

			Console.WriteLine($"Saved: {outputPath}");
			return 0;
		}
		finally
		{
			ComRuntime.Uninitialize();
		}
	}

	private static bool TryResolveFontFile(string input, string fontsFolder, out string? fontFilePath)
	{
		if (Path.IsPathFullyQualified(input) || input.Contains('\\') || input.Contains('/'))
		{
			fontFilePath = input;
			return File.Exists(fontFilePath);
		}

		string candidate = Path.Combine(fontsFolder, input);
		if (File.Exists(candidate))
		{
			fontFilePath = candidate;
			return true;
		}

		fontFilePath = null;
		return false;
	}

	private static string SanitizeFileName(string value)
	{
		char[] invalidChars = Path.GetInvalidFileNameChars();
		var chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
		string fileName = new(chars);

		return string.IsNullOrWhiteSpace(fileName) ? "font-thumbnail" : fileName;
	}
}

internal static class FontThumbnailInterop
{
	private static readonly Guid ClsidFontThumbnail = new("B8BE1E19-B9E4-4EBB-B7F6-A8FE1B3871E0");

	public static nint ExtractFromFontFile(string fontFilePath, int size, out string? itemLocation)
	{
		Guid iid = typeof(IInitializeWithFile).GUID;
		Guid clsid = ClsidFontThumbnail;

		HResult.ThrowIfFailed(NativeMethods.CoCreateInstance(
			ref clsid,
			null,
			CLSCTX.InprocServer,
			ref iid,
			out IUnknownObject obj));

		var initializer = (IInitializeWithFile)obj;
		HResult.ThrowIfFailed(initializer.Initialize(fontFilePath, STGM.Read));

		return Extract((IExtractImage)initializer, size, out itemLocation);
	}

	public static nint ExtractFromFontShellItem(string fontsFolder, string fontShellItemName, int size, out string? itemLocation)
	{
		IShellItem fontsShellItem = CreateShellItem(fontsFolder);
		IShellFolder fontsShellFolder = BindToHandler<IShellFolder>(fontsShellItem, ShellGuids.BHID_SFObject);

		uint eaten = 0;
		uint attributes = 0;
		HResult.ThrowIfFailed(fontsShellFolder.ParseDisplayName(
			0,
			null,
			fontShellItemName,
			out eaten,
			out nint childPidlValue,
			ref attributes));

		using SafePidlHandle childPidl = SafePidlHandle.Attach(childPidlValue);
		nint apidl = Marshal.AllocCoTaskMem(nint.Size);

		try
		{
			Marshal.WriteIntPtr(apidl, childPidl.DangerousGetHandle());

			Guid iid = typeof(IExtractImage).GUID;
			HResult.ThrowIfFailed(fontsShellFolder.GetUIObjectOf(
				0,
				1,
				apidl,
				in iid,
				0,
				out IUnknownObject obj));

			return Extract((IExtractImage)obj, size, out itemLocation);
		}
		finally
		{
			Marshal.FreeCoTaskMem(apidl);
		}
	}

	private static nint Extract(IExtractImage extractImage, int size, out string? itemLocation)
	{
		itemLocation = TryGetLocation(extractImage, size);
		HResult.ThrowIfFailed(extractImage.Extract(out nint hbitmap));

		return hbitmap;
	}

	private static string? TryGetLocation(IExtractImage extractImage, int size)
	{
		const int bufferLength = 1024;
		nint buffer = Marshal.AllocCoTaskMem(bufferLength * sizeof(char));

		try
		{
			var requestedSize = new SIZE(size, size);
			uint priority = 0;
			IEIFLAG flags = IEIFLAG.Quality;
			int hr = extractImage.GetLocation(buffer, bufferLength, out priority, in requestedSize, 32, ref flags);

			return hr >= 0 ? Marshal.PtrToStringUni(buffer) : null;
		}
		finally
		{
			Marshal.FreeCoTaskMem(buffer);
		}
	}

	private static IShellItem CreateShellItem(string parsingName)
	{
		Guid iid = typeof(IShellItem).GUID;
		HResult.ThrowIfFailed(NativeMethods.SHCreateItemFromParsingName(
			parsingName,
			null,
			ref iid,
			out IShellItem shellItem));

		return shellItem;
	}

	private static T BindToHandler<T>(IShellItem shellItem, Guid handlerId)
		where T : IUnknownObject
	{
		Guid iid = typeof(T).GUID;
		HResult.ThrowIfFailed(shellItem.BindToHandler(null, in handlerId, in iid, out IUnknownObject obj));

		return (T)obj;
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

	[LibraryImport("ole32.dll")]
	internal static partial int CoCreateInstance(
		ref Guid clsid,
		IUnknownObject? outer,
		CLSCTX context,
		ref Guid iid,
		out IUnknownObject obj);

	[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	internal static partial int SHCreateItemFromParsingName(
		string path,
		IUnknownObject? bindContext,
		ref Guid riid,
		out IShellItem obj);

	[LibraryImport("shell32.dll")]
	internal static partial void ILFree(nint pidl);

	[LibraryImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool DeleteObject(nint obj);
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

internal sealed class SafePidlHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	private SafePidlHandle()
		: base(ownsHandle: true)
	{
	}

	public static SafePidlHandle Attach(nint handle)
	{
		var safeHandle = new SafePidlHandle();
		safeHandle.SetHandle(handle);
		return safeHandle;
	}

	protected override bool ReleaseHandle()
	{
		NativeMethods.ILFree(handle);
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
	int CreateViewObject(nint hwndOwner, in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int GetAttributesOf(uint cidl, nint apidl, ref uint attributes);

	[PreserveSig]
	int GetUIObjectOf(nint hwndOwner, uint cidl, nint apidl, in Guid riid, nint reserved, out IUnknownObject obj);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("B7D14566-0509-4CCE-A71F-0A554233BD9B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IInitializeWithFile : IUnknownObject
{
	[PreserveSig]
	int Initialize(string filePath, STGM mode);
}

[GeneratedComInterface]
[Guid("BB2E617C-0920-11D1-9A0B-00C04FC2D6C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IExtractImage : IUnknownObject
{
	[PreserveSig]
	int GetLocation(nint pathBuffer, int bufferLength, out uint priority, in SIZE size, uint colorDepth, ref IEIFLAG flags);

	[PreserveSig]
	int Extract(out nint bitmap);
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
internal enum CLSCTX : uint
{
	InprocServer = 0x1,
}

[Flags]
internal enum STGM : uint
{
	Read = 0,
}

[Flags]
internal enum IEIFLAG : uint
{
	Async = 0x1,
	Cache = 0x2,
	Aspect = 0x4,
	Offline = 0x8,
	Gleam = 0x10,
	Screen = 0x20,
	OrigSize = 0x40,
	NoStamp = 0x80,
	NoBorder = 0x100,
	Quality = 0x200,
}

internal enum SIGDN : int
{
	ParentRelativeForUI = unchecked((int)0x80094001),
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct SIZE(int cx, int cy)
{
	public readonly int cx = cx;
	public readonly int cy = cy;
}
