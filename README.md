# Terminal Drop Path

[English](README.en.md)

Terminal Drop Path 是一个面向 Windows 10/11 传统 CMD 和 PowerShell 控制台的轻量开源工具。它提供一个置顶工具窗，既能把文件或文件夹的完整路径输入回启动它的终端，也能整理终端选区中的多余换行，不依赖 Windows Terminal。

## 功能

- 支持文件、文件夹和多选拖放
- 保留中文文件名和扩展名
- 自动生成绝对路径
- 按照 CMD 和 PowerShell 语法处理含空格的路径
- 只输入路径，不自动执行命令
- 将终端选区按段落复制，合并段内折行并保留空行
- 可将命令、URL 等内容强制复制为单行
- 不联网、不读取拖入文件的内容、不后台监控剪贴板
- 单个可执行文件，使用 Windows 自带的 .NET Framework 运行

## 快速开始

从 [Releases](https://github.com/sutaon/terminal-drop-path/releases) 下载 `TerminalDropPath-v0.2.0-windows.zip` 并解压。请不要下载 GitHub 自动生成、需要自行构建的 `Source code` 压缩包。

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

## 整理终端复制换行

当终端中的一段长文字在窗口边缘折成多行时，普通复制有时会把显示折行也带到记事本、Word 或其他编辑器中。处理方法：

1. 在启动本工具的原终端中选中文字。
2. 在 `Copy mode` 中选择模式。
3. 点击 `Copy terminal selection`，然后正常粘贴。

请在控制台标题栏菜单的“属性 -> 选项”中启用“启用自动换行选择”（`Enable line wrapping selection`）。这个选项让传统控制台自身在复制时精确去掉显示软折行；快捷方式中保存的设置可能覆盖全局控制台设置，修改后需要重新选择文字。

`Keep paragraphs` 是默认模式。它合并段内的单个换行，保留空行形成的段落边界；中文行直接衔接，英文行在需要时补一个空格。

`One line` 删除选区中的全部换行且不额外插入空格，适合命令、路径、哈希值和 URL。

这项功能只在点击按钮时读取本次控制台复制的纯文本，不注册全局快捷键，也不监听之后的剪贴板变化。如果内容必须保留每一个真实换行，例如代码、日志、列表、诗歌或表格，请继续使用终端原本的 `Ctrl+C`，不要使用整理模式。

> 控制台公开接口无法判断应用程序主动写入的单个换行究竟是“段内折行”还是有语义的真实换行。因此默认模式采用明确规则：单换行属于段内，空行属于段落边界；它不会宣称自动理解所有文本结构。

## 直接拖入传统控制台

多数非管理员模式的 Windows 10/11 CMD 和 PowerShell 本身也允许把文件直接拖入窗口。如果原生拖放已经可用，就不必运行本工具。Terminal Drop Path 主要解决以下场景：

- 希望稳定处理多个文件或文件夹
- 希望根据 CMD/PowerShell 自动处理引号
- 当前控制台的原生拖放被宿主或配置影响
- 希望拖放后仍有机会编辑输入，而不是自动执行

## 安全限制

Windows 会阻止低权限程序向管理员窗口发送输入。请让 Terminal Drop Path 与目标终端使用相同权限级别。通常建议使用非管理员终端，不要为了拖放功能关闭 UAC。

拖放路径时，工具只向启动它的控制台窗口发送 Unicode 键盘输入。发送前会重新确认目标窗口处于前台，避免把路径输入到其他应用。

CMD 即使在双引号内也会展开 `%变量%`，启用延迟展开时还会处理 `!变量!`。因此工具会拒绝向 CMD 输入包含 `%` 或 `!` 的路径，并提示改用 PowerShell 模式；PowerShell 的单引号格式可以安全保留这两个字符。Windows 的前台窗口切换与 `SendInput` 无法做到完全原子，工具会在发送紧前再次核对窗口，但发送期间仍不要主动切换到其他应用。

复制时，工具先确认目标控制台仍然存在有效选区，再调用控制台自身的复制命令；无选区时不会发送 Enter 或执行当前命令。仅当文本需要整理时，剪贴板内容会被替换为纯文本，原有 HTML/RTF 格式不会保留。

## 从源码构建

不需要安装 .NET SDK。脚本优先使用 Windows 中的 .NET Framework C# 编译器；若系统缺少该编译器，请安装 .NET Framework 4.8 Developer Pack：

```powershell
.\build.ps1
.\tests\TerminalDropPath.Tests.ps1
.\package.ps1 -SkipBuild
```

生成文件位于 `bin\TerminalDropPath.exe`，发布压缩包位于 `dist\TerminalDropPath-v0.2.0-windows.zip`。

## 命令行

```text
.\bin\TerminalDropPath.exe [--shell auto|cmd|powershell]
.\bin\TerminalDropPath.exe --format-only --shell cmd -- <path> [path...]
.\bin\TerminalDropPath.exe --normalize-copy paragraphs|single-line
```

`--format-only` 只输出格式化后的路径，供测试和脚本集成使用。

`--normalize-copy` 从标准输入按 UTF-8 读取文本，并把无 BOM 的 UTF-8 整理结果写到标准输出。`paragraphs` 合并段内换行并保留空行，`single-line` 删除所有换行。

CMD 可直接使用字节重定向：

```bat
type input.txt | bin\TerminalDropPath.exe --normalize-copy paragraphs > output.txt
```

Windows PowerShell 5.1 和 PowerShell 7 不支持 `< input.txt` 语法。需要从 PowerShell 调用时，可让 `cmd.exe` 只负责 UTF-8 文件的字节重定向：

```powershell
cmd.exe /d /c ".\bin\TerminalDropPath.exe --normalize-copy paragraphs < input.txt > output.txt"
```

## 许可证

[MIT](LICENSE)
