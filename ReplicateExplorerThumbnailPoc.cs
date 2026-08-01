#:sdk Microsoft.NET.Sdk
#:package Microsoft.Windows.CsWin32@0.3.298
#:property TargetFramework=net10.0-windows
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property AllowUnsafeBlocks=true
#:property DisableRuntimeMarshalling=true
#:property PublishAot=true
#:property InvariantGlobalization=true
#:property CsWin32RunAsBuildTask=true
#:package System.Drawing.Common@10.0.0
#:package System.CommandLine@2.0.10

using System.Collections.Concurrent;
using System.CommandLine;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.WindowsAndMessaging;
using Win32HResult = Windows.Win32.Foundation.HRESULT;

return await ExplorerThumbnailPoc.RunAsync(args);

internal static class ExplorerThumbnailPoc
{
	public static async Task<int> RunAsync(string[] args)
	{
		RootCommand rootCommand = CreateCommand();
		return await rootCommand.Parse(args).InvokeAsync();
	}

	private static RootCommand CreateCommand()
	{
		var pathArgument = new Argument<string>("path")
		{
			Description = "The file or folder to retrieve.",
		};
		var outputOption = new Option<FileInfo?>("--output", "-o")
		{
			Description = "Save the retrieved image as a PNG.",
		};
		Option<int> sizeOption = CreateSizeOption();
		Option<int> dpiOption = CreateDpiOption();
		Option<bool> cacheOnlyOption = CreateCacheOnlyOption();
		Option<bool> iconOnlyOption = CreateIconOnlyOption();
		Option<bool> thumbnailOnlyOption = CreateThumbnailOnlyOption();

		var rootCommand = new RootCommand("Retrieves Explorer-compatible Shell thumbnails and icons.")
		{
			pathArgument,
			outputOption,
			sizeOption,
			dpiOption,
			cacheOnlyOption,
			iconOnlyOption,
			thumbnailOnlyOption,
		};

		rootCommand.SetAction(async parseResult =>
		{
			string path = Path.GetFullPath(parseResult.GetValue(pathArgument)!);
			ThumbnailOptions options = CreateOptions(
				[path],
				parseResult.GetValue(outputOption)?.FullName,
				parseResult.GetValue(sizeOption),
				parseResult.GetValue(dpiOption),
				parseResult.GetValue(cacheOnlyOption),
				parseResult.GetValue(iconOnlyOption),
				parseResult.GetValue(thumbnailOnlyOption),
				prefetch: false);

			return await ExecuteSingleAsync(options);
		});

		var pathsArgument = new Argument<string[]>("paths")
		{
			Description = "The files or folders to prefetch.",
			Arity = ArgumentArity.OneOrMore,
		};
		Option<int> prefetchSizeOption = CreateSizeOption();
		Option<int> prefetchDpiOption = CreateDpiOption();
		Option<bool> prefetchCacheOnlyOption = CreateCacheOnlyOption();
		Option<bool> prefetchIconOnlyOption = CreateIconOnlyOption();
		Option<bool> prefetchThumbnailOnlyOption = CreateThumbnailOnlyOption();
		var prefetchCommand = new Command("prefetch", "Prefetch thumbnails for multiple Shell items.")
		{
			pathsArgument,
			prefetchSizeOption,
			prefetchDpiOption,
			prefetchCacheOnlyOption,
			prefetchIconOnlyOption,
			prefetchThumbnailOnlyOption,
		};

		prefetchCommand.SetAction(async parseResult =>
		{
			string[] paths = parseResult.GetValue(pathsArgument)!.Select(path => Path.GetFullPath(path)).ToArray();
			ThumbnailOptions options = CreateOptions(
				paths,
				outputPath: null,
				parseResult.GetValue(prefetchSizeOption),
				parseResult.GetValue(prefetchDpiOption),
				parseResult.GetValue(prefetchCacheOnlyOption),
				parseResult.GetValue(prefetchIconOnlyOption),
				parseResult.GetValue(prefetchThumbnailOnlyOption),
				prefetch: true);

			return await ExecutePrefetchAsync(options);
		});

		rootCommand.Subcommands.Add(prefetchCommand);
		return rootCommand;
	}

	private static Option<int> CreateSizeOption()
		=> new("--size", "-s")
		{
			Description = "Logical image size in pixels. Default: 256.",
			DefaultValueFactory = _ => 256,
			Validators =
			{
				result =>
				{
					if (result.GetValueOrDefault<int>() is < 16 or > 4096)
					{
						result.AddError("Size must be between 16 and 4096 pixels.");
					}
				},
			},
		};

	private static Option<int> CreateDpiOption()
		=> new("--dpi")
		{
			Description = "DPI used to convert logical size to physical pixels. Default: 96.",
			DefaultValueFactory = _ => 96,
			Validators =
			{
				result =>
				{
					if (result.GetValueOrDefault<int>() is < 48 or > 768)
					{
						result.AddError("DPI must be between 48 and 768.");
					}
				},
			},
		};

	private static Option<bool> CreateCacheOnlyOption()
		=> new("--cache-only")
		{
			Description = "Do not start extraction on a Shell cache miss.",
		};

	private static Option<bool> CreateIconOnlyOption()
		=> new("--icon-only")
		{
			Description = "Request only the Shell icon path.",
		};

	private static Option<bool> CreateThumbnailOnlyOption()
		=> new("--thumbnail-only")
		{
			Description = "Fail instead of falling back to an icon.",
		};

