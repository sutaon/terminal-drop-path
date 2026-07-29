# Terminal Drop Path

[中文说明](README.md)

Terminal Drop Path is a lightweight Windows 10/11 companion for classic CMD and PowerShell consoles. Drop files or folders onto its always-on-top window and it types their absolute paths back into the terminal that launched it. Windows Terminal is not required.

## Features

- Files, folders, and multi-selection
- Unicode file names and extensions
- Absolute path normalization
- Shell-aware quoting for CMD and PowerShell
- Types paths without pressing Enter
- No network access, file-content reads, or clipboard use
- One executable that runs on the built-in .NET Framework

## Usage

Download and extract the archive from [Releases](https://github.com/sutaon/terminal-drop-path/releases).

From CMD:

```bat
DropPath.cmd
```

From Windows PowerShell or PowerShell 7:

```powershell
.\DropPath.ps1
```

Drop files or folders onto the `Terminal Drop Path` window. The tool types the formatted paths into the original console input line and leaves execution to you.

## Security boundary

Windows blocks lower-integrity applications from sending input to elevated windows. Run the tool and target terminal at the same integrity level. Prefer a standard, non-administrator terminal; do not disable UAC for drag and drop.

Before typing, the tool verifies that its original terminal is the foreground window. It does not use the clipboard.

## Build and test

No .NET SDK is required. The build script uses the .NET Framework compiler included with Windows:

```powershell
.\build.ps1
.\tests\TerminalDropPath.Tests.ps1
```

The executable is written to `bin\TerminalDropPath.exe`.

## License

[MIT](LICENSE)
