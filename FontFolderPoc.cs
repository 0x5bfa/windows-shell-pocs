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

return FontFolderPoc.Run(args);

internal static class FontFolderPoc
{
	public static int Run(string[] args)
	{
		if (args is ["--help"] or ["-h"] or ["/?"])
		{
			Console.WriteLine("""
Usage:
  dotnet run --file FontFolderPoc.cs -- list [--limit count]
  dotnet run --file FontFolderPoc.cs -- family [font family/name] [output.png] [size]
  dotnet run --file FontFolderPoc.cs -- extract [font family/name or font file path] [output.png] [size]
  dotnet run --file FontFolderPoc.cs -- [font family/name or font file path] [output.png] [size]

Examples:
  dotnet run --file FontFolderPoc.cs -- list --limit 30
  dotnet run --file FontFolderPoc.cs -- family Arial
  dotnet run --file FontFolderPoc.cs -- extract Arial .\Arial.png 256
  dotnet run --file FontFolderPoc.cs -- extract C:\Windows\Fonts\arial.ttf .\arial-file.png 256
""");
			return 0;
		}

		if (args is ["list", .. var listArgs])
		{
			return RunList(listArgs);
		}

		if (args is ["family", .. var familyArgs])
		{
			return RunFamily(familyArgs);
		}

		if (args is ["extract", .. var extractArgs])
		{
			return RunExtract(extractArgs);
		}

		return RunExtract(args);
	}

	private static int RunList(string[] args)
	{
		int limit = ParseLimit(args);

		ComRuntime.Initialize();

		try
		{
			string fontsFolder = GetFontsFolder();
			IReadOnlyList<FontShellItem> items = FontFolderInterop.EnumerateFontShellItems(fontsFolder, limit);

			foreach (FontShellItem item in items)
			{
				Console.WriteLine($"{item.Index,3}. {item.DisplayName}");
			}

			Console.WriteLine();
			Console.WriteLine($"Shown: {items.Count}");
			return 0;
		}
		finally
		{
			ComRuntime.Uninitialize();
		}
	}

