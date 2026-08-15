using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Terminal Drop Path")]
[assembly: System.Reflection.AssemblyDescription("Drops paths and copies selections cleanly in classic Windows terminals.")]
[assembly: System.Reflection.AssemblyCompany("sutaon")]
[assembly: System.Reflection.AssemblyProduct("Terminal Drop Path")]
[assembly: System.Reflection.AssemblyCopyright("Copyright (c) 2026 sutaon")]
[assembly: System.Reflection.AssemblyVersion("0.2.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.2.1.0")]

namespace TerminalDropPath
{
    internal enum ShellMode
    {
        Auto,
        Cmd,
        PowerShell
    }

    internal enum CopyMode
    {
        Paragraphs,
        SingleLine
    }

    internal sealed class Options
    {
        public bool FormatOnly;
        public bool InputSelfTest;
        public bool NormalizeCopy;
        public bool ShowHelp;
        public int TargetProcessId;
        public long TargetWindow;
        public CopyMode Copy = CopyMode.Paragraphs;
        public ShellMode Shell = ShellMode.Auto;
        public readonly List<string> Paths = new List<string>();

        public static bool TryParse(string[] args, out Options options, out string error)
        {
            options = new Options();
            error = null;
            bool readingPaths = false;

            for (int index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                if (readingPaths)
                {
                    options.Paths.Add(argument);
                    continue;
                }

                if (argument == "--")
                {
                    readingPaths = true;
                }
                else if (argument == "--format-only")
                {
                    options.FormatOnly = true;
                }
                else if (argument == "--input-self-test")
                {
                    options.InputSelfTest = true;
                }
                else if (argument == "--normalize-copy")
                {
                    if (++index >= args.Length || !TryParseCopyMode(args[index], out options.Copy))
                    {
                        error = "--normalize-copy must be paragraphs or single-line.";
                        return false;
                    }
                    options.NormalizeCopy = true;
                }
                else if (argument == "--help" || argument == "-h" || argument == "/?")
                {
                    options.ShowHelp = true;
                }
                else if (argument == "--shell")
                {
                    if (++index >= args.Length || !TryParseShell(args[index], out options.Shell))
                    {
                        error = "--shell must be auto, cmd, or powershell.";
                        return false;
                    }
                }
                else if (argument == "--target-hwnd")
                {
                    if (++index >= args.Length || !long.TryParse(args[index], out options.TargetWindow) || options.TargetWindow <= 0)
                    {
                        error = "--target-hwnd must be a positive window handle.";
                        return false;
                    }
                }
                else if (argument == "--target-pid")
                {
                    if (++index >= args.Length || !int.TryParse(args[index], out options.TargetProcessId) || options.TargetProcessId <= 0)
                    {
                        error = "--target-pid must be a positive process ID.";
                        return false;
                    }
                }
                else
                {
                    error = "Unknown argument: " + argument;
                    return false;
                }
            }

            if (options.FormatOnly && options.Paths.Count == 0)
            {
                error = "--format-only requires at least one path after --.";
                return false;
            }

            if (options.NormalizeCopy && (options.FormatOnly || options.InputSelfTest || options.Paths.Count > 0 ||
                options.TargetWindow != 0 || options.TargetProcessId != 0))
            {
                error = "--normalize-copy cannot be combined with another operation or path arguments.";
                return false;
            }

            if ((options.TargetWindow == 0) != (options.TargetProcessId == 0))
            {
                error = "--target-hwnd and --target-pid must be provided together.";
                return false;
            }

            return true;
        }

        private static bool TryParseShell(string value, out ShellMode shell)
        {
            if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
            {
                shell = ShellMode.Auto;
                return true;
            }
            if (string.Equals(value, "cmd", StringComparison.OrdinalIgnoreCase))
            {
                shell = ShellMode.Cmd;
                return true;
            }
            if (string.Equals(value, "powershell", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "pwsh", StringComparison.OrdinalIgnoreCase))
            {
                shell = ShellMode.PowerShell;
                return true;
            }

            shell = ShellMode.Auto;
            return false;
        }

        private static bool TryParseCopyMode(string value, out CopyMode mode)
        {
            if (string.Equals(value, "paragraphs", StringComparison.OrdinalIgnoreCase))
            {
                mode = CopyMode.Paragraphs;
                return true;
            }
            if (string.Equals(value, "single-line", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "singleline", StringComparison.OrdinalIgnoreCase))
            {
                mode = CopyMode.SingleLine;
                return true;
            }

            mode = CopyMode.Paragraphs;
            return false;
        }
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Options options;
            string parseError;
            if (!Options.TryParse(args, out options, out parseError))
            {
                Console.Error.WriteLine(parseError);
                PrintHelp();
                return 2;
            }

            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            if (options.NormalizeCopy)
            {
                return NormalizeCopiedText(options.Copy);
            }

            ShellMode shell = options.Shell == ShellMode.Auto ? ShellDetector.Detect() : options.Shell;

            if (options.FormatOnly)
            {
                try
                {
                    WriteUtf8Line(PathFormatter.Format(options.Paths, shell));
                    return 0;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(exception.Message);
                    return 1;
                }
            }

            if (options.InputSelfTest)
            {
                return InputSelfTest.Run();
            }

            if (options.TargetWindow == 0)
            {
                return LaunchDetached(shell);
            }

            IntPtr targetWindow = new IntPtr(options.TargetWindow);
            if (!NativeMethods.IsWindow(targetWindow))
            {
                MessageBox.Show(
                    "The terminal window is no longer available. Start Terminal Drop Path from the target CMD or PowerShell window again.",
                    "Terminal Drop Path",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 1;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new DropPathForm(targetWindow, options.TargetProcessId, shell));
            return 0;
        }