	private static ThumbnailOptions CreateOptions(
		IReadOnlyList<string> paths,
		string? outputPath,
		int logicalSize,
		int dpi,
		bool cacheOnly,
		bool iconOnly,
		bool thumbnailOnly,
		bool prefetch)
	{
		if (iconOnly && thumbnailOnly)
		{
			throw new ArgumentException("--icon-only and --thumbnail-only are mutually exclusive.");
		}

		return new ThumbnailOptions(
			paths,
			outputPath,
			logicalSize,
			dpi,
			cacheOnly,
			iconOnly,
			thumbnailOnly,
			prefetch);
	}

	private static async Task<int> ExecuteSingleAsync(ThumbnailOptions options)
	{
		using var pipeline = new ThumbnailPipeline();
		ThumbnailResult image = await pipeline.GetAsync(options.Paths[0], options);
		PrintResult(image);
		if (!image.Succeeded)
		{
			return 1;
		}

		string outputPath = options.OutputPath
			?? Path.GetFullPath($"{SanitizeFileName(options.Paths[0])}-{options.LogicalSize}.png");
		image.Save(outputPath);
		Console.WriteLine($"Saved: {outputPath}");
		return 0;
	}

	private static async Task<int> ExecutePrefetchAsync(ThumbnailOptions options)
	{
		using var pipeline = new ThumbnailPipeline();
		ThumbnailResult[] results = await pipeline.PrefetchAsync(options.Paths, options);
		foreach (ThumbnailResult result in results)
		{
			PrintResult(result);
		}

		return results.Any(result => !result.Succeeded) ? 1 : 0;
	}

	private static void PrintResult(ThumbnailResult result)
	{
		Console.WriteLine($"Path: {result.Path}");
		Console.WriteLine($"Requested logical size: {result.LogicalSize}x{result.LogicalSize}");
		Console.WriteLine($"Requested physical size: {result.PhysicalSize}x{result.PhysicalSize}");
		Console.WriteLine($"Cache status: {result.CacheStatus}");
		Console.WriteLine($"Image source: {result.Source ?? "none"}");
		Console.WriteLine($"Image index (system image list): {result.ImageIndex}");
		Console.WriteLine($"Overlay index: {result.OverlayIndex}");
		Console.WriteLine($"Result: {(result.Succeeded ? "success" : "failure")}");
		if (!string.IsNullOrWhiteSpace(result.Error))
		{
			Console.WriteLine($"Error: {result.Error}");
		}
	}

	private static string SanitizeFileName(string value)
	{
		string name = Path.GetFileNameWithoutExtension(value);
		if (string.IsNullOrWhiteSpace(name))
		{
			name = "shell-thumbnail";
		}

		char[] invalid = Path.GetInvalidFileNameChars();
		return new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
	}
}

internal sealed class ThumbnailPipeline : IDisposable
{
	private readonly ConcurrentDictionary<ThumbnailKey, ThumbnailPayload> memoryCache = new();
	private readonly ConcurrentDictionary<ThumbnailKey, Lazy<Task<ThumbnailResult>>> inFlight = new();

	public Task<ThumbnailResult> GetAsync(string path, ThumbnailOptions options)
	{
		ThumbnailKey key = ThumbnailKey.Create(path, options);
		if (memoryCache.TryGetValue(key, out ThumbnailPayload? cachedPayload))
		{
			return Task.FromResult(ThumbnailResult.FromPayload(
				path,
				options,
				cachedPayload,
				ThumbnailCacheStatus.ProcessCacheHit));
		}

		var lazy = new Lazy<Task<ThumbnailResult>>(
			() => LoadAsync(key, options),
			LazyThreadSafetyMode.ExecutionAndPublication);
		Lazy<Task<ThumbnailResult>> selected = inFlight.GetOrAdd(key, lazy);
		return AwaitAndCacheAsync(key, selected.Value);
	}

	public async Task<ThumbnailResult[]> PrefetchAsync(IReadOnlyList<string> paths, ThumbnailOptions options)
	{
		using var limiter = new SemaphoreSlim(4, 4);
		Task<ThumbnailResult>[] requests = paths.Select(async path =>
		{
			await limiter.WaitAsync();
			try
			{
				return await GetAsync(path, options);
			}
			finally
			{
				limiter.Release();
			}
		}).ToArray();

		return await Task.WhenAll(requests);
	}

	private async Task<ThumbnailResult> AwaitAndCacheAsync(ThumbnailKey key, Task<ThumbnailResult> request)
	{
		try
		{
			ThumbnailResult result = await request;
			if (result.Succeeded && result.Payload is not null)
			{
				memoryCache[key] = result.Payload;
			}

			return result;
		}
		finally
		{
			inFlight.TryRemove(key, out _);
		}
	}

	private static async Task<ThumbnailResult> LoadAsync(ThumbnailKey key, ThumbnailOptions options)
	{
		return await Task.Run(() =>
		{
			ComRuntime.Initialize();
			try
			{
				return ShellImageFactory.Load(key, options);
			}
			finally
			{
				ComRuntime.Uninitialize();
			}
		});
	}

	public void Dispose()
	{
		memoryCache.Clear();
		inFlight.Clear();
	}
}

internal static class ShellImageFactory
{
	// CsWin32 does not expose this Shell class identifier from the SDK metadata.
	private static readonly Guid CLSID_LocalThumbnailCache =
		new("50EF4544-AC9F-4A8E-B21B-8A26180DB13F");

