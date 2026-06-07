#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0-windows
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property PublishAot=true
#:property InvariantGlobalization=true

if (args is ["--help"] or ["-h"] or ["/?"])
{
	Console.WriteLine("Usage: dotnet run --file RegisterFolderChangeNotificationPoc.cs -- [folder]");
	return 0;
}

string folderPath = args.Length > 0
	? args[0]
	: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

if (!Directory.Exists(folderPath))
{
	Console.Error.WriteLine($"Folder not found: {folderPath}");
	return 2;
}

using var watcher = new FileSystemWatcher(folderPath)
{
	IncludeSubdirectories = false,
	EnableRaisingEvents = true,
};

watcher.Created += (_, e) => Console.WriteLine($"Created: {e.FullPath}");
watcher.Changed += (_, e) => Console.WriteLine($"Changed: {e.FullPath}");
watcher.Deleted += (_, e) => Console.WriteLine($"Deleted: {e.FullPath}");
watcher.Renamed += (_, e) => Console.WriteLine($"Renamed: {e.OldFullPath} to {e.FullPath}");

Console.WriteLine($"Watching: {folderPath}");
Console.WriteLine("Press Enter to stop.");
Console.ReadLine();

return 0;
