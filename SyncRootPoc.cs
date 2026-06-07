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

return SyncRootPoc.Run(args);

internal static class SyncRootPoc
{
	private const string DefaultPath = @"C:\Users\onein\OneDrive - Academic\resume.pdf";

	public static int Run(string[] args)
	{
		if (args is ["--help"] or ["-h"] or ["/?"])
		{
			Console.WriteLine("Usage: dotnet run --file SyncRootPoc.cs -- <sync-root-file-path>");
			Console.WriteLine($"Default path: {DefaultPath}");
			return 0;
		}

		string path = args.Length > 0 ? args[0] : DefaultPath;

		HResult.ThrowIfFailed(NativeMethods.CoInitializeEx(0, COINIT.ApartmentThreaded));

		try
		{
			ISyncRootManager syncRootManager = ComActivation.CreateSyncRootManager();

			HResult.ThrowIfFailed(syncRootManager.GetStorageProviderInfoFromPath(
				path,
				out IStorageProviderInfo storageProviderInfo,
				out string? displayName,
				out int compareFlags));

			HResult.ThrowIfFailed(storageProviderInfo.GetFullProviderAndUserAndAccountIdentifier(
				out string? providerAndAccountId));

			HResult.ThrowIfFailed(storageProviderInfo.GetContextMenuVerbs(
				out uint verbCount,
				out Guid[] verbs,
				out int reserved));

			Console.WriteLine($"Path: {path}");
			Console.WriteLine($"Display name: {displayName ?? "(null)"}");
			Console.WriteLine($"Provider/user/account: {providerAndAccountId ?? "(null)"}");
			Console.WriteLine($"Compare flags: {compareFlags}");
			Console.WriteLine($"Context menu verbs: {verbCount}");

			for (int i = 0; i < verbs.Length; i++)
			{
				Console.WriteLine($"  {verbs[i]}");
			}

			if (reserved != 0)
			{
				Console.WriteLine($"Reserved: {reserved}");
			}

			return 0;
		}
		finally
		{
			NativeMethods.CoUninitialize();
		}
	}
}

internal static class ComActivation
{
	private static readonly Guid SyncRootManagerClsid = new("F324E4F9-8496-40B2-A1FF-9617C1C9AFFE");

	public static ISyncRootManager CreateSyncRootManager()
	{
		Guid clsid = SyncRootManagerClsid;
		Guid iid = typeof(ISyncRootManager).GUID;

		HResult.ThrowIfFailed(NativeMethods.CoCreateInstance(
			ref clsid,
			null,
			CLSCTX.InprocServer,
			ref iid,
			out ISyncRootManager syncRootManager));

		return syncRootManager;
	}
}

internal static partial class NativeMethods
{
	[LibraryImport("ole32.dll")]
	internal static partial int CoInitializeEx(nint pvReserved, COINIT coInit);

	[LibraryImport("ole32.dll")]
	internal static partial void CoUninitialize();

	[LibraryImport("ole32.dll")]
	internal static partial int CoCreateInstance(
		ref Guid rclsid,
		IUnknownObject? outer,
		CLSCTX clsContext,
		ref Guid riid,
		out ISyncRootManager obj);
}

internal static class HResult
{
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
[Guid("692d40a4-efa1-4089-88f8-15fd6f5f8b64")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface ISyncRootManager : IUnknownObject
{
	[PreserveSig]
	int GetStorageProviderInfo();

	[PreserveSig]
	int GetStorageProviderInfoFromPath(
		string filePath,
		out IStorageProviderInfo storageProviderInfo,
		out string? displayName,
		out int compareFlags);
}

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("ca01c124-2769-4576-bf12-8a54ee671a86")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IStorageProviderInfo : IUnknownObject
{
	[PreserveSig]
	int GetFullProviderAndUserAndAccountIdentifier(out string? value);

	[PreserveSig]
	int SetFullProviderAndUserAndAccountIdentifier(string value);

	[PreserveSig]
	int GetProviderIdentifier(out string? value);

	[PreserveSig]
	int IsSameIdentifier(IStorageProviderInfo other);

	[PreserveSig]
	int GetDisplayName(out string? value);

	[PreserveSig]
	int GetDisplayNameResource(out string? value);

	[PreserveSig]
	int SetDisplayNameResource(string value);

	[PreserveSig]
	int GetIcon(out string? iconPath, out int iconIndex);

	[PreserveSig]
	int GetIconResource(out string? value);

	[PreserveSig]
	int SetIconResource(string value);

	[PreserveSig]
	int SetHandlerClsid(in Guid clsid);

	[PreserveSig]
	int GetHandler(in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int SetBannerHandlerClsid(in Guid clsid);

	[PreserveSig]
	int GetBannerHandler(in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int SetCustomStateHandlerClsid(in Guid clsid);

	[PreserveSig]
	int GetCustomStateHandler(in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int SetThumbnailProviderClsid(in Guid clsid);

	[PreserveSig]
	int GetThumbnailProvider(string arg, in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int GetFlags(out StorageProviderInfoFlags flags);

	[PreserveSig]
	int SetFlags(StorageProviderInfoFlags mask, StorageProviderInfoFlags value);

	[PreserveSig]
	int GetCustomStateIdList(
		[MarshalUsing(CountElementName = nameof(count))] out uint[] ids,
		out uint count);

	[PreserveSig]
	int GetCustomStateDisplayName(uint id, out string? name);

	[PreserveSig]
	int GetProtectionMode(out string? mode);

	[PreserveSig]
	int SetProtectionMode(string mode);

	[PreserveSig]
	int GetRecycleBinUrl(out string? url);

	[PreserveSig]
	int SetRecycleBinUrl(string url);

	[PreserveSig]
	int SetExtendedPropertiesHandlerClsid(in Guid clsid);

	[PreserveSig]
	int GetExtendedPropertiesHandler(in Guid riid, out IUnknownObject obj);

	[PreserveSig]
	int GetContextMenuVerbs(
		out uint count,
		[MarshalUsing(CountElementName = nameof(count))] out Guid[] verbs,
		out int reserved);
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
internal enum StorageProviderInfoFlags : uint
{
	None = 0,
}
