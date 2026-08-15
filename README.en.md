# Terminal Drop Path

[中文说明](README.md)

Terminal Drop Path is a lightweight Windows 10/11 companion for classic CMD and PowerShell consoles. It now starts as a completely hidden background copy cleaner by default, while the original path-drop window remains available on demand. Windows Terminal is not required.

## Features

- Files, folders, and multi-selection
- Unicode file names and extensions
- Absolute path normalization
- Space-safe quoting for CMD and PowerShell
- Types paths without pressing Enter
- Paragraph-aware copying that joins wrapped lines and keeps blank-line boundaries
- One-line copying for commands, URLs, paths, and hashes
- No window, taskbar button, or tray icon in the default mode
- One listener per target terminal, even when launched repeatedly
- Automatic background-process exit when the target terminal closes
- No network access or dropped-file content reads; only target-terminal copies are read or normalized
- One executable that runs on the built-in .NET Framework

## Usage

Download and extract `TerminalDropPath-v0.3.0-windows.zip` from [Releases](https://github.com/sutaon/terminal-drop-path/releases). Do not download GitHub's automatically generated `Source code` archives, which require a local build.

From CMD:

```bat
DropPath.cmd
```

From Windows PowerShell or PowerShell 7:

```powershell
.\DropPath.ps1
```

The command returns immediately without showing a window, taskbar button, or tray icon. The background instance stays active while that original terminal remains open and exits automatically when it closes. Repeated launches from the same terminal keep only one instance.

Enable both `Properties -> Options -> QuickEdit Mode` and `Enable line wrapping selection` in the console title-bar menu. Then left-drag to select text, right-click inside the terminal to copy, and paste normally into another document.

## Show the path-drop window

Use the visible interface when you need file or folder dropping, copy-mode controls, pause/resume, or the manual copy fallback.

From CMD:

```bat
DropPath.cmd --show-window
```

From Windows PowerShell or PowerShell 7:

```powershell
.\DropPath.ps1 -ShowWindow
```

If a hidden instance is already running, this reveals that same instance instead of creating another clipboard listener. Drop files or folders onto the window to type their formatted paths into the original console input line without pressing Enter.

## Copy wrapped terminal text

When a long paragraph wraps at the edge of the terminal, a normal copy can sometimes carry those display line breaks into Notepad, Word, or another editor. Automatic cleanup is enabled as soon as the hidden instance starts; no tool-window click is required.

Enable both `Properties -> Options -> QuickEdit Mode` and `Enable line wrapping selection` in the console title-bar menu. QuickEdit makes right-click copy a left-drag selection; line-wrapping selection lets the classic console itself remove display-only soft wraps precisely. Settings stored in a console shortcut can override the global console setting; reselect the text after changing it.

`Keep paragraphs` is the default. It joins single line breaks inside a paragraph and retains blank lines as paragraph boundaries. CJK text is joined directly; a space is added at likely Latin word boundaries.

`One line` removes every line break without inserting spaces. Use it for commands, paths, hashes, and URLs.

While running, the tool receives Windows clipboard-update notifications but processes an update only when the clipboard owner is its original target terminal. Copies from browsers, editors, and other terminals are neither read nor modified. `Ctrl+C` copies made in the target terminal are normalized too. `Copy selection now` remains available as a manual fallback and is not required for the normal workflow.

When every real line break must be retained, such as for source code, logs, lists, poetry, or tables, reveal the interface with `--show-window` / `-ShowWindow` and clear `Auto-clean terminal copies`, or close the interface to stop the tool before using the terminal's normal copy behavior.

> Public console APIs cannot determine whether a single hard line break emitted by an application is merely paragraph wrapping or a meaningful break. The paragraph mode therefore uses an explicit rule: single breaks are joined and blank lines delimit paragraphs. It does not claim to infer every document structure.

## Security boundary

Windows blocks lower-integrity applications from sending input to elevated windows. Run the tool and target terminal at the same integrity level. Prefer a standard, non-administrator terminal; do not disable UAC for drag and drop.

Before typing a path, the tool verifies that its original terminal is the foreground window.

CMD expands `%VARIABLE%` even inside double quotes and also processes `!VARIABLE!` when delayed expansion is enabled. The tool therefore rejects CMD paths containing `%` or `!` and asks you to use PowerShell mode, whose single-quoted form preserves both characters. Foreground changes and `SendInput` cannot be perfectly atomic; the tool rechecks the target immediately before sending, but you should still avoid switching applications during the send.

Automatic cleanup accepts text only when the target console still owns the clipboard and its sequence number has not changed; both are rechecked before the read and write so a later copy is not overwritten. The manual copy button also verifies that the original console has a non-empty selection before invoking the console's own Copy command. It never sends Enter when no selection exists. If normalization is needed, the copied value is replaced with plain text, so HTML/RTF clipboard formatting is not retained.

## Build and test

No .NET SDK is required. The build script first uses the .NET Framework compiler available on Windows; install the .NET Framework 4.8 Developer Pack if that compiler is missing:

```powershell
.\build.ps1
.\tests\TerminalDropPath.Tests.ps1
.\package.ps1 -SkipBuild
```

The executable is written to `bin\TerminalDropPath.exe`, and the release archive to `dist\TerminalDropPath-v0.3.0-windows.zip`.

## Command line

```text
.\bin\TerminalDropPath.exe [--shell auto|cmd|powershell] [--show-window]
.\bin\TerminalDropPath.exe --format-only --shell cmd -- <path> [path...]
.\bin\TerminalDropPath.exe --normalize-copy paragraphs|single-line
```

Without `--show-window`, the executable runs as a hidden background instance. The CMD launcher forwards `--show-window`; the PowerShell launcher uses `-ShowWindow`.

`--normalize-copy` reads UTF-8 text from standard input and writes normalized UTF-8 without a BOM. `paragraphs` joins single line breaks while keeping blank lines; `single-line` removes every line break.

CMD can use byte-preserving redirection directly:

```bat
type input.txt | bin\TerminalDropPath.exe --normalize-copy paragraphs > output.txt
```

Windows PowerShell 5.1 and PowerShell 7 do not support `< input.txt`. From PowerShell, let `cmd.exe` handle only the UTF-8 file redirection:

```powershell
cmd.exe /d /c ".\bin\TerminalDropPath.exe --normalize-copy paragraphs < input.txt > output.txt"
```

## License

[MIT](LICENSE)
