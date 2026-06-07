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

if (args is ["--help"] or ["-h"] or ["/?"])
{
	Console.WriteLine("""
Usage:
  dotnet run --file ShellUrlPoc.cs -- "C:\Windows"
  dotnet run --file ShellUrlPoc.cs -- "shell:Downloads"
""");
	return 0;
}

string input = args.Length > 0 ? string.Join(' ', args) : Environment.CurrentDirectory;

Console.WriteLine("ShellUrl POC");
Console.WriteLine($"Input: {input}");

IShellUrl shellUrl = ShellUrlNative.CreateShellUrl();
shellUrl.SetOwnerWindow(0);

using SafePidlHandle currentDirectoryPidl = ShellUrlNative.TryParseDisplayName(Environment.CurrentDirectory);
if (!currentDirectoryPidl.IsInvalid)
{
	Check(shellUrl.SetCurrentDirectoryPidl(currentDirectoryPidl.DangerousGetHandle()), "IShellUrl.SetCurrentDirectoryPidl");
}

int flags = shellUrl.GetParseFlags(isFileDialog: false) | 0x1;
Check(shellUrl.Parse(input, flags), "IShellUrl.Parse");
Check(shellUrl.GetPidl(out nint parsedPidlValue), "IShellUrl.GetPidl");

using SafePidlHandle parsedPidl = SafePidlHandle.Attach(parsedPidlValue);

Console.WriteLine($"Parse flags: 0x{flags:X8}");
Console.WriteLine($"PIDL: 0x{parsedPidl.DangerousGetHandle():X}");
Console.WriteLine($"File system path: {ShellUrlNative.TryGetName(parsedPidl, SIGDN.FileSysPath) ?? "(not a file-system path)"}");
Console.WriteLine($"Parsing name: {ShellUrlNative.TryGetName(parsedPidl, SIGDN.DesktopAbsoluteParsing) ?? "(unavailable)"}");

var displayBuffer = new char[2084];
if (shellUrl.GetDisplayText(displayBuffer, displayBuffer.Length) >= 0)
{
	Console.WriteLine($"Display text: {CreateNullTerminatedString(displayBuffer)}");
}

if (shellUrl.GetDisplayNameAlloc(out nint displayNameValue) >= 0)
{
	using SafeCoTaskMemString displayName = SafeCoTaskMemString.Attach(displayNameValue);
	if (!displayName.IsInvalid)
	{
		Console.WriteLine($"Display name alloc: {displayName}");
	}
}

return 0;

static void Check(int hr, string operation)
{
	if (hr < 0)
	{
		Marshal.ThrowExceptionForHR(hr);
	}
}

static string CreateNullTerminatedString(char[] buffer)
{
	int length = Array.IndexOf(buffer, '\0');
	if (length < 0)
	{
		length = buffer.Length;
	}

	return new string(buffer, 0, length);
}

internal static class ShellUrlNative
{
	private static readonly Guid ClsidShellUrl = new("4BEC2015-BFA1-42FA-9C0C-59431BBE880E");

	public static IShellUrl CreateShellUrl()
	{
		Guid clsid = ClsidShellUrl;
		Guid iid = typeof(IShellUrl).GUID;
		int hr = NativeMethods.SHCoCreateInstance(
			null,
			ref clsid,
			null,
			ref iid,
			out IShellUrl shellUrl);

		if (hr < 0)
		{
			throw new InvalidOperationException(
				$"SHCoCreateInstance(CLSID_ShellUrl) failed: 0x{hr:X8}",
				Marshal.GetExceptionForHR(hr));
		}

		return shellUrl;
	}

	public static SafePidlHandle TryParseDisplayName(string parsingName)
	{
		int hr = NativeMethods.SHParseDisplayName(parsingName, null, out nint pidl, 0, out _);
		return hr >= 0 ? SafePidlHandle.Attach(pidl) : SafePidlHandle.Invalid;
	}