	public static ThumbnailResult Load(ThumbnailKey key, ThumbnailOptions options)
	{
		if (!File.Exists(key.Path) && !Directory.Exists(key.Path))
		{
			return ThumbnailResult.Failure(key.Path, options, "The path does not exist.");
		}

		int physicalSize = key.PhysicalSize;
		ShellImageMetadata metadata = ShellImageMetadata.Read(key.Path);
		int lastHr = HResult.E_FAIL;

		if (options.IconOnly)
		{
			lastHr = TryShellItemImageFactory(
				key.Path,
				physicalSize,
				SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_BIGGERSIZEOK | SIIGBF.SIIGBF_INCACHEONLY,
				out ThumbnailPayload? cachedIconPayload);
			if (lastHr >= 0 && cachedIconPayload is not null)
			{
				return CompleteThumbnail(key, options, cachedIconPayload, metadata, ThumbnailCacheStatus.ShellCacheHit);
			}

			if (options.CacheOnly)
			{
				return ThumbnailResult.Failure(key.Path, options, $"Shell icon cache miss: HRESULT 0x{lastHr:X8}");
			}
		}

		if (!options.IconOnly)
		{
			lastHr = TryThumbnailCache(
				key.Path,
				physicalSize,
				WTS_FLAGS.WTS_INCACHEONLY | WTS_FLAGS.WTS_SCALETOREQUESTEDSIZE,
				out ThumbnailPayload? cachedPayload);
			if (lastHr >= 0 && cachedPayload is not null)
			{
				return CompleteThumbnail(key, options, cachedPayload, metadata, ThumbnailCacheStatus.ShellCacheHit);
			}

			lastHr = TryShellItemImageFactory(
				key.Path,
				physicalSize,
				SIIGBF.SIIGBF_THUMBNAILONLY | SIIGBF.SIIGBF_BIGGERSIZEOK | SIIGBF.SIIGBF_INCACHEONLY,
				out ThumbnailPayload? cachedFactoryPayload);
			if (lastHr >= 0 && cachedFactoryPayload is not null)
			{
				return CompleteThumbnail(key, options, cachedFactoryPayload, metadata, ThumbnailCacheStatus.ShellCacheHit);
			}

			if (options.CacheOnly)
			{
				return ThumbnailResult.Failure(key.Path, options, $"Shell cache miss: HRESULT 0x{lastHr:X8}");
			}

			lastHr = TryThumbnailCache(
				key.Path,
				physicalSize,
				WTS_FLAGS.WTS_EXTRACT | WTS_FLAGS.WTS_SCALETOREQUESTEDSIZE | WTS_FLAGS.WTS_SCALEUP,
				out ThumbnailPayload? extractedPayload);
			if (lastHr >= 0 && extractedPayload is not null)
			{
				return CompleteThumbnail(key, options, extractedPayload, metadata, ThumbnailCacheStatus.BackgroundExtractionOrFallback);
			}

			SIIGBF thumbnailFlags = SIIGBF.SIIGBF_THUMBNAILONLY | SIIGBF.SIIGBF_BIGGERSIZEOK;
			lastHr = TryShellItemImageFactory(key.Path, physicalSize, thumbnailFlags, out ThumbnailPayload? factoryPayload);
			if (lastHr >= 0 && factoryPayload is not null)
			{
				return CompleteThumbnail(key, options, factoryPayload, metadata, ThumbnailCacheStatus.BackgroundExtractionOrFallback);
			}

			if (options.ThumbnailOnly)
			{
				return ThumbnailResult.Failure(key.Path, options, $"Thumbnail extraction failed: HRESULT 0x{lastHr:X8}");
			}

			lastHr = TryExtractImage(key.Path, physicalSize, out ThumbnailPayload? legacyPayload);
			if (lastHr >= 0 && legacyPayload is not null)
			{
				return CompleteThumbnail(key, options, legacyPayload, metadata, ThumbnailCacheStatus.BackgroundExtractionOrFallback);
			}
		}

		lastHr = TryShellItemImageFactory(
			key.Path,
			physicalSize,
			SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_BIGGERSIZEOK,
			out ThumbnailPayload? iconPayload);
		if (lastHr >= 0 && iconPayload is not null)
		{
			return CompleteThumbnail(key, options, iconPayload, metadata, ThumbnailCacheStatus.BackgroundExtractionOrFallback);
		}

		lastHr = TryRenderSystemIcon(key.Path, physicalSize, metadata, out ThumbnailPayload? renderedIcon);
		if (lastHr >= 0 && renderedIcon is not null)
		{
			return CompleteThumbnail(
				key,
				options,
				renderedIcon,
				metadata,
				ThumbnailCacheStatus.BackgroundExtractionOrFallback);
		}

		return ThumbnailResult.Failure(key.Path, options, $"All Shell image providers failed: HRESULT 0x{lastHr:X8}");
	}

	private static ThumbnailResult CompleteThumbnail(
		ThumbnailKey key,
		ThumbnailOptions options,
		ThumbnailPayload payload,
		ShellImageMetadata metadata,
		ThumbnailCacheStatus cacheStatus)
	{
		int overlayIndex = metadata.OverlayIndex;
		payload = payload with { ImageIndex = metadata.ImageIndex, OverlayIndex = overlayIndex };
		if (overlayIndex == 0)
		{
			return ThumbnailResult.FromPayload(key.Path, options, payload, cacheStatus);
		}

		if (ShellImageFactory.TryRenderOverlay(payload.Png, overlayIndex, out byte[] compositedPng))
		{
			return ThumbnailResult.FromPayload(
				key.Path,
				options,
				payload with { Png = compositedPng },
				cacheStatus);
		}

		return ThumbnailResult.FromPayload(key.Path, options, payload, cacheStatus);
	}

