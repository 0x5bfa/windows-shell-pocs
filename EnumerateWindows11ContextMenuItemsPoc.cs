#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0-windows
#:property ImplicitUsings=enable
#:property Nullable=enable
#:property PublishAot=true
#:property InvariantGlobalization=true

if (args is ["--help"] or ["-h"] or ["/?"])
{
	Console.WriteLine("Usage: dotnet run --file EnumerateWindows11ContextMenuItemsPoc.cs");
	return 0;
}

Console.WriteLine("The original EnumerateWindows11ContextMenuItems method was empty.");
Console.WriteLine("Use EnumerateOpenWithMenuItemsPoc.cs for the migrated context menu sample.");
return 0;
