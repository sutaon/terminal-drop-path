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
[assembly: System.Reflection.AssemblyDescription("Types dropped file and folder paths into classic Windows terminals.")]
[assembly: System.Reflection.AssemblyCompany("sutaon")]
[assembly: System.Reflection.AssemblyProduct("Terminal Drop Path")]
[assembly: System.Reflection.AssemblyCopyright("Copyright (c) 2026 sutaon")]
[assembly: System.Reflection.AssemblyVersion("0.1.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.1.1.0")]

namespace TerminalDropPath
{
    internal enum ShellMode
    {
        Auto,
        Cmd,
        PowerShell
    }

    internal sealed class Options
    {
        public bool FormatOnly;
        public bool InputSelfTest;
        public bool ShowHelp;
        public long TargetWindow;
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
    }

    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

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

            ShellMode shell = options.Shell == ShellMode.Auto ? ShellDetector.Detect() : options.Shell;

            if (options.FormatOnly)
            {
                try
                {
                    Console.WriteLine(PathFormatter.Format(options.Paths, shell));
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
            Application.Run(new DropPathForm(targetWindow, shell));
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

            string shellArgument = shell == ShellMode.Cmd ? "cmd" : "powershell";
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = Application.ExecutablePath;
            startInfo.Arguments = "--target-hwnd " + consoleWindow.ToInt64() + " --shell " + shellArgument;
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
            Console.WriteLine("Terminal Drop Path 0.1.1");
            Console.WriteLine("Usage:");
            Console.WriteLine("  TerminalDropPath.exe [--shell auto|cmd|powershell]");
            Console.WriteLine("  TerminalDropPath.exe --format-only --shell cmd -- <path> [path...]");
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
        public static ShellMode Detect()
        {
            int processId = Process.GetCurrentProcess().Id;
            for (int depth = 0; depth < 8; depth++)
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
    }

    internal sealed class DropPathForm : Form
    {
        private readonly IntPtr _targetWindow;
        private readonly Label _statusLabel;
        private readonly ComboBox _shellSelector;
        private ShellMode _shell;

        public DropPathForm(IntPtr targetWindow, ShellMode shell)
        {
            _targetWindow = targetWindow;
            _shell = shell;

            Text = "Terminal Drop Path";
            ClientSize = new Size(340, 142);
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
            titleLabel.Text = "Drop files or folders";
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

            _statusLabel = new Label();
            _statusLabel.AutoEllipsis = true;
            _statusLabel.Location = new Point(18, 96);
            _statusLabel.Size = new Size(304, 24);
            _statusLabel.Text = "Ready";
            _statusLabel.ForeColor = Color.FromArgb(74, 85, 104);
            Controls.Add(_statusLabel);

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            FormClosed += delegate(object sender, FormClosedEventArgs args)
            {
                _shellSelector.SelectedIndexChanged -= ShellSelectorChanged;
            };

            PositionNearTarget();
        }

        private void ShellSelectorChanged(object sender, EventArgs args)
        {
            _shell = _shellSelector.SelectedIndex == 0 ? ShellMode.Cmd : ShellMode.PowerShell;
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

            uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(NativeMethods.INPUT)));
            if (sent != inputs.Length)
            {
                error = "Windows accepted only " + sent + " of " + inputs.Length + " input events";
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
        private static extern bool CloseHandle(IntPtr handle);

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
