# Repository Instructions

- Save files as CRLF and UTF-8 without BOM.
- Use file-based C# source; each `.cs` sample should build with `dotnet run --file <file>.cs`.
- Use `System.CommandLine` for command-line parsing in executable samples.
- Follow `ReplicateFileItemActivationPoc.cs` for the canonical file-based app
  header, generated CsWin32 usage, and AOT-safe command entry point.
- Use `Microsoft.Windows.CsWin32` for public Win32, Shell, COM, structs, enums, handles, and P/Invoke declarations. Add required identifiers to the repository-level `NativeMethods.txt`; do not manually redeclare public SDK APIs.
- Keep manually declared COM interfaces only for undocumented or SDK-missing contracts, and isolate them from generated public interop.
- Keep samples NativeAOT-safe: prefer CsWin32 generated signatures, `sizeof(T)` or compile-time SDK constants for structure sizes, and explicit unmanaged buffers. Do not use reflection-based or runtime-marshalling patterns such as `Marshal.SizeOf`, `Marshal.PtrToStructure`, or `Marshal.GetDelegateForFunctionPointer`.
- Do not add `.csproj`, `.sln`, or `.slnx` files.
- Codex plugins such as `microsoft/win-dev-skills` are Codex-level tooling; install them in Codex, not as project files in this repository.