	private static int TryThumbnailCache(
		string path,
		int physicalSize,
		WTS_FLAGS flags,
		out ThumbnailPayload? payload)
	{
		payload = null;
		int hr = ComInterop.CreateInstance(
			CLSID_LocalThumbnailCache,
			out IThumbnailCache thumbnailCache);
		if (hr < 0)
		{
			return hr;
		}

		hr = PInvoke.SHCreateItemFromParsingName(path, null, out IShellItem shellItem).Value;
		if (hr < 0)
		{
			return hr;
		}

		hr = UI_Shell_IThumbnailCache_Extensions.GetThumbnail(
			thumbnailCache,
			shellItem,
			(uint)physicalSize,
			flags,
			out ISharedBitmap sharedBitmap,
			out WTS_CACHEFLAGS cacheFlags,
			out WTS_THUMBNAILID thumbnailId);
		if (hr < 0 || sharedBitmap is null)
		{
			return hr < 0 ? hr : HResult.E_FAIL;
		}

		HBITMAP bitmapHandle = default;
		try
		{
			hr = UI_Shell_ISharedBitmap_Extensions.GetSharedBitmap(sharedBitmap, out bitmapHandle).Value;
			if (hr < 0 || bitmapHandle.IsNull)
			{
				return hr < 0 ? hr : HResult.E_FAIL;
			}

			hr = UI_Shell_ISharedBitmap_Extensions.GetFormat(sharedBitmap, out WTS_ALPHATYPE alphaType).Value;
			if (hr < 0)
			{
				return hr;
			}

			byte[]? png = BitmapEncoder.EncodeHBitmap(bitmapHandle, alphaType == WTS_ALPHATYPE.WTSAT_RGB);
			if (png is null)
			{
				return HResult.E_FAIL;
			}

			payload = new ThumbnailPayload(png, "IThumbnailCache", -1, 0);
			return HResult.S_OK;
		}
		finally
		{
			if (!bitmapHandle.IsNull)
			{
				PInvoke.DeleteObject((HGDIOBJ)bitmapHandle);
			}
		}
	}

	private static int TryShellItemImageFactory(
		string path,
		int physicalSize,
		SIIGBF flags,
		out ThumbnailPayload? payload)
	{
		payload = null;
		int hr = PInvoke.SHCreateItemFromParsingName(path, null, out IShellItemImageFactory imageFactory).Value;
		if (hr < 0)
		{
			return hr;
		}

		HBITMAP bitmapHandle = default;
		try
		{
			hr = UI_Shell_IShellItemImageFactory_Extensions.GetImage(
				imageFactory,
				new SIZE { cx = physicalSize, cy = physicalSize },
				flags,
				out bitmapHandle);
			if (hr < 0 || bitmapHandle.IsNull)
			{
				return hr < 0 ? hr : HResult.E_FAIL;
			}

			byte[]? png = BitmapEncoder.EncodeHBitmap(bitmapHandle, forceOpaqueWhenNoAlpha: false);
			if (png is null)
			{
				return HResult.E_FAIL;
			}

			payload = new ThumbnailPayload(png, "IShellItemImageFactory", -1, 0);
			return HResult.S_OK;
		}
		finally
		{
			if (!bitmapHandle.IsNull)
			{
				PInvoke.DeleteObject((HGDIOBJ)bitmapHandle);
			}
		}
	}

	private static unsafe int TryExtractImage(string path, int physicalSize, out ThumbnailPayload? payload)
	{
		payload = null;
		ITEMIDLIST* pidl = null;
		try
		{
			int hr = PInvoke.SHParseDisplayName(path, null, out pidl, 0, out _).Value;
			if (hr < 0 || pidl is null)
			{
				return hr < 0 ? hr : HResult.E_FAIL;
			}

			hr = PInvoke.SHBindToParent(
				*pidl,
				typeof(IShellFolder).GUID,
				out object folderObject,
				out ITEMIDLIST* childPidl).Value;
			if (hr < 0)
			{
				return hr;
			}

			IShellFolder folder = (IShellFolder)folderObject;
			ITEMIDLIST* childArray = childPidl;
			hr = UI_Shell_IShellFolder_Extensions.GetUIObjectOf(
				folder,
				default,
				1,
				&childArray,
				typeof(IExtractImage).GUID,
				out object extractImageObject).Value;
			if (hr < 0)
			{
				return hr;
			}

			IExtractImage extractImage = (IExtractImage)extractImageObject;
			Span<char> pathBuffer = stackalloc char[260];
			uint priority = 0;
			uint flags = 0x00000002 | 0x00000200; // IEIFLAG_CACHE | IEIFLAG_QUALITY.
			SIZE requestedSize = new() { cx = physicalSize, cy = physicalSize };
			hr = UI_Shell_IExtractImage_Extensions.GetLocation(
				extractImage,
				pathBuffer,
				ref priority,
				requestedSize,
				32,
				ref flags).Value;
			if (hr < 0)
			{
				return hr;
			}

			hr = UI_Shell_IExtractImage_Extensions.Extract(extractImage, out HBITMAP bitmapHandle).Value;
			if (hr < 0 || bitmapHandle.IsNull)
			{
				return hr < 0 ? hr : HResult.E_FAIL;
			}

			try
			{
				byte[]? png = BitmapEncoder.EncodeHBitmap(bitmapHandle, forceOpaqueWhenNoAlpha: false);
				if (png is null)
				{
					return HResult.E_FAIL;
				}

				payload = new ThumbnailPayload(png, "IExtractImage", -1, 0);
				return HResult.S_OK;
			}
			finally
			{
				PInvoke.DeleteObject((HGDIOBJ)bitmapHandle);
			}
		}
		finally
		{
			if (pidl is not null)
			{
				PInvoke.ILFree(pidl);
			}
		}
	}

