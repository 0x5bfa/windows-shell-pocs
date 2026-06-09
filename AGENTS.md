# Repository Instructions

- Save files as CRLF and UTF-8 without BOM.
- Use file-based C# source; each `.cs` sample should build with `dotnet run --file <file>.cs`.
- Do not use CsWin32; manually define interop code.
- For COM, use `GeneratedComInterface` and marshaled interface instances instead of raw pointers.
- Do not add `.csproj`, `.sln`, or `.slnx` files.
- Codex plugins such as `microsoft/win-dev-skills` are Codex-level tooling; install them in Codex, not as project files in this repository.