	private static int RunFamily(string[] args)
	{
		string input = args.Length > 0 ? args[0] : "Arial";
		int requestedSize = ParseSize(args, index: 2);
		string outputPath = Path.GetFullPath(args.Length > 1 ? args[1] : $"{SanitizeFileName(input)}.png");

		ComRuntime.Initialize();

		try
		{
			nint hbitmap = FontFolderInterop.ExtractFromFontFamily(
				GetFontsFolder(),
				input,
				requestedSize,
				out FontShellItem item,
				out string? itemLocation);

			SaveBitmap(hbitmap, outputPath);

			Console.WriteLine("Path: C:\\Windows\\Fonts EnumObjects -> GetUIObjectOf(IID_IExtractImage)");
			Console.WriteLine($"Matched font shell item: {item.DisplayName}");
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

	private static int RunExtract(string[] args)
	{
		string input = args.Length > 0 ? args[0] : "Arial";
		int requestedSize = ParseSize(args, index: 2);
		string outputPath = Path.GetFullPath(args.Length > 1 ? args[1] : $"{SanitizeFileName(input)}.png");

		ComRuntime.Initialize();

		try
		{
			string fontsFolder = GetFontsFolder();
			string? itemLocation;
			nint hbitmap;

			if (TryResolveFontFile(input, fontsFolder, out string? fontFilePath))
			{
				Console.WriteLine("Path: CLSID_FontThumbnail -> IInitializeWithFile -> IExtractImage");
				Console.WriteLine($"Input file: {fontFilePath}");
				hbitmap = FontFolderInterop.ExtractFromFontFile(fontFilePath!, requestedSize, out itemLocation);
			}
			else
			{
				Console.WriteLine("Path: C:\\Windows\\Fonts IShellFolder -> GetUIObjectOf(IID_IExtractImage)");
				Console.WriteLine($"Font shell item: {input}");
				hbitmap = FontFolderInterop.ExtractFromFontShellItem(fontsFolder, input, requestedSize, out itemLocation);
			}

			SaveBitmap(hbitmap, outputPath);

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

	private static int ParseLimit(string[] args)
	{
		int limit = 50;

		for (int index = 0; index < args.Length; index++)
		{
			if (args[index] is "--limit" or "-l")
			{
				if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out limit))
				{
					throw new ArgumentException("--limit requires a numeric value.");
				}

				index++;
				continue;
			}

			throw new ArgumentException($"Unknown list argument: {args[index]}");
		}

		return Math.Clamp(limit, 1, 10000);
	}

	private static int ParseSize(string[] args, int index)
	{
		return args.Length > index && int.TryParse(args[index], out int parsedSize)
			? Math.Clamp(parsedSize, 16, 1024)
			: 256;
	}

	private static void SaveBitmap(nint hbitmap, string outputPath)
	{
		try
		{
			using Bitmap bitmap = CreateBitmapPreservingAlpha(hbitmap);
			bitmap.Save(outputPath, ImageFormat.Png);
		}
		finally
		{
			NativeMethods.DeleteObject(hbitmap);
		}
	}

	private static Bitmap CreateBitmapPreservingAlpha(nint hbitmap)
	{
		if (NativeMethods.GetObject(hbitmap, Marshal.SizeOf<BITMAP>(), out BITMAP sourceBitmap) == 0
			|| sourceBitmap.bmWidth <= 0
			|| sourceBitmap.bmHeight == 0)
		{
			return Bitmap.FromHbitmap(hbitmap);
		}

		int width = sourceBitmap.bmWidth;
		int height = Math.Abs(sourceBitmap.bmHeight);
		int stride = checked(width * 4);

		if (sourceBitmap.bmBits != 0
			&& sourceBitmap.bmBitsPixel == 32
			&& TryCreateBitmapFromScan0(sourceBitmap, width, height, out Bitmap bitmap))
		{
			return bitmap;
		}

		byte[] pixels = new byte[checked(stride * height)];
		var bitmapInfo = new BITMAPINFO
		{
			bmiHeader = new BITMAPINFOHEADER
			{
				biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
				biWidth = width,
				biHeight = -height,
				biPlanes = 1,
				biBitCount = 32,
				biCompression = BICompression.Rgb,
				biSizeImage = (uint)pixels.Length,
			},
		};

		nint screenDc = NativeMethods.GetDC(0);
		if (screenDc == 0)
		{
			return Bitmap.FromHbitmap(hbitmap);
		}

		try
		{
			int scanLines = NativeMethods.GetDIBits(screenDc, hbitmap, 0, (uint)height, pixels, ref bitmapInfo, DIBColorMode.RgbColors);
			if (scanLines != height)
			{
				return Bitmap.FromHbitmap(hbitmap);
			}
		}
		finally
		{
			NativeMethods.ReleaseDC(0, screenDc);
		}

		if (!HasAlphaChannel(pixels))
		{
			MakeOpaque(pixels);
		}

		var outputBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
		BitmapData data = outputBitmap.LockBits(
			new Rectangle(0, 0, width, height),
			ImageLockMode.WriteOnly,
			PixelFormat.Format32bppArgb);

		try
		{
			for (int row = 0; row < height; row++)
			{
				Marshal.Copy(pixels, row * stride, nint.Add(data.Scan0, row * data.Stride), stride);
			}
		}
		finally
		{
			outputBitmap.UnlockBits(data);
		}

		return outputBitmap;
	}

	private static bool TryCreateBitmapFromScan0(BITMAP sourceBitmap, int width, int height, out Bitmap bitmap)
	{
		int sourceStride = Math.Abs(sourceBitmap.bmWidthBytes);
		if (sourceStride <= 0)
		{
			bitmap = null!;
			return false;
		}

		nint scan0 = sourceBitmap.bmBits;
		if (sourceBitmap.bmHeight > 0)
		{
			scan0 = nint.Add(scan0, checked(sourceStride * (height - 1)));
			sourceStride = -sourceStride;
		}

		if (!HasAlphaChannel(scan0, sourceStride, width, height))
		{
			bitmap = null!;
			return false;
		}

		try
		{
			bitmap = new Bitmap(width, height, sourceStride, PixelFormat.Format32bppArgb, scan0);
			return true;
		}
		catch (ArgumentException)
		{
			bitmap = null!;
			return false;
		}
	}

	private static bool HasAlphaChannel(byte[] bgraPixels)
	{
		for (int index = 3; index < bgraPixels.Length; index += 4)
		{
			if (bgraPixels[index] != 0)
			{
				return true;
			}
		}

		return false;
	}

	private static bool HasAlphaChannel(nint scan0, int stride, int width, int height)
	{
		for (int row = 0; row < height; row++)
		{
			nint rowStart = nint.Add(scan0, checked(row * stride));
			for (int column = 0; column < width; column++)
			{
				if (Marshal.ReadByte(rowStart, checked((column * 4) + 3)) != 0)
				{
					return true;
				}
			}
		}

		return false;
	}

	private static void MakeOpaque(byte[] bgraPixels)
	{
		for (int index = 3; index < bgraPixels.Length; index += 4)
		{
			bgraPixels[index] = 255;
		}
	}

	private static string GetFontsFolder()
		=> Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

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

internal readonly record struct FontShellItem(int Index, string DisplayName, string? ParsingName);

internal static class FontFolderInterop
{
	private static readonly Guid ClsidFontThumbnail = new("B8BE1E19-B9E4-4EBB-B7F6-A8FE1B3871E0");

	public static IReadOnlyList<FontShellItem> EnumerateFontShellItems(string fontsFolder, int limit)
	{
		IShellFolder fontsShellFolder = GetFontsShellFolder(fontsFolder);
		HResult.ThrowIfFailed(fontsShellFolder.EnumObjects(
			0,
			SHCONTF.Folders | SHCONTF.NonFolders | SHCONTF.IncludeHidden,
			out IEnumIDList enumIdList));

		var items = new List<FontShellItem>();
		while (items.Count < limit && enumIdList.Next(1, out nint childPidlValue, out uint fetched) == HResult.S_OK && fetched == 1)
		{
			using SafePidlHandle childPidl = SafePidlHandle.Attach(childPidlValue);
			items.Add(CreateFontShellItem(fontsShellFolder, childPidl.DangerousGetHandle(), items.Count + 1));
		}

		return items;
	}

	public static nint ExtractFromFontFamily(string fontsFolder, string familyName, int size, out FontShellItem item, out string? itemLocation)
	{
		if (TryExtractFromFontFamily(fontsFolder, familyName, exactMatch: true, size, out item, out itemLocation, out nint exactBitmap))
		{
			return exactBitmap;
		}

		if (TryExtractFromFontFamily(fontsFolder, familyName, exactMatch: false, size, out item, out itemLocation, out nint partialBitmap))
		{
			return partialBitmap;
		}

		throw new InvalidOperationException($"Font family shell item was not found: {familyName}");
	}

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
		IShellFolder fontsShellFolder = GetFontsShellFolder(fontsFolder);

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
		return ExtractFromChildPidl(fontsShellFolder, childPidl.DangerousGetHandle(), size, out itemLocation);
	}

	private static bool TryExtractFromFontFamily(
		string fontsFolder,
		string familyName,
		bool exactMatch,
		int size,
		out FontShellItem item,
		out string? itemLocation,
		out nint hbitmap)
	{
		IShellFolder fontsShellFolder = GetFontsShellFolder(fontsFolder);
		HResult.ThrowIfFailed(fontsShellFolder.EnumObjects(
			0,
			SHCONTF.Folders | SHCONTF.NonFolders | SHCONTF.IncludeHidden,
			out IEnumIDList enumIdList));

		int index = 0;
		while (enumIdList.Next(1, out nint childPidlValue, out uint fetched) == HResult.S_OK && fetched == 1)
		{
			index++;
			using SafePidlHandle childPidl = SafePidlHandle.Attach(childPidlValue);
			FontShellItem currentItem = CreateFontShellItem(fontsShellFolder, childPidl.DangerousGetHandle(), index);
			if (!IsFamilyMatch(currentItem, familyName, exactMatch))
			{
				continue;
			}

			item = currentItem;
			hbitmap = ExtractFromChildPidl(fontsShellFolder, childPidl.DangerousGetHandle(), size, out itemLocation);
			return true;
		}

		item = default;
		itemLocation = null;
		hbitmap = 0;
		return false;
	}

	private static bool IsFamilyMatch(FontShellItem item, string familyName, bool exactMatch)
	{
		if (exactMatch)
		{
			return string.Equals(item.DisplayName, familyName, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(item.ParsingName, familyName, StringComparison.OrdinalIgnoreCase);
		}

		return item.DisplayName.Contains(familyName, StringComparison.OrdinalIgnoreCase)
			|| (item.ParsingName?.Contains(familyName, StringComparison.OrdinalIgnoreCase) ?? false);
	}

	private static nint ExtractFromChildPidl(IShellFolder fontsShellFolder, nint childPidl, int size, out string? itemLocation)
	{
		nint apidl = Marshal.AllocCoTaskMem(nint.Size);

		try
		{
			Marshal.WriteIntPtr(apidl, childPidl);

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

	private static FontShellItem CreateFontShellItem(IShellFolder fontsShellFolder, nint childPidl, int index)
	{
		string displayName = TryGetDisplayNameOf(fontsShellFolder, childPidl, SHGDNF.Normal) ?? "(unnamed)";
		string? parsingName = TryGetDisplayNameOf(fontsShellFolder, childPidl, SHGDNF.ForParsing);

		return new FontShellItem(index, displayName, parsingName);
	}

	private static string? TryGetDisplayNameOf(IShellFolder fontsShellFolder, nint childPidl, SHGDNF flags)
	{
		int hr = fontsShellFolder.GetDisplayNameOf(childPidl, flags, out STRRET strret);
		if (hr < 0)
		{
			return null;
		}

		const int bufferLength = 1024;
		nint buffer = Marshal.AllocCoTaskMem(bufferLength * sizeof(char));

		try
		{
			hr = NativeMethods.StrRetToBuf(ref strret, childPidl, buffer, bufferLength);
			return hr >= 0 ? Marshal.PtrToStringUni(buffer) : null;
		}
		finally
		{
			Marshal.FreeCoTaskMem(buffer);
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

	private static IShellFolder GetFontsShellFolder(string fontsFolder)
	{
		IShellItem fontsShellItem = CreateShellItem(fontsFolder);
		return BindToHandler<IShellFolder>(fontsShellItem, ShellGuids.BHID_SFObject);
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

	[LibraryImport("shlwapi.dll", EntryPoint = "StrRetToBufW")]
	internal static partial int StrRetToBuf(ref STRRET strret, nint pidl, nint buffer, int bufferLength);

	[LibraryImport("gdi32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool DeleteObject(nint obj);

	[LibraryImport("gdi32.dll", EntryPoint = "GetObjectW")]
	internal static partial int GetObject(nint obj, int count, out BITMAP bitmap);

	[LibraryImport("gdi32.dll")]
	internal static partial int GetDIBits(
		nint dc,
		nint bitmap,
		uint startScan,
		uint scanLines,
		[Out] byte[] bits,
		ref BITMAPINFO bitmapInfo,
		DIBColorMode usage);

	[LibraryImport("user32.dll")]
	internal static partial nint GetDC(nint hwnd);

	[LibraryImport("user32.dll")]
	internal static partial int ReleaseDC(nint hwnd, nint dc);
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
	int EnumObjects(nint hwnd, SHCONTF flags, out IEnumIDList enumIdList);

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

	[PreserveSig]
	int GetDisplayNameOf(nint pidl, SHGDNF flags, out STRRET name);

	[PreserveSig]
	int SetNameOf(nint hwnd, nint pidl, string name, SHGDNF flags, out nint newPidl);
}

[GeneratedComInterface]
[Guid("000214F2-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IEnumIDList : IUnknownObject
{
	[PreserveSig]
	int Next(uint count, out nint pidl, out uint fetched);

	[PreserveSig]
	int Skip(uint count);

	[PreserveSig]
	int Reset();

	[PreserveSig]
	int Clone(out IEnumIDList enumIdList);
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
internal enum SHCONTF : uint
{
	Folders = 0x20,
	NonFolders = 0x40,
	IncludeHidden = 0x80,
}

[Flags]
internal enum SHGDNF : uint
{
	Normal = 0,
	ForParsing = 0x8000,
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

internal enum BICompression : uint
{
	Rgb = 0,
}

internal enum DIBColorMode : uint
{
	RgbColors = 0,
}

internal enum STRRET_TYPE : uint
{
	WStr = 0,
	Offset = 1,
	CStr = 2,
}

[StructLayout(LayoutKind.Explicit, Size = 272)]
internal struct STRRET
{
	[FieldOffset(0)]
	public STRRET_TYPE uType;

	[FieldOffset(8)]
	public nint pOleStr;

	[FieldOffset(8)]
	public uint uOffset;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct SIZE(int cx, int cy)
{
	public readonly int cx = cx;
	public readonly int cy = cy;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BITMAP
{
	public int bmType;
	public int bmWidth;
	public int bmHeight;
	public int bmWidthBytes;
	public ushort bmPlanes;
	public ushort bmBitsPixel;
	public nint bmBits;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BITMAPINFO
{
	public BITMAPINFOHEADER bmiHeader;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BITMAPINFOHEADER
{
	public uint biSize;
	public int biWidth;
	public int biHeight;
	public ushort biPlanes;
	public ushort biBitCount;
	public BICompression biCompression;
	public uint biSizeImage;
	public int biXPelsPerMeter;
	public int biYPelsPerMeter;
	public uint biClrUsed;
	public uint biClrImportant;
}