	public static string? TryGetName(SafePidlHandle pidl, SIGDN sigdn)
	{
		int hr = NativeMethods.SHGetNameFromIDList(pidl.DangerousGetHandle(), sigdn, out nint name);
		if (hr < 0)
		{
			return null;
		}

		using SafeCoTaskMemString safeName = SafeCoTaskMemString.Attach(name);
		return safeName.IsInvalid ? null : safeName.ToString();
	}
}

internal static partial class NativeMethods
{
	[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	internal static partial int SHCoCreateInstance(
		string? className,
		ref Guid clsid,
		IUnknownObject? outer,
		ref Guid riid,
		out IShellUrl obj);

	[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	internal static partial int SHParseDisplayName(
		string name,
		IUnknownObject? bindContext,
		out nint pidl,
		uint attributesIn,
		out uint attributesOut);

	[LibraryImport("shell32.dll")]
	internal static partial int SHGetNameFromIDList(
		nint pidl,
		SIGDN sigdnName,
		out nint name);

	[LibraryImport("shell32.dll")]
	internal static partial void ILFree(nint pidl);
}

internal sealed class SafePidlHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	private SafePidlHandle()
		: base(ownsHandle: true)
	{
	}

	public static SafePidlHandle Invalid => new();

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

internal sealed class SafeCoTaskMemString : SafeHandleZeroOrMinusOneIsInvalid
{
	private SafeCoTaskMemString()
		: base(ownsHandle: true)
	{
	}

	public static SafeCoTaskMemString Attach(nint handle)
	{
		var safeHandle = new SafeCoTaskMemString();
		safeHandle.SetHandle(handle);
		return safeHandle;
	}

	public override string ToString()
		=> Marshal.PtrToStringUni(handle) ?? string.Empty;

	protected override bool ReleaseHandle()
	{
		Marshal.FreeCoTaskMem(handle);
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
[Guid("4F33718D-BAE1-4F9B-96F2-D2A16E683346")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal partial interface IShellUrl : IUnknownObject
{
	[PreserveSig]
	int Parse(string text, int flags);

	[PreserveSig]
	int Unknown_20();

	[PreserveSig]
	int SetUserInput(string text, int sourceOrMode);

	[PreserveSig]
	int GetDisplayText(
		[Out, MarshalUsing(CountElementName = nameof(cchBuffer))] char[] buffer,
		int cchBuffer);

	[PreserveSig]
	int GetPidl(out nint pidl);

	[PreserveSig]
	int SetPidl(nint pidl);

	[PreserveSig]
	int SetPidlAndDisplayText(nint pidl, string? displayText);

	[PreserveSig]
	nint GetNavigationText();

	[PreserveSig]
	int Unknown_58();

	[PreserveSig]
	int SetUnknownState(int value);

	[PreserveSig]
	int ParseAsync(nint hwndOwner, string text, int flags, nint cancelToken);

	[PreserveSig]
	int GetParseResult();

	[PreserveSig]
	int SetParseId(int parseId);

	[PreserveSig]
	int GetParseId(out int parseId);

	[PreserveSig]
	int SetParseOptions(int value1, int value2);

	[PreserveSig]
	int Unknown_90();

	[PreserveSig]
	int ExecutePlaceholder(nint hwnd, nint arg1, nint arg2, int flags);

	[PreserveSig]
	int SetCurrentDirectoryPidl(nint pidl);

	[PreserveSig]
	void SetOwnerWindow(nint hwnd);

	[PreserveSig]
	int GetParsedPidl(out nint pidl);

	[PreserveSig]
	int GetParseFlags([MarshalAs(UnmanagedType.Bool)] bool isFileDialog);

	[PreserveSig]
	int GetDisplayNameAlloc(out nint displayName);

	[PreserveSig]
	int GetNavigationDisplayNameAlloc(out nint displayName);
}

internal enum SIGDN : uint
{
	FileSysPath = 0x80058000,
	DesktopAbsoluteParsing = 0x80028000,
}