	private static int TryRenderSystemIcon(
		string path,
		int physicalSize,
		ShellImageMetadata metadata,
		out ThumbnailPayload? payload)
	{
		payload = null;
		if (!TryExtractIcon(path, physicalSize, metadata, out HICON icon))
		{
			return HResult.E_FAIL;
		}

		try
		{
			byte[]? png = BitmapEncoder.EncodeHIcon(icon, physicalSize);
			if (png is null)
			{
				return HResult.E_FAIL;
			}

			payload = new ThumbnailPayload(png, "IExtractIconW", metadata.ImageIndex, metadata.OverlayIndex);
			return HResult.S_OK;
		}
		finally
		{
			PInvoke.DestroyIcon(icon);
		}
	}

	internal static bool TryRenderOverlay(byte[] png, int overlayIndex, out byte[] compositedPng)
	{
		compositedPng = png;
		if (overlayIndex <= 0
			|| !BitmapEncoder.TryDecodePng(png, out byte[] baseBgra, out int width, out int height)
			|| width != height)
		{
			return false;
		}

		int imageListId = width switch
		{
			<= 16 => (int)PInvoke.SHIL_SMALL,
			<= 32 => (int)PInvoke.SHIL_LARGE,
			<= 48 => (int)PInvoke.SHIL_EXTRALARGE,
			_ => (int)PInvoke.SHIL_JUMBO,
		};

		if (PInvoke.SHGetImageList(imageListId, out IImageList imageList).Value < 0
			|| imageList.GetOverlayImage(overlayIndex, out int overlayImageIndex).Value < 0
			|| UI_Controls_IImageList_Extensions.GetIcon(
				imageList,
				overlayImageIndex,
				0,
				out HICON overlayIcon).Value < 0
			|| overlayIcon.IsNull)
		{
			return false;
		}

		try
		{
			if (!BitmapEncoder.TryCompositeOverlay(baseBgra, width, height, overlayIcon, out byte[] result))
			{
				return false;
			}

			compositedPng = BitmapEncoder.EncodeBgra(result, width, height) ?? png;
			return !ReferenceEquals(compositedPng, png);
		}
		finally
		{
			PInvoke.DestroyIcon(overlayIcon);
		}
	}

	private static unsafe bool TryExtractIcon(
		string path,
		int physicalSize,
		ShellImageMetadata metadata,
		out HICON icon)
	{
		icon = default;
		string iconPath = metadata.IconPath;
		int resourceIndex = metadata.IconResourceIndex;
		if (string.IsNullOrWhiteSpace(iconPath)
			&& !TryGetIconLocation(path, out iconPath, out resourceIndex))
		{
			return false;
		}

		if (!TryGetExtractIcon(path, out IExtractIconW extractIcon))
		{
			return false;
		}

		uint iconSize = unchecked((uint)((physicalSize & 0xFFFF) | ((physicalSize & 0xFFFF) << 16)));
		HRESULT hr = UI_Shell_IExtractIconW_Extensions.Extract(
			extractIcon,
			iconPath,
			unchecked((uint)resourceIndex),
			out HICON largeIcon,
			out HICON smallIcon,
			iconSize);
		if (hr.Value < 0)
		{
			return false;
		}

		icon = !largeIcon.IsNull ? largeIcon : smallIcon;
		if (!smallIcon.IsNull && smallIcon != icon)
		{
			PInvoke.DestroyIcon(smallIcon);
		}
		return !icon.IsNull;
	}

	private static unsafe bool TryGetExtractIcon(string path, out IExtractIconW extractIcon)
	{
		extractIcon = null!;
		ITEMIDLIST* pidl = null;
		try
		{
			if (PInvoke.SHParseDisplayName(path, null, out pidl, 0, out _).Value < 0 || pidl is null)
			{
				return false;
			}

			if (PInvoke.SHBindToParent(
				*pidl,
				typeof(IShellFolder).GUID,
				out object folderObject,
				out ITEMIDLIST* childPidl).Value < 0)
			{
				return false;
			}

			ITEMIDLIST* childArray = childPidl;
			IShellFolder folder = (IShellFolder)folderObject;
			if (UI_Shell_IShellFolder_Extensions.GetUIObjectOf(
				folder,
				default,
				1,
				&childArray,
				typeof(IExtractIconW).GUID,
				out object iconObject).Value < 0)
			{
				return false;
			}

			extractIcon = (IExtractIconW)iconObject;
			return true;
		}
		finally
		{
			if (pidl is not null)
			{
				PInvoke.ILFree(pidl);
			}
		}
	}

	internal static unsafe bool TryGetIconLocation(
		string path,
		out string iconPath,
		out int imageIndex)
	{
		iconPath = string.Empty;
		imageIndex = -1;
		if (!TryGetExtractIcon(path, out IExtractIconW extractIcon))
		{
			return false;
		}

		Span<char> buffer = stackalloc char[260];
		if (UI_Shell_IExtractIconW_Extensions.GetIconLocation(
			extractIcon,
			0,
			buffer,
			out imageIndex,
			out _).Value < 0)
		{
			return false;
		}

		int terminator = buffer.IndexOf('\0');
		iconPath = new string(terminator >= 0 ? buffer[..terminator] : buffer);
		return !string.IsNullOrWhiteSpace(iconPath);
	}
}

internal sealed record ThumbnailOptions(
	IReadOnlyList<string> Paths,
	string? OutputPath,
	int LogicalSize,
	int Dpi,
	bool CacheOnly,
	bool IconOnly,
	bool ThumbnailOnly,
	bool Prefetch);

internal readonly record struct ThumbnailKey(
	string Path,
	int LogicalSize,
	int Dpi,
	bool IconOnly,
	bool ThumbnailOnly,
	long ContentStamp,
	long ContentLength)
{
	public int PhysicalSize => Math.Max(1, checked((LogicalSize * Dpi + 48) / 96));

	public static ThumbnailKey Create(string path, ThumbnailOptions options)
	{
		var info = new FileInfo(path);
		return new ThumbnailKey(
			path,
			options.LogicalSize,
			options.Dpi,
			options.IconOnly,
			options.ThumbnailOnly,
			info.Exists ? info.LastWriteTimeUtc.Ticks : 0,
			info.Exists ? info.Length : 0);
	}
}

