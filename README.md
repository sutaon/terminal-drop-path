# Terminal Drop Path

[English](README.en.md)

Terminal Drop Path 是一个面向 Windows 10/11 传统 CMD 和 PowerShell 控制台的轻量开源工具。它提供一个置顶拖放区，将文件或文件夹的完整路径直接输入回启动它的终端，不依赖 Windows Terminal，也不会占用剪贴板。

## 功能

- 支持文件、文件夹和多选拖放
- 保留中文文件名和扩展名
- 自动生成绝对路径
- 分别按照 CMD 和 PowerShell 语法处理空格及特殊字符
- 只输入路径，不自动执行命令
- 不联网、不读取文件内容、不使用剪贴板
- 单个可执行文件，使用 Windows 自带的 .NET Framework 运行

## 快速开始

从 [Releases](https://github.com/sutaon/terminal-drop-path/releases) 下载压缩包并解压。

在 CMD 中运行：

```bat
DropPath.cmd
```

在 Windows PowerShell 或 PowerShell 7 中运行：

```powershell
.\DropPath.ps1
```

随后将文件或文件夹拖到出现的 `Terminal Drop Path` 小窗口。路径会被输入到原终端的当前输入行中，例如：

```text
C:\Users\Example\Desktop\示例项目-v3.txt
```

工具不会自动按 Enter，你可以继续补充命令或确认内容后再执行。

## 直接拖入传统控制台

多数非管理员模式的 Windows 10/11 CMD 和 PowerShell 本身也允许把文件直接拖入窗口。如果原生拖放已经可用，就不必运行本工具。Terminal Drop Path 主要解决以下场景：

- 希望稳定处理多个文件或文件夹
- 希望根据 CMD/PowerShell 自动处理引号
- 当前控制台的原生拖放被宿主或配置影响
- 希望拖放后仍有机会编辑输入，而不是自动执行

## 安全限制

Windows 会阻止低权限程序向管理员窗口发送输入。请让 Terminal Drop Path 与目标终端使用相同权限级别。通常建议使用非管理员终端，不要为了拖放功能关闭 UAC。

工具只向启动它的控制台窗口发送 Unicode 键盘输入。发送前会重新确认目标窗口处于前台，避免把路径输入到其他应用。

## 从源码构建

不需要安装 .NET SDK。Windows 自带的 .NET Framework C# 编译器即可构建：

```powershell
.\build.ps1
.\tests\TerminalDropPath.Tests.ps1
```

生成文件位于 `bin\TerminalDropPath.exe`。

## 命令行

```text
TerminalDropPath.exe [--shell auto|cmd|powershell]
TerminalDropPath.exe --format-only --shell cmd -- <path> [path...]
```

`--format-only` 只输出格式化后的路径，供测试和脚本集成使用。

## 许可证

[MIT](LICENSE)
