# Repository Instructions

- Save files as CRLF and UTF-8 without BOM.
- Use file-based C# source; each `.cs` sample should build with `dotnet run --file <file>.cs`.
- Do not use CsWin32; manually define interop code.
- For COM, use `GeneratedComInterface` and marshaled interface instances instead of raw pointers.
- Do not add `.csproj`, `.sln`, or `.slnx` files unless the user explicitly asks for a dedicated WinUI project like `DtshPocWinUI`.

## WinUI plugin setup

- Do not commit `win-dev-skills/`; it is a local clone used to install the Codex WinUI plugin.
- If a WinUI POC needs the plugin, set it up from a clone:

```powershell
if (-not (Test-Path .\win-dev-skills)) {
    git clone https://github.com/microsoft/win-dev-skills.git win-dev-skills
}

codex plugin marketplace add .\win-dev-skills
codex plugin add winui@win-dev-skills
codex plugin list
```

- After setup, use the plugin skills from the local clone. For this repo, prefer `win-dev-skills\plugins\winui\skills\winui-dev-workflow\BuildAndRun.ps1` when building or launching WinUI projects.