internal sealed record ThumbnailPayload(
	byte[] Png,
	string Source,
	int ImageIndex,
	int OverlayIndex);

internal sealed class ThumbnailResult
{
	private ThumbnailResult(
		string path,
		int logicalSize,
		int physicalSize,
		ThumbnailCacheStatus cacheStatus,
		ThumbnailPayload? payload,
		string? error)
	{
		Path = path;
		LogicalSize = logicalSize;
		PhysicalSize = physicalSize;
		CacheStatus = cacheStatus;
		Payload = payload;
		Error = error;
	}

	public string Path { get; }
	public int LogicalSize { get; }
	public int PhysicalSize { get; }
	public ThumbnailCacheStatus CacheStatus { get; }
	public ThumbnailPayload? Payload { get; }
	public byte[]? Png => Payload?.Png;
	public string? Source => Payload?.Source;
	public int ImageIndex => Payload?.ImageIndex ?? -1;
	public int OverlayIndex => Payload?.OverlayIndex ?? 0;
	public string? Error { get; }
	public bool Succeeded => Png is not null;

	public static ThumbnailResult FromPayload(
		string path,
		ThumbnailOptions options,
		ThumbnailPayload payload,
		ThumbnailCacheStatus cacheStatus)
		=> new(
			path,
			options.LogicalSize,
			Math.Max(1, checked((options.LogicalSize * options.Dpi + 48) / 96)),
			cacheStatus,
			payload,
			null);

	public static ThumbnailResult Failure(string path, ThumbnailOptions options, string error)
		=> new(
			path,
			options.LogicalSize,
			Math.Max(1, checked((options.LogicalSize * options.Dpi + 48) / 96)),
			ThumbnailCacheStatus.Miss,
			null,
			error);

	public void Save(string outputPath)
	{
		if (Png is null)
		{
			throw new InvalidOperationException("The thumbnail request did not produce an image.");
		}

		string? directory = System.IO.Path.GetDirectoryName(outputPath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllBytes(outputPath, Png);
	}
}

internal enum ThumbnailCacheStatus
{
	Miss,
	ShellCacheHit,
	ProcessCacheHit,
	BackgroundExtractionOrFallback,
}

internal static unsafe class BitmapEncoder
{
	private const uint BI_RGB = 0;
	private const byte SentinelBlue = 0x37;
	private const byte SentinelGreen = 0xA1;
	private const byte SentinelRed = 0xE9;

	public static byte[]? EncodeHBitmap(HBITMAP bitmapHandle, bool forceOpaqueWhenNoAlpha)
	{
		if (bitmapHandle.IsNull)
		{
			return null;
		}

		BITMAP bitmap = default;
		HGDIOBJ objectHandle = new(bitmapHandle.Value);
		if (PInvoke.GetObject(objectHandle, sizeof(BITMAP), &bitmap) == 0)
		{
			return null;
		}

		int width = Math.Abs(bitmap.bmWidth);
		int height = Math.Abs(bitmap.bmHeight);
		if (width == 0 || height == 0)
		{
			return null;
		}

		byte[]? bgra = ReadBgra(bitmapHandle, width, height);
		if (bgra is null)
		{
			return null;
		}

		bool hasAlpha = false;
		bool hasRgb = false;
		for (int offset = 0; offset < bgra.Length; offset += 4)
		{
			hasAlpha |= bgra[offset + 3] != 0;
			hasRgb |= (bgra[offset] | bgra[offset + 1] | bgra[offset + 2]) != 0;
		}

		if (forceOpaqueWhenNoAlpha || (!hasAlpha && hasRgb))
		{
			for (int offset = 3; offset < bgra.Length; offset += 4)
			{
				bgra[offset] = 255;
			}
		}

		return EncodeBgra(bgra, width, height);
	}

	public static byte[]? EncodeHIcon(HICON icon, int size)
	{
		if (icon.IsNull || size <= 0)
		{
			return null;
		}

		byte[]? bgra = RenderHIcon(icon, size);
		return bgra is null ? null : EncodeBgra(bgra, size, size);
	}

	public static bool TryCompositeOverlay(
		byte[] baseBgra,
		int width,
		int height,
		HICON overlayIcon,
		out byte[] compositedBgra)
	{
		compositedBgra = baseBgra;
		if (width <= 0 || height <= 0 || baseBgra.Length != checked(width * height * 4))
		{
			return false;
		}

		byte[]? overlayBgra = RenderHIcon(overlayIcon, width);
		if (overlayBgra is null || overlayBgra.Length != baseBgra.Length)
		{
			return false;
		}

		compositedBgra = new byte[baseBgra.Length];
		for (int offset = 0; offset < baseBgra.Length; offset += 4)
		{
			int sourceAlpha = overlayBgra[offset + 3];
			if (sourceAlpha == 0)
			{
				baseBgra.AsSpan(offset, 4).CopyTo(compositedBgra.AsSpan(offset, 4));
				continue;
			}

			int destinationAlpha = baseBgra[offset + 3];
			int inverseSourceAlpha = 255 - sourceAlpha;
			int resultAlpha = sourceAlpha + (destinationAlpha * inverseSourceAlpha + 127) / 255;
			if (resultAlpha == 0)
			{
				continue;
			}

			compositedBgra[offset] = (byte)((overlayBgra[offset] * sourceAlpha
				+ baseBgra[offset] * destinationAlpha * inverseSourceAlpha / 255) / resultAlpha);
			compositedBgra[offset + 1] = (byte)((overlayBgra[offset + 1] * sourceAlpha
				+ baseBgra[offset + 1] * destinationAlpha * inverseSourceAlpha / 255) / resultAlpha);
			compositedBgra[offset + 2] = (byte)((overlayBgra[offset + 2] * sourceAlpha
				+ baseBgra[offset + 2] * destinationAlpha * inverseSourceAlpha / 255) / resultAlpha);
			compositedBgra[offset + 3] = (byte)resultAlpha;
		}

		return true;
	}

