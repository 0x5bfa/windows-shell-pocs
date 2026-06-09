# Shell API sample for Files

This repository uses file-based C# samples instead of project files. Each `.cs`
sample is self-contained and buildable with `dotnet run --file`.

Shell samples:

```powershell
dotnet run --file EnumerateLogicalDrivesPoc.cs
dotnet run --file EnumerateFolderPoc.cs -- C:\
dotnet run --file EnumerateDetailsViewColumnsPoc.cs -- C:\
dotnet run --file EnumerateSearchFolderPoc.cs
dotnet run --file GetFolderViewPoc.cs -- C:\
dotnet run --file ShellUrlPoc.cs -- "shell:Downloads"
dotnet run --file DtshPoc.cs -- status
dotnet run --file EnumerateOpenWithMenuItemsPoc.cs -- README.md
```

Side-effecting shell samples:

```powershell
dotnet run --file ShowFormatDriveDialogPoc.cs
dotnet run --file EnumerateJumpListPoc.cs -- [file]
dotnet run --file RegisterFolderChangeNotificationPoc.cs -- [folder]
dotnet run --file PinFolderToQuickAccessPoc.cs -- [folder]
dotnet run --file SetVolumeLabelPoc.cs -- [label]
dotnet run --file DtshPoc.cs -- turn-on
dotnet run --file DtshPoc.cs -- open-net-center
```

Sync-root sample:

```powershell
dotnet run --file SyncRootPoc.cs -- --help
```

Notes:

- `DtshPoc.cs` targets the internal `CLSID_DetectionAndSharing` / `IDetectionAndSharing` object from `dtsh.dll`. `turn-on` may require an elevated console.
- `ShellUrlPoc.cs` targets the internal `CLSID_ShellUrl` / `IShellUrl` object from `ExplorerFrame.dll`.
- These internal interfaces are inferred from Windows call sites, so treat them as undocumented and version-sensitive.
