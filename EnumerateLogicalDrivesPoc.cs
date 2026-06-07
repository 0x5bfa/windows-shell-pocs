#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0-windows
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property AllowUnsafeBlocks=true
#:property PublishAot=true
#:property InvariantGlobalization=true

using System.Numerics;
using System.Runtime.InteropServices;

return EnumerateLogicalDrivesPoc.Run();

internal static class EnumerateLogicalDrivesPoc
{
	public static int Run()
	{
		uint availableDrives = NativeMethods.GetLogicalDrives();
		if (availableDrives == 0)
		{
			return 0;
		}

		int count = BitOperations.PopCount(availableDrives);
		var driveLetters = new char[count];

		count = 0;
		char driveLetter = 'A';
		while (availableDrives != 0)
		{
			if ((availableDrives & 1) != 0)
			{
				driveLetters[count++] = driveLetter;
			}

			availableDrives >>= 1;
			driveLetter++;
		}

		Console.WriteLine($"Available drives: {string.Join(", ", driveLetters)}");
		return 0;
	}
}

internal static partial class NativeMethods
{
	[LibraryImport("kernel32.dll")]
	internal static partial uint GetLogicalDrives();
}