	public static byte[]? EncodeBgra(byte[] bgra, int width, int height)
	{
		if (width <= 0 || height <= 0 || bgra.Length != checked(width * height * 4))
		{
			return null;
		}

		try
		{
			using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			Rectangle rectangle = new(0, 0, width, height);
			BitmapData data = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			try
			{
				fixed (byte* source = bgra)
				{
					for (int y = 0; y < height; y++)
					{
						Buffer.MemoryCopy(
							source + y * width * 4,
							(void*)(data.Scan0 + y * data.Stride),
							width * 4,
							width * 4);
					}
				}
			}
			finally
			{
				bitmap.UnlockBits(data);
			}

			using var stream = new MemoryStream();
			bitmap.Save(stream, ImageFormat.Png);
			return stream.ToArray();
		}
		catch (ExternalException)
		{
			return null;
		}
	}

	public static bool TryDecodePng(byte[] png, out byte[] bgra, out int width, out int height)
	{
		bgra = Array.Empty<byte>();
		width = 0;
		height = 0;
		try
		{
			using var stream = new MemoryStream(png, writable: false);
			using var source = new Bitmap(stream);
			width = source.Width;
			height = source.Height;
			using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.Clear(Color.Transparent);
				graphics.DrawImageUnscaled(source, 0, 0);
			}

			Rectangle rectangle = new(0, 0, width, height);
			BitmapData data = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			try
			{
				bgra = new byte[checked(width * height * 4)];
				fixed (byte* destination = bgra)
				{
					for (int y = 0; y < height; y++)
					{
						Buffer.MemoryCopy(
							(void*)(data.Scan0 + y * data.Stride),
							destination + y * width * 4,
							width * 4,
							width * 4);
					}
				}
			}
			finally
			{
				bitmap.UnlockBits(data);
			}

			return true;
		}
		catch (ArgumentException)
		{
			return false;
		}
		catch (ExternalException)
		{
			return false;
		}
	}

	internal static byte[]? RenderHIcon(HICON icon, int size)
	{
		HDC screenDc = PInvoke.GetDC(default);
		HDC memoryDc = default;
		HBITMAP bitmapHandle = default;
		HGDIOBJ oldBitmap = default;
		try
		{
			memoryDc = PInvoke.CreateCompatibleDC(default);
			if (memoryDc.IsNull)
			{
				return null;
			}

			BITMAPINFO bitmapInfo = CreateBitmapInfo(size, size);
			void* bits;
			bitmapHandle = PInvoke.CreateDIBSection(
				screenDc,
				&bitmapInfo,
				DIB_USAGE.DIB_RGB_COLORS,
				out bits,
				default,
				0);

			if (bitmapHandle.IsNull || bits is null)
			{
				return null;
			}

			int byteCount = checked(size * size * 4);
			Span<byte> destination = new(bits, byteCount);
			for (int offset = 0; offset < destination.Length; offset += 4)
			{
				destination[offset] = SentinelBlue;
				destination[offset + 1] = SentinelGreen;
				destination[offset + 2] = SentinelRed;
				destination[offset + 3] = 0;
			}

			oldBitmap = PInvoke.SelectObject(memoryDc, new HGDIOBJ(bitmapHandle.Value));
			if (oldBitmap.IsNull
				|| PInvoke.DrawIconEx(
					memoryDc,
					0,
					0,
					icon,
					size,
					size,
					0,
					default,
					DI_FLAGS.DI_NORMAL).Value == 0)
			{
				return null;
			}

			byte[] bgra = new byte[byteCount];
			fixed (byte* destinationBytes = bgra)
			{
				Buffer.MemoryCopy(bits, destinationBytes, byteCount, byteCount);
			}
			SetRenderedAlpha(bgra);
			return bgra;
		}
		finally
		{
			if (!oldBitmap.IsNull)
			{
				PInvoke.SelectObject(memoryDc, oldBitmap);
			}
			if (!bitmapHandle.IsNull)
			{
				PInvoke.DeleteObject(new HGDIOBJ(bitmapHandle.Value));
			}
			if (!memoryDc.IsNull)
			{
				PInvoke.DeleteDC(memoryDc);
			}
			if (!screenDc.IsNull)
			{
				PInvoke.ReleaseDC(default, screenDc);
			}
		}
	}

	private static byte[]? ReadBgra(HBITMAP bitmapHandle, int width, int height)
	{
		HDC screenDc = PInvoke.GetDC(default);
		try
		{
			BITMAPINFO bitmapInfo = CreateBitmapInfo(width, height);
			byte[] result = new byte[checked(width * height * 4)];
			fixed (byte* destination = result)
			{
				int scanLines = PInvoke.GetDIBits(
					screenDc,
					bitmapHandle,
					0,
					(uint)height,
					destination,
					&bitmapInfo,
					DIB_USAGE.DIB_RGB_COLORS);
				return scanLines == height ? result : null;
			}
		}
		finally
		{
			if (!screenDc.IsNull)
			{
				PInvoke.ReleaseDC(default, screenDc);
			}
		}
	}