        private static int LaunchDetached(ShellMode shell)
        {
            IntPtr consoleWindow = NativeMethods.GetConsoleWindow();
            if (consoleWindow == IntPtr.Zero || !NativeMethods.IsWindowVisible(consoleWindow))
            {
                MessageBox.Show(
                    "Start this tool from a classic CMD or PowerShell console. Windows Terminal already supports dropping paths directly into its input line.",
                    "Terminal Drop Path",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return 1;
            }

            int targetProcessId = ShellDetector.FindConsoleProcessId();
            if (targetProcessId <= 0)
            {
                Console.Error.WriteLine("Unable to identify the CMD or PowerShell process attached to this console.");
                return 1;
            }

            string shellArgument = shell == ShellMode.Cmd ? "cmd" : "powershell";
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Application.ExecutablePath;
            startInfo.Arguments = "--target-hwnd " + consoleWindow.ToInt64() +
                " --target-pid " + targetProcessId + " --shell " + shellArgument;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;

            try
            {
                Process.Start(startInfo);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Unable to start the drop window: " + exception.Message);
                return 1;
            }
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Terminal Drop Path 0.2.1");
            Console.WriteLine("Usage:");
            Console.WriteLine("  TerminalDropPath.exe [--shell auto|cmd|powershell]");
            Console.WriteLine("  TerminalDropPath.exe --format-only --shell cmd -- <path> [path...]");
            Console.WriteLine("  TerminalDropPath.exe --normalize-copy paragraphs|single-line < input.txt");
        }

        private static int NormalizeCopiedText(CopyMode mode)
        {
            try
            {
                string input;
                using (StreamReader reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)))
                {
                    input = reader.ReadToEnd();
                }

                using (StreamWriter writer = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)))
                {
                    writer.Write(CopiedTextFormatter.Format(input, mode));
                }
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }

        private static void WriteUtf8Line(string text)
        {
            if (Console.IsOutputRedirected)
            {
                using (StreamWriter writer = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)))
                {
                    writer.WriteLine(text);
                }
                return;
            }

            Encoding originalEncoding = Console.OutputEncoding;
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
                Console.WriteLine(text);
            }
            finally
            {
                Console.OutputEncoding = originalEncoding;
            }
        }
    }

    internal static class PathFormatter
    {
        public static string Format(IEnumerable<string> paths, ShellMode shell)
        {
            if (paths == null)
            {
                throw new ArgumentNullException("paths");
            }

            List<string> formatted = new List<string>();
            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                string absolutePath = Path.GetFullPath(path);
                formatted.Add(FormatOne(absolutePath, shell));
            }

            if (formatted.Count == 0)
            {
                throw new ArgumentException("No valid file or folder paths were provided.", "paths");
            }

            return string.Join(" ", formatted.ToArray());
        }

        private static string FormatOne(string path, ShellMode shell)
        {
            if (shell == ShellMode.Cmd && (path.IndexOf('%') >= 0 || path.IndexOf('!') >= 0))
            {
                throw new ArgumentException("CMD cannot safely preserve % or ! in a path; use PowerShell mode.", "paths");
            }

            if (!NeedsQuoting(path))
            {
                return path;
            }

            if (shell == ShellMode.PowerShell)
            {
                return "'" + path.Replace("'", "''") + "'";
            }

            return "\"" + path + "\"";
        }

        private static bool NeedsQuoting(string path)
        {
            foreach (char character in path)
            {
                if (char.IsLetterOrDigit(character) ||
                    character == '\\' || character == '/' || character == ':' ||
                    character == '.' || character == '_' || character == '-')
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }

    internal static class ShellDetector
    {
        private const int MaximumParentDepth = 8;

        public static ShellMode Detect()
        {
            int processId = Process.GetCurrentProcess().Id;
            for (int depth = 0; depth < MaximumParentDepth; depth++)
            {
                processId = NativeMethods.GetParentProcessId(processId);
                if (processId <= 0)
                {
                    break;
                }

                try
                {
                    string name = Process.GetProcessById(processId).ProcessName;
                    if (string.Equals(name, "cmd", StringComparison.OrdinalIgnoreCase))
                    {
                        return ShellMode.Cmd;
                    }
                    if (string.Equals(name, "powershell", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "pwsh", StringComparison.OrdinalIgnoreCase))
                    {
                        return ShellMode.PowerShell;
                    }
                }
                catch (ArgumentException)
                {
                    break;
                }
            }

            return ShellMode.PowerShell;
        }

        public static int FindConsoleProcessId()
        {
            int currentProcessId = Process.GetCurrentProcess().Id;
            uint[] processIds = new uint[64];
            uint processCount = NativeMethods.GetConsoleProcessList(processIds, (uint)processIds.Length);
            if (processCount > processIds.Length)
            {
                if (processCount > int.MaxValue)
                {
                    return 0;
                }
                processIds = new uint[(int)processCount];
                processCount = NativeMethods.GetConsoleProcessList(processIds, (uint)processIds.Length);
            }

            HashSet<uint> attachedProcessIds = new HashSet<uint>();
            int usableCount = (int)Math.Min(processCount, (uint)processIds.Length);
            for (int index = 0; index < usableCount; index++)
            {
                attachedProcessIds.Add(processIds[index]);
            }

            int processId = currentProcessId;
            int shellProcessId = 0;
            for (int depth = 0; depth < MaximumParentDepth; depth++)
            {
                processId = NativeMethods.GetParentProcessId(processId);
                if (processId <= 0)
                {
                    break;
                }

                try
                {
                    string name = Process.GetProcessById(processId).ProcessName;
                    if (attachedProcessIds.Contains((uint)processId) &&
                        (string.Equals(name, "cmd", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(name, "powershell", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(name, "pwsh", StringComparison.OrdinalIgnoreCase)))
                    {
                        shellProcessId = processId;
                    }
                }
                catch (ArgumentException)
                {
                    break;
                }
            }

            if (shellProcessId > 0)
            {
                return shellProcessId;
            }

            for (int index = 0; index < usableCount; index++)
            {
                if (processIds[index] != 0 && processIds[index] != (uint)currentProcessId)
                {
                    return (int)processIds[index];
                }
            }

            return 0;
        }
    }

    internal static class CopiedTextFormatter
    {
        private const string WindowsNewLine = "\r\n";

        public static string Format(string text, CopyMode mode)
        {
            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (mode == CopyMode.SingleLine)
            {
                return normalized.Replace("\n", string.Empty);
            }

            string[] lines = normalized.Split(new[] { '\n' }, StringSplitOptions.None);
            StringBuilder result = new StringBuilder(text.Length);
            for (int index = 0; index < lines.Length; index++)
            {
                string current = lines[index];
                result.Append(current);
                if (index == lines.Length - 1)
                {
                    continue;
                }

                string next = lines[index + 1];
                if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(next))
                {
                    result.Append(WindowsNewLine);
                }
                else
                {
                    result.Append(GetParagraphJoiner(current, next));
                }
            }

            return result.ToString();
        }

        public static int CountLineBreaks(string text)
        {
            int count = 0;
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] == '\r')
                {
                    count++;
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }
                }
                else if (text[index] == '\n')
                {
                    count++;
                }
            }
            return count;
        }

        private static string GetParagraphJoiner(string current, string next)
        {
            char previousCharacter = current[current.Length - 1];
            char nextCharacter = next[0];
            int previousCodePoint = char.IsLowSurrogate(previousCharacter) && current.Length > 1 && char.IsHighSurrogate(current[current.Length - 2])
                ? char.ConvertToUtf32(current[current.Length - 2], previousCharacter)
                : previousCharacter;
            int nextCodePoint = char.IsHighSurrogate(nextCharacter) && next.Length > 1 && char.IsLowSurrogate(next[1])
                ? char.ConvertToUtf32(nextCharacter, next[1])
                : nextCharacter;
            if (char.IsWhiteSpace(previousCharacter) || char.IsWhiteSpace(nextCharacter) ||
                IsCjk(previousCodePoint) || IsCjk(nextCodePoint) ||
                IsNoSpaceAfter(previousCharacter) || IsNoSpaceBefore(nextCharacter))
            {
                return string.Empty;
            }

            return " ";
        }

        private static bool IsCjk(int codePoint)
        {
            return (codePoint >= 0x2E80 && codePoint <= 0x9FFF) ||
                   (codePoint >= 0xAC00 && codePoint <= 0xD7AF) ||
                   (codePoint >= 0xF900 && codePoint <= 0xFAFF) ||
                   (codePoint >= 0xFF00 && codePoint <= 0xFFEF) ||
                   (codePoint >= 0x20000 && codePoint <= 0x2FA1F);
        }

        private static bool IsNoSpaceBefore(char character)
        {
            return ",.;:!?%)]}>/\\'\"-".IndexOf(character) >= 0;
        }

        private static bool IsNoSpaceAfter(char character)
        {
            return "([{</\\-".IndexOf(character) >= 0;
        }
    }

    internal static class ClipboardTextCleaner
    {
        public static bool TryClean(
            IntPtr newOwnerWindow,
            IntPtr expectedOwner,
            uint copiedSequence,
            string copiedText,
            CopyMode mode,
            out int mergedLineBreaks,
            out string error)
        {
            return TryClean(
                newOwnerWindow,
                expectedOwner,
                copiedSequence,
                copiedText,
                mode,
                null,
                out mergedLineBreaks,
                out error);
        }

        public static bool TryClean(
            IntPtr newOwnerWindow,
            IntPtr expectedOwner,
            uint copiedSequence,
            string copiedText,
            CopyMode mode,
            Func<bool> isExpectedOwnerValid,
            out int mergedLineBreaks,
            out string error)
        {
            string formatted = CopiedTextFormatter.Format(copiedText, mode);
            mergedLineBreaks = CopiedTextFormatter.CountLineBreaks(copiedText) -
                CopiedTextFormatter.CountLineBreaks(formatted);
            if (string.Equals(formatted, copiedText, StringComparison.Ordinal))
            {
                if ((isExpectedOwnerValid != null && !isExpectedOwnerValid()) ||
                    !ClipboardAccess.IsExpectedState(copiedSequence, expectedOwner))
                {
                    error = "The clipboard changed before the terminal copy could be confirmed";
                    return false;
                }
                error = null;
                return true;
            }

            return ClipboardAccess.TryReplaceText(
                newOwnerWindow,
                formatted,
                copiedSequence,
                expectedOwner,
                isExpectedOwnerValid,
                out error);
        }
    }

    internal static class ClipboardUpdatePolicy
    {
        public static bool ShouldQueue(
            bool autoCleanEnabled,
            bool targetIdentityValid,
            IntPtr targetWindow,
            IntPtr clipboardOwner,
            uint clipboardSequence)
        {
            return autoCleanEnabled &&
                targetIdentityValid &&
                targetWindow != IntPtr.Zero &&
                clipboardOwner == targetWindow &&
                clipboardSequence != 0;
        }
    }

    internal sealed class DropPathForm : Form
    {
        private const int ClipboardUpdateMessage = 0x031D;
        private const int ClipboardUpdateDebounceMilliseconds = 40;
        private const uint ProcessSynchronize = 0x00100000;
        private const uint WaitTimeout = 0x00000102;
        private readonly IntPtr _targetWindow;
        private readonly CheckBox _autoCleanCheckBox;
        private readonly System.Windows.Forms.Timer _clipboardUpdateTimer;
        private readonly ComboBox _copyModeSelector;
        private readonly ToolTip _copyToolTip;
        private readonly Label _statusLabel;
        private readonly ComboBox _shellSelector;
        private readonly int _targetProcessId;
        private readonly uint _targetWindowProcessId;
        private bool _autoCleanEnabled;
        private bool _clipboardListenerRegistered;
        private CopyMode _copyMode;
        private uint _pendingClipboardSequence;
        private ShellMode _shell;
        private IntPtr _targetWindowProcessHandle;

        public DropPathForm(IntPtr targetWindow, int targetProcessId, ShellMode shell)
        {
            _targetWindow = targetWindow;
            _targetProcessId = targetProcessId;
            uint targetWindowProcessId;
            NativeMethods.GetWindowThreadProcessId(targetWindow, out targetWindowProcessId);
            _targetWindowProcessId = targetWindowProcessId;
            _targetWindowProcessHandle = targetWindowProcessId == 0
                ? IntPtr.Zero
                : NativeMethods.OpenProcess(ProcessSynchronize, false, targetWindowProcessId);
            _shell = shell;
            _copyMode = CopyMode.Paragraphs;
            _autoCleanEnabled = true;

            Text = "Terminal Drop Path";
            ClientSize = new Size(340, 250);
            MinimumSize = Size;
            MaximumSize = Size;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            TopMost = true;
            AllowDrop = true;
            BackColor = Color.FromArgb(246, 247, 249);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            StartPosition = FormStartPosition.Manual;

            Label titleLabel = new Label();
            titleLabel.AutoSize = false;
            titleLabel.Location = new Point(18, 16);
            titleLabel.Size = new Size(304, 28);
            titleLabel.Text = "Drop paths or copy clean text";
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            titleLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
            Controls.Add(titleLabel);

            Label shellLabel = new Label();
            shellLabel.AutoSize = true;
            shellLabel.Location = new Point(18, 57);
            shellLabel.Text = "Target shell";
            Controls.Add(shellLabel);

            _shellSelector = new ComboBox();
            _shellSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _shellSelector.Location = new Point(103, 53);
            _shellSelector.Size = new Size(116, 25);
            _shellSelector.Items.Add("CMD");
            _shellSelector.Items.Add("PowerShell");
            _shellSelector.SelectedIndex = shell == ShellMode.Cmd ? 0 : 1;
            _shellSelector.SelectedIndexChanged += ShellSelectorChanged;
            Controls.Add(_shellSelector);

            CheckBox topMostCheckBox = new CheckBox();
            topMostCheckBox.AutoSize = true;
            topMostCheckBox.Location = new Point(231, 56);
            topMostCheckBox.Text = "On top";
            topMostCheckBox.Checked = true;
            topMostCheckBox.CheckedChanged += delegate(object sender, EventArgs args)
            {
                TopMost = topMostCheckBox.Checked;
            };
            Controls.Add(topMostCheckBox);

            Label copyModeLabel = new Label();
            copyModeLabel.AutoSize = true;
            copyModeLabel.Location = new Point(18, 94);
            copyModeLabel.Text = "Copy mode";
            Controls.Add(copyModeLabel);

            _copyModeSelector = new ComboBox();
            _copyModeSelector.DropDownStyle = ComboBoxStyle.DropDownList;
            _copyModeSelector.Location = new Point(103, 90);
            _copyModeSelector.Size = new Size(219, 25);
            _copyModeSelector.Items.Add("Keep paragraphs");
            _copyModeSelector.Items.Add("One line");
            _copyModeSelector.SelectedIndex = 0;
            _copyModeSelector.SelectedIndexChanged += CopyModeSelectorChanged;
            Controls.Add(_copyModeSelector);

            _autoCleanCheckBox = new CheckBox();
            _autoCleanCheckBox.AutoSize = true;
            _autoCleanCheckBox.Location = new Point(18, 128);
            _autoCleanCheckBox.Text = "Auto-clean terminal copies";
            _autoCleanCheckBox.Checked = true;
            _autoCleanCheckBox.CheckedChanged += AutoCleanCheckBoxChanged;
            Controls.Add(_autoCleanCheckBox);

            Button copyButton = new Button();
            copyButton.Location = new Point(18, 157);
            copyButton.Size = new Size(304, 30);
            copyButton.Text = "Copy selection now";
            copyButton.UseVisualStyleBackColor = true;
            copyButton.Click += OnCopySelection;
            Controls.Add(copyButton);

            _copyToolTip = new ToolTip();
            _copyToolTip.SetToolTip(copyButton, "Manual fallback for copying the current target-terminal selection.");
            _copyToolTip.SetToolTip(
                _autoCleanCheckBox,
                "Cleans right-click and Ctrl+C copies from the target terminal only.");

            _clipboardUpdateTimer = new System.Windows.Forms.Timer();
            _clipboardUpdateTimer.Interval = ClipboardUpdateDebounceMilliseconds;
            _clipboardUpdateTimer.Tick += OnClipboardUpdateTimerTick;

            _statusLabel = new Label();
            _statusLabel.AutoEllipsis = true;
            _statusLabel.Location = new Point(18, 208);
            _statusLabel.Size = new Size(304, 24);
            _statusLabel.Text = "Starting auto-clean...";
            _statusLabel.ForeColor = Color.FromArgb(74, 85, 104);
            Controls.Add(_statusLabel);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            Shown += OnFormShown;
            FormClosed += delegate(object sender, FormClosedEventArgs args)
            {
                if (_clipboardListenerRegistered)
                {
                    NativeMethods.RemoveClipboardFormatListener(Handle);
                    _clipboardListenerRegistered = false;
                }
                _clipboardUpdateTimer.Stop();
                _clipboardUpdateTimer.Tick -= OnClipboardUpdateTimerTick;
                _clipboardUpdateTimer.Dispose();
                if (_targetWindowProcessHandle != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(_targetWindowProcessHandle);
                    _targetWindowProcessHandle = IntPtr.Zero;
                }
                Shown -= OnFormShown;
                _shellSelector.SelectedIndexChanged -= ShellSelectorChanged;
                _copyModeSelector.SelectedIndexChanged -= CopyModeSelectorChanged;
                _autoCleanCheckBox.CheckedChanged -= AutoCleanCheckBoxChanged;
                copyButton.Click -= OnCopySelection;
                _copyToolTip.Dispose();
            };

            PositionNearTarget();
        }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);

            if (message.Msg != ClipboardUpdateMessage)
            {
                return;
            }

            uint sequence = NativeMethods.GetClipboardSequenceNumber();
            bool targetIdentityValid = IsOriginalTargetWindow();
            if (!targetIdentityValid)
            {
                DisableAutoClean("Target terminal closed; auto-clean stopped");
                return;
            }
            if (!ClipboardUpdatePolicy.ShouldQueue(
                    _autoCleanEnabled,
                    targetIdentityValid,
                    _targetWindow,
                    NativeMethods.GetClipboardOwner(),
                    sequence))
            {
                return;
            }

            _pendingClipboardSequence = sequence;
            _clipboardUpdateTimer.Stop();
            _clipboardUpdateTimer.Start();
        }

        private void OnFormShown(object sender, EventArgs args)
        {
            if (!IsOriginalTargetWindow())
            {
                DisableAutoClean("Could not track the target terminal; auto-clean disabled");
                return;
            }

            if (!NativeMethods.AddClipboardFormatListener(Handle))
            {
                DisableAutoClean("Auto-clean unavailable (Windows error " + Marshal.GetLastWin32Error() + ")");
                return;
            }

            _clipboardListenerRegistered = true;
            SetStatus("Auto-clean ready", false);
        }

        private void ShellSelectorChanged(object sender, EventArgs args)
        {
            _shell = _shellSelector.SelectedIndex == 0 ? ShellMode.Cmd : ShellMode.PowerShell;
        }

        private void CopyModeSelectorChanged(object sender, EventArgs args)
        {
            _copyMode = _copyModeSelector.SelectedIndex == 1 ? CopyMode.SingleLine : CopyMode.Paragraphs;
        }

        private void AutoCleanCheckBoxChanged(object sender, EventArgs args)
        {
            _autoCleanEnabled = _autoCleanCheckBox.Checked;
            if (!_autoCleanEnabled)
            {
                _clipboardUpdateTimer.Stop();
                _pendingClipboardSequence = 0;
            }
            SetStatus(_autoCleanEnabled ? "Auto-clean enabled" : "Auto-clean paused", false);
        }

        private void OnClipboardUpdateTimerTick(object sender, EventArgs args)
        {
            _clipboardUpdateTimer.Stop();
            uint expectedSequence = _pendingClipboardSequence;
            _pendingClipboardSequence = 0;

            uint currentSequence = NativeMethods.GetClipboardSequenceNumber();
            bool targetIdentityValid = IsOriginalTargetWindow();
            if (!targetIdentityValid)
            {
                DisableAutoClean("Target terminal closed; auto-clean stopped");
                return;
            }
            if (currentSequence != expectedSequence ||
                !ClipboardUpdatePolicy.ShouldQueue(
                    _autoCleanEnabled,
                    targetIdentityValid,
                    _targetWindow,
                    NativeMethods.GetClipboardOwner(),
                    currentSequence))
            {
                return;
            }

            string copiedText;
            string error;
            if (!ClipboardAccess.TryReadOwnedText(expectedSequence, _targetWindow, out copiedText, out error))
            {
                if (NativeMethods.GetClipboardSequenceNumber() == expectedSequence &&
                    NativeMethods.GetClipboardOwner() == _targetWindow)
                {
                    SetStatus(error, true);
                }
                return;
            }

            int mergedLineBreaks;
            if (!ClipboardTextCleaner.TryClean(
                    Handle,
                    _targetWindow,
                    expectedSequence,
                    copiedText,
                    _copyMode,
                    IsOriginalTargetWindow,
                    out mergedLineBreaks,
                    out error))
            {
                SetStatus(error, true);
                return;
            }

            SetCopyStatus("Terminal copy cleaned", mergedLineBreaks);
        }

        private bool IsOriginalTargetWindow()
        {
            if (_targetWindowProcessHandle == IntPtr.Zero ||
                NativeMethods.WaitForSingleObject(_targetWindowProcessHandle, 0) != WaitTimeout ||
                !NativeMethods.IsWindow(_targetWindow))
            {
                return false;
            }

            uint currentProcessId;
            NativeMethods.GetWindowThreadProcessId(_targetWindow, out currentProcessId);
            return currentProcessId != 0 && currentProcessId == _targetWindowProcessId;
        }

        private void DisableAutoClean(string message)
        {
            if (_clipboardListenerRegistered)
            {
                NativeMethods.RemoveClipboardFormatListener(Handle);
                _clipboardListenerRegistered = false;
            }
            _clipboardUpdateTimer.Stop();
            _pendingClipboardSequence = 0;
            _autoCleanEnabled = false;
            _autoCleanCheckBox.CheckedChanged -= AutoCleanCheckBoxChanged;
            _autoCleanCheckBox.Checked = false;
            _autoCleanCheckBox.Enabled = false;
            _autoCleanCheckBox.CheckedChanged += AutoCleanCheckBoxChanged;
            SetStatus(message, true);
        }

        private void OnCopySelection(object sender, EventArgs args)
        {
            SetStatus("Copying terminal selection...", false);

            int mergedLineBreaks;
            string error;
            if (!ConsoleSelectionCopier.TryCopy(Handle, _targetWindow, _targetProcessId, _copyMode, out mergedLineBreaks, out error))
            {
                SetStatus(error, true);
                return;
            }

            SetCopyStatus("Copied", mergedLineBreaks);
        }

        private void OnDragEnter(object sender, DragEventArgs args)
        {
            args.Effect = args.Data != null && args.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void OnDragDrop(object sender, DragEventArgs args)
        {
            string[] paths = args.Data == null ? null : args.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null || paths.Length == 0)
            {
                SetStatus("No file path received", true);
                return;
            }

            string text;
            try
            {
                text = PathFormatter.Format(paths, _shell);
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, true);
                return;
            }

            SetStatus("Sending " + paths.Length + (paths.Length == 1 ? " path" : " paths"), false);
            string error;
            if (!KeyboardInjector.TrySend(_targetWindow, text, out error))
            {
                SetStatus(error, true);
                return;
            }

            SetStatus("Inserted " + paths.Length + (paths.Length == 1 ? " path" : " paths"), false);
        }

        private void SetStatus(string text, bool isError)
        {
            _statusLabel.Text = text;
            _statusLabel.ForeColor = isError
                ? Color.FromArgb(176, 42, 55)
                : Color.FromArgb(49, 103, 72);
        }

        private void SetCopyStatus(string prefix, int mergedLineBreaks)
        {
            if (mergedLineBreaks == 0)
            {
                SetStatus(prefix + "; line breaks unchanged", false);
            }
            else
            {
                SetStatus(
                    prefix + "; merged " + mergedLineBreaks +
                    (mergedLineBreaks == 1 ? " line break" : " line breaks"),
                    false);
            }
        }

        private void PositionNearTarget()
        {
            NativeMethods.RECT rectangle;
            if (!NativeMethods.GetWindowRect(_targetWindow, out rectangle))
            {
                StartPosition = FormStartPosition.CenterScreen;
                return;
            }

            Rectangle workingArea = Screen.FromHandle(_targetWindow).WorkingArea;
            int x = Math.Min(rectangle.Right - Width, workingArea.Right - Width - 12);
            int y = Math.Min(rectangle.Top + 36, workingArea.Bottom - Height - 12);
            x = Math.Max(x, workingArea.Left + 12);
            y = Math.Max(y, workingArea.Top + 12);
            Location = new Point(x, y);
        }
    }

    internal static class ConsoleSelectionCopier
    {
        // Classic conhost routes this WM_COMMAND ID through its native wrap-aware copy path.
        private const uint CommandCopy = 0xFFF0;
        private const uint MessageCommand = 0x0111;
        private const uint SelectionNotEmpty = 0x0002;
        private const uint SendMessageAbortIfHung = 0x0002;
        private static readonly TimeSpan ClipboardTimeout = TimeSpan.FromSeconds(2);

        public static bool TryCopy(IntPtr clipboardOwnerWindow, IntPtr targetWindow, int targetProcessId, CopyMode mode, out int mergedLineBreaks, out string error)
        {
            mergedLineBreaks = 0;
            error = null;

            if (!KeyboardInjector.TryActivateTarget(targetWindow, out error))
            {
                return false;
            }

            Thread.Sleep(100);

            NativeMethods.FreeConsole();
            if (!NativeMethods.AttachConsole((uint)targetProcessId))
            {
                error = "Could not attach to the target terminal (Windows error " + Marshal.GetLastWin32Error() + ")";
                return false;
            }

            try
            {
                if (NativeMethods.GetConsoleWindow() != targetWindow)
                {
                    error = "The target terminal process no longer belongs to the original console";
                    return false;
                }

                NativeMethods.CONSOLE_SELECTION_INFO selection;
                if (!NativeMethods.GetConsoleSelectionInfo(out selection))
                {
                    error = "Could not read the terminal selection (Windows error " + Marshal.GetLastWin32Error() + ")";
                    return false;
                }
                if ((selection.flags & SelectionNotEmpty) == 0)
                {
                    error = "Select text in the target terminal first";
                    return false;
                }

                uint previousSequence = NativeMethods.GetClipboardSequenceNumber();
                UIntPtr messageResult;
                if (NativeMethods.SendMessageTimeout(
                        targetWindow,
                        MessageCommand,
                        new UIntPtr(CommandCopy),
                        IntPtr.Zero,
                        SendMessageAbortIfHung,
                        1000,
                        out messageResult) == IntPtr.Zero)
                {
                    error = "The terminal did not accept the copy command (Windows error " + Marshal.GetLastWin32Error() + ")";
                    return false;
                }

                string copiedText;
                uint copiedSequence;
                if (!ClipboardAccess.TryWaitForText(previousSequence, targetWindow, ClipboardTimeout, out copiedText, out copiedSequence, out error))
                {
                    return false;
                }

                if (!ClipboardTextCleaner.TryClean(
                        clipboardOwnerWindow,
                        targetWindow,
                        copiedSequence,
                        copiedText,
                        mode,
                        out mergedLineBreaks,
                        out error))
                {
                    return false;
                }

                return true;
            }
            finally
            {
                NativeMethods.FreeConsole();
            }
        }
    }

    internal static class ClipboardAccess
    {
        private const uint ClipboardUnicodeText = 13;
        private const uint GlobalMemoryMoveable = 0x0002;
        private const int RetryCount = 8;
        private const int RetryDelayMilliseconds = 25;

        public static bool TryWaitForText(
            uint previousSequence,
            IntPtr expectedOwner,
            TimeSpan timeout,
            out string text,
            out uint copiedSequence,
            out string error)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            do
            {
                uint observedSequence = NativeMethods.GetClipboardSequenceNumber();
                if (observedSequence != previousSequence &&
                    TryReadOwnedText(observedSequence, expectedOwner, out text, out error))
                {
                    copiedSequence = observedSequence;
                    return true;
                }
                Thread.Sleep(20);
            }
            while (DateTime.UtcNow < deadline);

            text = null;
            copiedSequence = 0;
            error = "The terminal did not place text on the clipboard";
            return false;
        }

        public static bool TryReadOwnedText(
            uint expectedSequence,
            IntPtr expectedOwner,
            out string text,
            out string error)
        {
            text = null;
            if (NativeMethods.GetClipboardSequenceNumber() != expectedSequence ||
                NativeMethods.GetClipboardOwner() != expectedOwner)
            {
                error = "The clipboard changed before the terminal copy could be read";
                return false;
            }

            if (!TryGetText(out text, out error))
            {
                return false;
            }

            if (NativeMethods.GetClipboardSequenceNumber() != expectedSequence ||
                NativeMethods.GetClipboardOwner() != expectedOwner)
            {
                text = null;
                error = "The clipboard changed while the terminal copy was being read";
                return false;
            }

            return true;
        }

        public static bool IsExpectedState(uint expectedSequence, IntPtr expectedOwner)
        {
            return NativeMethods.GetClipboardSequenceNumber() == expectedSequence &&
                NativeMethods.GetClipboardOwner() == expectedOwner;
        }

        public static bool TryReplaceText(
            IntPtr newOwnerWindow,
            string text,
            uint expectedSequence,
            IntPtr expectedOwner,
            Func<bool> isExpectedOwnerValid,
            out string error)
        {
            IntPtr globalMemory = IntPtr.Zero;
            bool clipboardOwnsMemory = false;
            try
            {
                try
                {
                    int byteCount = checked((text.Length + 1) * sizeof(char));
                    globalMemory = NativeMethods.GlobalAlloc(GlobalMemoryMoveable, new UIntPtr((uint)byteCount));
                }
                catch (OverflowException)
                {
                    error = "The selected text is too large to place on the clipboard";
                    return false;
                }

                if (globalMemory == IntPtr.Zero)
                {
                    error = "Could not allocate clipboard memory (Windows error " + Marshal.GetLastWin32Error() + ")";
                    return false;
                }

                IntPtr destination = NativeMethods.GlobalLock(globalMemory);
                if (destination == IntPtr.Zero)
                {
                    error = "Could not prepare clipboard text (Windows error " + Marshal.GetLastWin32Error() + ")";
                    return false;
                }

                try
                {
                    if (text.Length > 0)
                    {
                        Marshal.Copy(text.ToCharArray(), 0, destination, text.Length);
                    }
                    Marshal.WriteInt16(destination, text.Length * sizeof(char), 0);
                }
                finally
                {
                    NativeMethods.GlobalUnlock(globalMemory);
                }

                for (int attempt = 0; attempt < RetryCount; attempt++)
                {
                    if (!NativeMethods.OpenClipboard(newOwnerWindow))
                    {
                        Thread.Sleep(RetryDelayMilliseconds);
                        continue;
                    }

                    try
                    {
                        if ((isExpectedOwnerValid != null && !isExpectedOwnerValid()) ||
                            NativeMethods.GetClipboardSequenceNumber() != expectedSequence ||
                            NativeMethods.GetClipboardOwner() != expectedOwner)
                        {
                            error = "The clipboard changed before the cleaned text could be written; copy again";
                            return false;
                        }
                        if (!NativeMethods.EmptyClipboard())
                        {
                            error = "Could not clear the clipboard (Windows error " + Marshal.GetLastWin32Error() + ")";
                            return false;
                        }
                        if (NativeMethods.SetClipboardData(ClipboardUnicodeText, globalMemory) == IntPtr.Zero)
                        {
                            error = "Could not place the cleaned text on the clipboard (Windows error " + Marshal.GetLastWin32Error() + ")";
                            return false;
                        }

                        clipboardOwnsMemory = true;
                        error = null;
                        return true;
                    }
                    finally
                    {
                        NativeMethods.CloseClipboard();
                    }
                }

                error = "The clipboard is busy; try the copy action again";
                return false;
            }
            finally
            {
                if (globalMemory != IntPtr.Zero && !clipboardOwnsMemory)
                {
                    NativeMethods.GlobalFree(globalMemory);
                }
            }
        }

        private static bool TryGetText(out string text, out string error)
        {
            for (int attempt = 0; attempt < RetryCount; attempt++)
            {
                try
                {
                    if (!Clipboard.ContainsText(TextDataFormat.UnicodeText))
                    {
                        text = null;
                        error = "The terminal selection did not contain text";
                        return false;
                    }

                    text = Clipboard.GetText(TextDataFormat.UnicodeText);
                    error = null;
                    return true;
                }
                catch (ExternalException exception)
                {
                    text = null;
                    error = exception.Message;
                    Thread.Sleep(RetryDelayMilliseconds);
                }
            }

            text = null;
            error = "The clipboard is busy; try the copy action again";
            return false;
        }
    }

    internal static class KeyboardInjector
    {
        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;
        private const uint KeyEventUnicode = 0x0004;
        private const int ShowRestore = 9;

        public static bool TrySend(IntPtr targetWindow, string text, out string error)
        {
            error = null;
            if (!NativeMethods.IsWindow(targetWindow))
            {
                error = "Target terminal was closed";
                return false;
            }

            if (!TryActivate(targetWindow))
            {
                error = "Could not focus the target terminal";
                return false;
            }

            Thread.Sleep(150);
            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[text.Length * 2];
            int inputIndex = 0;
            foreach (char character in text)
            {
                inputs[inputIndex++] = CreateUnicodeInput(character, false);
                inputs[inputIndex++] = CreateUnicodeInput(character, true);
            }

            if (NativeMethods.GetForegroundWindow() != targetWindow)
            {
                error = "Target terminal lost focus before input could be sent";
                return false;
            }

            uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            if (sent != inputs.Length)
            {
                error = "Windows accepted only " + sent + " of " + inputs.Length + " input events";
                return false;
            }

            return true;
        }

        public static bool TryActivateTarget(IntPtr targetWindow, out string error)
        {
            error = null;
            if (!NativeMethods.IsWindow(targetWindow))
            {
                error = "Target terminal was closed";
                return false;
            }
            if (!TryActivate(targetWindow))
            {
                error = "Could not focus the target terminal";
                return false;
            }
            return true;
        }

        private static bool TryActivate(IntPtr targetWindow)
        {
            NativeMethods.ShowWindowAsync(targetWindow, ShowRestore);
            NativeMethods.SetForegroundWindow(targetWindow);
            if (WaitForForeground(targetWindow))
            {
                return true;
            }

            uint currentThread = NativeMethods.GetCurrentThreadId();
            IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();
            uint foregroundThread = foregroundWindow == IntPtr.Zero
                ? 0
                : NativeMethods.GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
            uint targetThread = NativeMethods.GetWindowThreadProcessId(targetWindow, IntPtr.Zero);
            bool attachedForeground = false;
            bool attachedTarget = false;

            try
            {
                if (foregroundThread != 0 && foregroundThread != currentThread)
                {
                    attachedForeground = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
                }
                if (targetThread != 0 && targetThread != currentThread)
                {
                    attachedTarget = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
                }

                NativeMethods.BringWindowToTop(targetWindow);
                NativeMethods.SetForegroundWindow(targetWindow);
            }
            finally
            {
                if (attachedTarget)
                {
                    NativeMethods.AttachThreadInput(currentThread, targetThread, false);
                }
                if (attachedForeground)
                {
                    NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
                }
            }

            return WaitForForeground(targetWindow);
        }

        private static bool WaitForForeground(IntPtr targetWindow)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                if (NativeMethods.GetForegroundWindow() == targetWindow)
                {
                    return true;
                }
                Thread.Sleep(25);
            }

            return false;
        }

        private static NativeMethods.INPUT CreateUnicodeInput(char character, bool keyUp)
        {
            NativeMethods.INPUT input = new NativeMethods.INPUT();
            input.type = InputKeyboard;
            input.union.keyboard = new NativeMethods.KEYBDINPUT();
            input.union.keyboard.virtualKey = 0;
            input.union.keyboard.scanCode = character;
            input.union.keyboard.flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0);
            input.union.keyboard.time = 0;
            input.union.keyboard.extraInfo = UIntPtr.Zero;
            return input;
        }

    }

    internal static class InputSelfTest
    {
        public static int Run()
        {
            Application.EnableVisualStyles();
            using (Form form = new Form())
            using (TextBox textBox = new TextBox())
            {
                string expected = "C:\\Users\\Example\\Desktop\\示例项目-v3.txt";
                form.Text = "Terminal Drop Path Input Test";
                form.ClientSize = new Size(640, 72);
                textBox.Location = new Point(12, 20);
                textBox.Width = 610;
                form.Controls.Add(textBox);
                form.Show();
                form.Activate();
                textBox.Focus();
                Application.DoEvents();
                Thread.Sleep(100);

                string error;
                bool sent = KeyboardInjector.TrySend(form.Handle, expected, out error);
                DateTime deadline = DateTime.UtcNow.AddSeconds(2);
                while (textBox.Text.Length < expected.Length && DateTime.UtcNow < deadline)
                {
                    Application.DoEvents();
                    Thread.Sleep(20);
                }

                if (!sent || textBox.Text != expected)
                {
                    Console.Error.WriteLine(error ?? ("Input mismatch: " + textBox.Text));
                    return 1;
                }

                Console.WriteLine("Unicode input self-test passed.");
                return 0;
            }
        }
    }

    internal static class NativeMethods
    {
        private const uint SnapshotProcesses = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct COORD
        {
            public short x;
            public short y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SMALL_RECT
        {
            public short left;
            public short top;
            public short right;
            public short bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CONSOLE_SELECTION_INFO
        {
            public uint flags;
            public COORD selectionAnchor;
            public SMALL_RECT selection;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT
        {
            public uint type;
            public INPUTUNION union;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mouse;

            [FieldOffset(0)]
            public KEYBDINPUT keyboard;

            [FieldOffset(0)]
            public HARDWAREINPUT hardware;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            public int x;
            public int y;
            public uint mouseData;
            public uint flags;
            public uint time;
            public UIntPtr extraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            public ushort virtualKey;
            public ushort scanCode;
            public uint flags;
            public uint time;
            public UIntPtr extraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HARDWAREINPUT
        {
            public uint message;
            public ushort parameterLow;
            public ushort parameterHigh;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct PROCESSENTRY32
        {
            public uint size;
            public uint usage;
            public uint processId;
            public IntPtr defaultHeapId;
            public uint moduleId;
            public uint threads;
            public uint parentProcessId;
            public int basePriority;
            public uint flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string executableFile;
        }

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AttachConsole(uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint GetConsoleProcessList(uint[] processList, uint processCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetConsoleSelectionInfo(out CONSOLE_SELECTION_INFO selectionInfo);

        [DllImport("user32.dll")]
        internal static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetClipboardOwner();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddClipboardFormatListener(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RemoveClipboardFormatListener(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenClipboard(IntPtr newOwner);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetClipboardData(uint format, IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GlobalLock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GlobalUnlock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr GlobalFree(IntPtr memory);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern IntPtr SendMessageTimeout(
            IntPtr window,
            uint message,
            UIntPtr wParam,
            IntPtr lParam,
            uint flags,
            uint timeout,
            out UIntPtr result);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr window, out RECT rectangle);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AttachThreadInput(uint attachThread, uint attachToThread, bool attach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BringWindowToTop(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindowAsync(IntPtr window, int command);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        internal static int GetParentProcessId(int processId)
        {
            IntPtr snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
            if (snapshot == new IntPtr(-1))
            {
                return 0;
            }

            try
            {
                PROCESSENTRY32 entry = new PROCESSENTRY32();
                entry.size = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
                if (!Process32First(snapshot, ref entry))
                {
                    return 0;
                }

                do
                {
                    if (entry.processId == processId)
                    {
                        return (int)entry.parentProcessId;
                    }
                }
                while (Process32Next(snapshot, ref entry));

                return 0;
            }
            finally
            {
                CloseHandle(snapshot);
            }
        }
    }
}
