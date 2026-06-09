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

WinUI POC:

```powershell
.\win-dev-skills\plugins\winui\skills\winui-dev-workflow\BuildAndRun.ps1 .\DtshPocWinUI\DtshPocWinUI.csproj -SkipRun
```

Agent WinUI plugin setup:

```powershell
if (-not (Test-Path .\win-dev-skills)) {
    git clone https://github.com/microsoft/win-dev-skills.git win-dev-skills
}

codex plugin marketplace add .\win-dev-skills
codex plugin add winui@win-dev-skills
codex plugin list
```

`win-dev-skills/` is a local clone for agent tooling and is intentionally ignored.

Notes:

- `DtshPoc.cs` targets the internal `CLSID_DetectionAndSharing` / `IDetectionAndSharing` object from `dtsh.dll`. `turn-on` may require an elevated console.
- `ShellUrlPoc.cs` targets the internal `CLSID_ShellUrl` / `IShellUrl` object from `ExplorerFrame.dll`.
- These internal interfaces are inferred from Windows call sites, so treat them as undocumented and version-sensitive.