	private static BITMAPINFO CreateBitmapInfo(int width, int height)
		=> new()
		{
			bmiHeader = new BITMAPINFOHEADER
			{
				biSize = (uint)sizeof(BITMAPINFOHEADER),
				biWidth = width,
				biHeight = -height,
				biPlanes = 1,
				biBitCount = 32,
				biCompression = BI_RGB,
			},
		};

	private static void SetRenderedAlpha(byte[] bgra)
	{
		for (int offset = 0; offset < bgra.Length; offset += 4)
		{
			bool wasDrawn = bgra[offset] != SentinelBlue
				|| bgra[offset + 1] != SentinelGreen
				|| bgra[offset + 2] != SentinelRed;
			if (wasDrawn)
			{
				bgra[offset + 3] = 255;
			}
			else
			{
				bgra[offset] = 0;
				bgra[offset + 1] = 0;
				bgra[offset + 2] = 0;
				bgra[offset + 3] = 0;
			}
		}
	}
}

internal readonly record struct ShellImageMetadata(
	int ImageIndex,
	int OverlayIndex,
	string IconPath,
	int IconResourceIndex)
{
	public static ShellImageMetadata Read(string path)
	{
		string iconPath = string.Empty;
		int iconResourceIndex = -1;
		if (ShellImageFactory.TryGetIconLocation(path, out string resolvedIconPath, out iconResourceIndex))
		{
			iconPath = resolvedIconPath;
		}

		if (TryGetOverlayManagerInfo(path, out int imageIndex, out int overlayIndex))
		{
			return new ShellImageMetadata(imageIndex, overlayIndex, iconPath, iconResourceIndex);
		}

		if (TryGetShellIconIndex(path, out imageIndex))
		{
			return new ShellImageMetadata(imageIndex, 0, iconPath, iconResourceIndex);
		}

		return new ShellImageMetadata(-1, 0, iconPath, iconResourceIndex);
	}

	private static bool TryGetOverlayManagerInfo(string path, out int imageIndex, out int overlayIndex)
	{
		imageIndex = -1;
		overlayIndex = 0;
		if (ComInterop.CreateInstance(
			PInvoke.CLSID_CFSIconOverlayManager,
			out IShellIconOverlayManager manager) < 0)
		{
			return false;
		}

		uint attributes;
		try
		{
			attributes = (uint)File.GetAttributes(path);
		}
		catch (IOException)
		{
			attributes = 0;
		}
		catch (UnauthorizedAccessException)
		{
			attributes = 0;
		}

		if (UI_Shell_IShellIconOverlayManager_Extensions.GetFileOverlayInfo(
			manager,
			path,
			attributes,
			out imageIndex,
			PInvoke.SIOM_ICONINDEX).Value < 0)
		{
			return false;
		}

		if (UI_Shell_IShellIconOverlayManager_Extensions.GetFileOverlayInfo(
			manager,
			path,
			attributes,
			out int reportedOverlayIndex,
			PInvoke.SIOM_OVERLAYINDEX).Value >= 0
			&& reportedOverlayIndex > 0)
		{
			overlayIndex = reportedOverlayIndex;
		}

		return imageIndex >= 0;
	}

	private static unsafe bool TryGetShellIconIndex(string path, out int imageIndex)
	{
		imageIndex = -1;
		ITEMIDLIST* pidl = null;
		try
		{
			if (PInvoke.SHParseDisplayName(path, null, out pidl, 0, out _).Value < 0 || pidl is null)
			{
				return false;
			}

			if (PInvoke.SHBindToParent(
				*pidl,
				typeof(IShellIcon).GUID,
				out object shellIconObject,
				out ITEMIDLIST* childPidl).Value < 0)
			{
				return false;
			}

			HRESULT hr = ((IShellIcon)shellIconObject).GetIconOf(
				childPidl,
				PInvoke.GIL_FORSHELL,
				out imageIndex);
			return hr.Value >= 0 && imageIndex >= 0;
		}
		finally
		{
			if (pidl is not null)
			{
				PInvoke.ILFree(pidl);
			}
		}
	}
}

internal static class ComRuntime
{
	[ThreadStatic]
	private static bool shouldUninitialize;

	public static unsafe void Initialize()
	{
		int hr = PInvoke.CoInitializeEx(null, COINIT.COINIT_APARTMENTTHREADED).Value;
		shouldUninitialize = hr == HResult.S_OK || hr == HResult.S_FALSE;
		if (hr != HResult.RPC_E_CHANGED_MODE)
		{
			HResult.ThrowIfFailed(hr);
		}
	}

	public static void Uninitialize()
	{
		if (shouldUninitialize)
		{
			PInvoke.CoUninitialize();
			shouldUninitialize = false;
		}
	}
}

internal static class ComInterop
{
	public static unsafe int CreateInstance<T>(Guid classId, out T instance)
		where T : class
	{
		Guid interfaceId = typeof(T).GUID;
		Guid* classIdPointer = &classId;
		Guid* interfaceIdPointer = &interfaceId;
		HRESULT hr = PInvoke.CoCreateInstance(
			classIdPointer,
			null,
			CLSCTX.CLSCTX_INPROC_SERVER,
			interfaceIdPointer,
			out object result);
		instance = (T)result;
		return hr.Value;
	}
}

internal static class HResult
{
	public static int S_OK => Win32HResult.S_OK.Value;
	public static int S_FALSE => Win32HResult.S_FALSE.Value;
	public static int E_FAIL => Win32HResult.E_FAIL.Value;
	public static int RPC_E_CHANGED_MODE => Win32HResult.RPC_E_CHANGED_MODE.Value;

	public static void ThrowIfFailed(int hr)
	{
		if (hr < 0)
		{
			throw new COMException($"HRESULT 0x{hr:X8}", hr);
		}
	}
}
