using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SwapCtrlAlt
{
    static class Program
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_LCONTROL = 0xA2;
        private const int VK_LMENU = 0xA4;

        private const uint LLKHF_EXTENDED = 0x00000001;
        private const uint LLKHF_INJECTED = 0x00000010;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte SCAN_LCTRL = 0x1D;
        private const byte SCAN_LALT = 0x38;

        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static bool _enabled = true;
        private static NotifyIcon _trayIcon;
        private static Icon _iconActive;
        private static Icon _iconPaused;
        private static ToolStripMenuItem _toggleItem;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\SwapLeftCtrlLeftAlt_Singleton_Mutex", out createdNew))
            {
                if (!createdNew)
                {
                    // Second launch acts as an instant toggle-off switch
                    Process current = Process.GetCurrentProcess();
                    foreach (Process p in Process.GetProcessesByName(current.ProcessName))
                    {
                        if (p.Id != current.Id)
                        {
                            try { p.Kill(); } catch { }
                        }
                    }
                    ReleaseStuckModifiers();
                    ToastForm.ShowToast(
                        "DISABLED: Default Keyboard Restored",
                        "Left Ctrl and Left Alt are back to normal",
                        Color.FromArgb(239, 68, 68),
                        1400,
                        true
                    );
                    Application.Run();
                    return;
                }

                _iconActive = CreateAppIcon(true);
                _iconPaused = CreateAppIcon(false);

                ContextMenuStrip menu = new ContextMenuStrip();
                _toggleItem = new ToolStripMenuItem("Active (Click to Pause)", null, OnToggle);
                _toggleItem.Font = new Font(_toggleItem.Font, FontStyle.Bold);
                ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit SwapCtrlAlt", null, OnExit);

                menu.Items.Add(_toggleItem);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(exitItem);

                // Note: NotifyIcon.Text must be < 64 chars on .NET Framework
                _trayIcon = new NotifyIcon
                {
                    Icon = _iconActive,
                    Text = "SwapCtrlAlt: Active (LCtrl ⇄ LAlt)",
                    ContextMenuStrip = menu,
                    Visible = true
                };
                _trayIcon.DoubleClick += OnToggle;

                _hookID = SetHook(_proc);

                ToastForm.ShowToast(
                    "ACTIVE: Left Ctrl ⇄ Left Alt",
                    "Run the app again or use tray icon to disable",
                    Color.FromArgb(16, 185, 129),
                    1800,
                    false
                );

                Application.ApplicationExit += (s, e) => Cleanup();
                Application.Run();
            }
        }

        private static void OnToggle(object sender, EventArgs e)
        {
            _enabled = !_enabled;
            ReleaseStuckModifiers();
            if (_enabled)
            {
                _trayIcon.Icon = _iconActive;
                _trayIcon.Text = "SwapCtrlAlt: Active (LCtrl ⇄ LAlt)";
                _toggleItem.Text = "Active (Click to Pause)";
                ToastForm.ShowToast(
                    "ACTIVE: Left Ctrl ⇄ Left Alt",
                    "Key swapping resumed",
                    Color.FromArgb(16, 185, 129),
                    1300,
                    false
                );
            }
            else
            {
                _trayIcon.Icon = _iconPaused;
                _trayIcon.Text = "SwapCtrlAlt: Paused";
                _toggleItem.Text = "Paused (Click to Resume)";
                ToastForm.ShowToast(
                    "PAUSED: Left Ctrl ⇄ Left Alt",
                    "Double-click tray icon to resume",
                    Color.FromArgb(107, 114, 128),
                    1300,
                    false
                );
            }
        }

        private static void OnExit(object sender, EventArgs e)
        {
            Cleanup();
            Application.Exit();
        }

        private static void Cleanup()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            ReleaseStuckModifiers();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }

        private static void ReleaseStuckModifiers()
        {
            if ((GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0)
                keybd_event((byte)VK_LCONTROL, SCAN_LCTRL, KEYEVENTF_KEYUP, UIntPtr.Zero);
            if ((GetAsyncKeyState(VK_LMENU) & 0x8000) != 0)
                keybd_event((byte)VK_LMENU, SCAN_LALT, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _enabled)
            {
                KBDLLHOOKSTRUCT hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                // Ignore synthetic keystrokes injected by our own hook
                if ((hookStruct.flags & LLKHF_INJECTED) == 0)
                {
                    int msg = wParam.ToInt32();
                    bool isDown = (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN);
                    bool isUp = (msg == WM_KEYUP || msg == WM_SYSKEYUP);

                    if (isDown || isUp)
                    {
                        uint flags = isUp ? KEYEVENTF_KEYUP : 0;

                        // Ignore synthetic Left Ctrl generated by Right Alt (AltGr, scanCode 0x21D or Extended)
                        if (hookStruct.vkCode == VK_LCONTROL && (hookStruct.flags & LLKHF_EXTENDED) == 0 && hookStruct.scanCode != 0x21D)
                        {
                            keybd_event((byte)VK_LMENU, SCAN_LALT, flags, UIntPtr.Zero);
                            return (IntPtr)1;
                        }
                        else if (hookStruct.vkCode == VK_LMENU && (hookStruct.flags & LLKHF_EXTENDED) == 0)
                        {
                            keybd_event((byte)VK_LCONTROL, SCAN_LCTRL, flags, UIntPtr.Zero);
                            return (IntPtr)1;
                        }
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public static Bitmap RenderKeycapArt(int size, bool active)
        {
            Bitmap bmp = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                float pad = size * 0.04f;
                float bgSize = size - pad * 2;
                float radius = size * 0.24f;

                Color bg1 = active ? Color.FromArgb(79, 70, 229) : Color.FromArgb(75, 85, 99);
                Color bg2 = active ? Color.FromArgb(6, 182, 212) : Color.FromArgb(55, 65, 81);

                using (GraphicsPath bgPath = RoundedRect(new RectangleF(pad, pad, bgSize, bgSize), radius))
                using (LinearGradientBrush bgBrush = new LinearGradientBrush(
                    new PointF(0, 0), new PointF(size, size), bg1, bg2))
                {
                    g.FillPath(bgBrush, bgPath);
                    using (Pen borderPen = new Pen(Color.FromArgb(90, 255, 255, 255), Math.Max(1f, size * 0.03f)))
                    {
                        g.DrawPath(borderPen, bgPath);
                    }
                }

                // Top-left keycap: Ctrl (Amber/Orange)
                RectangleF key1 = new RectangleF(size * 0.12f, size * 0.14f, size * 0.48f, size * 0.34f);
                Color k1Top = active ? Color.FromArgb(245, 158, 11) : Color.FromArgb(107, 114, 128);
                Color k1Bot = active ? Color.FromArgb(217, 119, 6) : Color.FromArgb(75, 85, 99);
                DrawKeycap(g, key1, size * 0.08f, k1Top, k1Bot, "Ctrl", size * 0.145f);

                // Bottom-right keycap: Alt (Emerald/Green)
                RectangleF key2 = new RectangleF(size * 0.40f, size * 0.52f, size * 0.48f, size * 0.34f);
                Color k2Top = active ? Color.FromArgb(16, 185, 129) : Color.FromArgb(107, 114, 128);
                Color k2Bot = active ? Color.FromArgb(5, 150, 105) : Color.FromArgb(75, 85, 99);
                DrawKeycap(g, key2, size * 0.08f, k2Top, k2Bot, "Alt", size * 0.145f);

                // Swap arrows
                using (Font arrowFont = new Font("Segoe UI Symbol", size * 0.15f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString("⇅", arrowFont, Brushes.White, new RectangleF(size * 0.65f, size * 0.14f, size * 0.25f, size * 0.34f), sf);
                }
            }
            return bmp;
        }

        private static void DrawKeycap(Graphics g, RectangleF rect, float radius, Color c1, Color c2, string text, float fontSize)
        {
            RectangleF shadowRect = new RectangleF(rect.X, rect.Y + rect.Height * 0.10f, rect.Width, rect.Height);
            using (GraphicsPath sp = RoundedRect(shadowRect, radius))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(95, 0, 0, 0)))
            {
                g.FillPath(sb, sp);
            }

            using (GraphicsPath kp = RoundedRect(rect, radius))
            using (LinearGradientBrush kb = new LinearGradientBrush(rect, c1, c2, LinearGradientMode.Vertical))
            {
                g.FillPath(kb, kp);
                using (Pen kpPen = new Pen(Color.FromArgb(160, 255, 255, 255), Math.Max(1f, rect.Height * 0.06f)))
                {
                    g.DrawPath(kpPen, kp);
                }
            }

            using (Font f = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(text, f, Brushes.White, rect, sf);
            }
        }

        public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
        {
            float d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Icon CreateAppIcon(bool active)
        {
            using (Bitmap bmp = RenderKeycapArt(64, active))
            {
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }

    internal class ToastForm : Form
    {
        private readonly string _title;
        private readonly string _subtitle;
        private readonly Color _accent;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly bool _exitAppOnClose;
        private int _ticks;
        private readonly int _maxTicks;

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WS_EX_TOPMOST (0x08) | WS_EX_TOOLWINDOW (0x80) | WS_EX_NOACTIVATE (0x08000000)
                cp.ExStyle |= 0x00000008 | 0x00000080 | 0x08000000;
                return cp;
            }
        }

        public static void ShowToast(string title, string subtitle, Color accent, int durationMs, bool exitAppOnClose)
        {
            ToastForm form = new ToastForm(title, subtitle, accent, durationMs, exitAppOnClose);
            form.Show();
        }

        private ToastForm(string title, string subtitle, Color accent, int durationMs, bool exitAppOnClose)
        {
            _title = title;
            _subtitle = subtitle;
            _accent = accent;
            _exitAppOnClose = exitAppOnClose;
            _maxTicks = Math.Max(10, durationMs / 30);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(360, 74);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(24, 24, 32);
            Opacity = 0.96;
            ShowInTaskbar = false;
            TopMost = true;

            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(screen.Left + (screen.Width - Width) / 2, screen.Top + 36);

            using (GraphicsPath path = Program.RoundedRect(new RectangleF(0, 0, Width, Height), 16f))
            {
                Region = new Region(path);
            }

            _timer = new System.Windows.Forms.Timer { Interval = 30 };
            _timer.Tick += (s, e) =>
            {
                _ticks++;
                if (_ticks > _maxTicks - 8)
                {
                    Opacity = Math.Max(0, Opacity - 0.12);
                }
                if (_ticks >= _maxTicks)
                {
                    _timer.Stop();
                    Close();
                    if (_exitAppOnClose)
                    {
                        Application.Exit();
                    }
                }
            };
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using (GraphicsPath border = Program.RoundedRect(new RectangleF(1, 1, Width - 2, Height - 2), 15f))
            using (Pen p = new Pen(_accent, 2f))
            {
                g.DrawPath(p, border);
            }

            using (Bitmap iconBmp = Program.RenderKeycapArt(48, _accent.G > 120))
            {
                g.DrawImage(iconBmp, 16, 13, 48, 48);
            }

            using (Font fTitle = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (Font fSub = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (SolidBrush bAccent = new SolidBrush(_accent))
            using (SolidBrush bSub = new SolidBrush(Color.FromArgb(200, 210, 225)))
            {
                g.DrawString(_title, fTitle, bAccent, new PointF(76, 14));
                g.DrawString(_subtitle, fSub, bSub, new PointF(76, 40));
            }
        }
    }
}
