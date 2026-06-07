#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0-windows
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property AllowUnsafeBlocks=true
#:property PublishAot=true
#:property InvariantGlobalization=true

using System.Runtime.InteropServices;

if (args is ["--help"] or ["-h"] or ["/?"])
{
	Console.WriteLine("Usage: dotnet run --file ShowFormatDriveDialogPoc.cs");
	return 0;
}

NativeMethods.SHFormatDrive(NativeMethods.GetConsoleWindow(), 3, SHFMT_ID.Default, SHFMT_OPT.Full);
return 0;

internal static partial class NativeMethods
{
	[LibraryImport("kernel32.dll")]
	internal static partial nint GetConsoleWindow();

	[LibraryImport("shell32.dll")]
	internal static partial uint SHFormatDrive(nint hwnd, uint drive, SHFMT_ID formatId, SHFMT_OPT options);
}

internal enum SHFMT_ID : uint
{
	Default = 0xFFFF_FFFF,
}

[Flags]
internal enum SHFMT_OPT : uint
{
	Full = 0x0001,
	SysOnly = 0x0002,
}
