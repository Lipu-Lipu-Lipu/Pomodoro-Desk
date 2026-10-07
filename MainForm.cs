using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PomodoroTimer;

public sealed class MainForm : Form
{
    private static readonly Color BgColor = Color.FromArgb(0x1E, 0x1E, 0x1E);
    private static readonly Color FgColor = Color.FromArgb(0xEA, 0xEA, 0xEA);
    private static readonly Color DimColor = Color.FromArgb(0x9A, 0x9A, 0x9A);
    private static readonly Color WorkColor = Color.FromArgb(0xE7, 0x4C, 0x3C);
    private static readonly Color RestColor = Color.FromArgb(0x2E, 0xCC, 0x71);
    private static readonly Color OvertimeColor = Color.FromArgb(0xF3, 0x9C, 0x12);
    private static readonly Color TrackColor = Color.FromArgb(0x2E, 0x2E, 0x2E);
    private static readonly Color IdleRingColor = Color.FromArgb(0x45, 0x45, 0x4D);
    private static readonly Color ControlBgColor = Color.FromArgb(0x2D, 0x2D, 0x2D);
    private static readonly Color LinkColor = Color.FromArgb(0x8A, 0xB4, 0xF8);

    private readonly PomodoroEngine _engine = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _trayMenu = new() { Renderer = new DarkMenuRenderer() };
    private readonly AppSettings _loadedSettings = SettingsStore.Load();

    private readonly Label _lblTitle = new();
    private readonly Label _lblPhase = new();
    private readonly Label _lblWork = new();
    private readonly Label _lblRest = new();
    private readonly Label _lblWorkMinUnit = new();
    private readonly Label _lblWorkSecUnit = new();
    private readonly Label _lblRestMinUnit = new();
    private readonly Label _lblRestSecUnit = new();
    private readonly NumericUpDown _numWorkMin = new();
    private readonly NumericUpDown _numWorkSec = new();
    private readonly NumericUpDown _numRestMin = new();
    private readonly NumericUpDown _numRestSec = new();
    private readonly LinkLabel _lnkReset = new();
    private readonly TimerRing _ring;
    private readonly AccentButton _btnAction = new();

    private int _tickCount;
    private bool _settingsLocked;
    private int _lockedWorkMin = 25;
    private int _lockedWorkSec;
    private int _lockedRestMin = 5;
    private int _lockedRestSec;
    private AlertForm? _activeAlert;

    public MainForm()
    {
        Text = "番茄钟";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgColor;
        Font = new Font("Segoe UI", 10.5f);
        Icon = LoadAppIcon();
        ClientSize = new Size(S(400), S(620));
        MinimumSize = new Size(S(400), S(620));
        DpiChanged += OnFormDpiChanged;

        _ring = new TimerRing(S(320))
        {
            TimeFont = new Font("Segoe UI", 42f, FontStyle.Bold),
            TrackColor = TrackColor,
            TextColor = FgColor,
            OvertimeColor = OvertimeColor
        };

        BuildUi();

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Visible = true,
            Text = "番茄钟"
        };
        _notifyIcon.BalloonTipClicked += (_, _) => RestoreAndActivate();
        _notifyIcon.DoubleClick += (_, _) => RestoreAndActivate();

        var openItem = new ToolStripMenuItem("打开番茄钟") { ForeColor = FgColor };
        openItem.Font = new Font(openItem.Font, FontStyle.Bold);
        openItem.Click += (_, _) => RestoreAndActivate();
        var exitItem = new ToolStripMenuItem("退出") { ForeColor = FgColor };
        exitItem.Click += (_, _) => Close();
        _trayMenu.Items.Add(openItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(exitItem);
        _notifyIcon.ContextMenuStrip = _trayMenu;

        _timer.Tick += (_, _) => OnTick();

        UpdateIdleUi();
    }

    private int S(int px) => Scaled(px, DeviceDpi);

    private static int Scaled(int px, int dpi) => (int)Math.Round(px * (dpi / 96.0));

    /// <summary>
    /// 跨屏 DPI 变化后重建最小尺寸约束并校正窗口位置尺寸。
    /// 否则旧 DPI 的 MinimumSize 会把窗口“锁大”：系统缩放被钳制，导致窗口比屏幕还大且无法缩小。
    /// </summary>
    private void OnFormDpiChanged(object? sender, DpiChangedEventArgs e)
    {
        // 延迟到系统 DPI 变更处理完成后执行，覆盖建议矩形被旧 MinimumSize 卡住的情况
        BeginInvoke(() =>
        {
            if (IsDisposed)
            {
                return;
            }
            MinimumSize = new Size(Scaled(400, e.DeviceDpiNew), Scaled(620, e.DeviceDpiNew));
            Bounds = e.SuggestedRectangle;
            ClampToWorkArea();
        });
    }

    /// <summary>确保窗口完整落在当前屏幕的工作区内。</summary>
    private void ClampToWorkArea()
    {
        Screen screen = Screen.FromControl(this);
        Rectangle workArea = screen.WorkingArea;
        int w = Math.Min(Width, workArea.Width);
        int h = Math.Min(Height, workArea.Height);
        int x = Math.Min(Math.Max(Location.X, workArea.X), workArea.Right - w);
        int y = Math.Min(Math.Max(Location.Y, workArea.Y), workArea.Bottom - h);
        if (w != Width || h != Height || x != Location.X || y != Location.Y)
        {
            Bounds = new Rectangle(x, y, w, h);
        }
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? Application.ExecutablePath;
            Icon? extracted = Icon.ExtractAssociatedIcon(exePath);
            if (extracted != null)
            {
                return extracted;
            }
        }
        catch
        {
        }
        return SystemIcons.Application;
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = BgColor,
            Padding = new Padding(S(36), S(22), S(36), S(24))
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 标题
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 阶段
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 圆环（随窗口伸缩）
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 设置
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 按钮
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 返回设置

        _lblTitle.Text = "🍅 番茄钟";
        _lblTitle.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
        _lblTitle.ForeColor = FgColor;
        _lblTitle.AutoSize = true;
        _lblTitle.Anchor = AnchorStyles.None;
        _lblTitle.Margin = new Padding(0, 0, 0, S(2));

        _lblPhase.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        _lblPhase.ForeColor = DimColor;
        _lblPhase.AutoSize = true;
        _lblPhase.Anchor = AnchorStyles.None;
        _lblPhase.Margin = new Padding(0, 0, 0, S(12));

        _ring.Anchor = AnchorStyles.None;
        _ring.Margin = new Padding(0, 0, 0, S(20));

        TableLayoutPanel settings = BuildSettingsPanel();
        settings.Anchor = AnchorStyles.None;
        settings.Margin = new Padding(0, 0, 0, S(18));

        _btnAction.Font = new Font("Segoe UI", 12.5f, FontStyle.Bold);
        _btnAction.ForeColor = Color.White;
        _btnAction.Accent = WorkColor;
        _btnAction.Anchor = AnchorStyles.None;
        _btnAction.Margin = new Padding(0, 0, 0, S(10));
        _btnAction.Click += (_, _) => OnAction();

        _lnkReset.Text = "返回设置";
        _lnkReset.Font = new Font("Segoe UI", 9.5f);
        _lnkReset.LinkColor = LinkColor;
        _lnkReset.ActiveLinkColor = LinkColor;
        _lnkReset.AutoSize = true;
        _lnkReset.Anchor = AnchorStyles.None;
        _lnkReset.Visible = false;
        _lnkReset.Cursor = Cursors.Hand;
        _lnkReset.LinkBehavior = LinkBehavior.HoverUnderline;
        _lnkReset.Click += (_, _) => ResetToIdle();

        root.Controls.Add(_lblTitle, 0, 0);
        root.Controls.Add(_lblPhase, 0, 1);
        root.Controls.Add(_ring, 0, 2);
        root.Controls.Add(settings, 0, 3);
        root.Controls.Add(_btnAction, 0, 4);
        root.Controls.Add(_lnkReset, 0, 5);
        Controls.Add(root);

        root.Layout += (_, _) =>
        {
            int[] widths = root.GetColumnWidths();
            int[] heights = root.GetRowHeights();
            if (widths.Length == 0 || heights.Length < 3)
            {
                return;
            }
            int availW = widths[0] - _ring.Margin.Horizontal;
            int availH = heights[2] - _ring.Margin.Vertical;
            int side = Math.Max(S(160), Math.Min(availW, availH));
            if (_ring.Width != side || _ring.Height != side)
            {
                _ring.Size = new Size(side, side);
            }
        };
    }

    private TableLayoutPanel BuildSettingsPanel()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 5,
            BackColor = BgColor
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _lblWork.Text = "工作";
        _lblWork.AutoSize = true;
        _lblWork.ForeColor = FgColor;
        _lblWork.Anchor = AnchorStyles.Right;
        _lblWork.Margin = new Padding(0, S(8), S(10), S(8));

        _lblRest.Text = "休息";
        _lblRest.AutoSize = true;
        _lblRest.ForeColor = FgColor;
        _lblRest.Anchor = AnchorStyles.Right;
        _lblRest.Margin = new Padding(0, S(8), S(10), S(8));

        ConfigureMinutes(_numWorkMin, maxMinutes: 120, defaultValue: _loadedSettings.WorkMinutes);
        ConfigureSeconds(_numWorkSec, _loadedSettings.WorkSeconds);
        ConfigureUnit(_lblWorkMinUnit, "分");
        ConfigureUnit(_lblWorkSecUnit, "秒");
        _numWorkMin.ValueChanged += (_, _) =>
        {
            if (_settingsLocked)
            {
                RevertIfChanged(_numWorkMin, _numWorkSec, _lockedWorkMin, _lockedWorkSec);
                return;
            }
            UpdateIdlePreview();
            SaveSettings();
        };
        _numWorkSec.ValueChanged += (_, _) =>
        {
            if (_settingsLocked)
            {
                RevertIfChanged(_numWorkMin, _numWorkSec, _lockedWorkMin, _lockedWorkSec);
                return;
            }
            UpdateIdlePreview();
            SaveSettings();
        };

        ConfigureMinutes(_numRestMin, maxMinutes: 60, defaultValue: _loadedSettings.RestMinutes);
        ConfigureSeconds(_numRestSec, _loadedSettings.RestSeconds);
        ConfigureUnit(_lblRestMinUnit, "分");
        ConfigureUnit(_lblRestSecUnit, "秒");
        _numRestMin.ValueChanged += (_, _) =>
        {
            if (_settingsLocked)
            {
                RevertIfChanged(_numRestMin, _numRestSec, _lockedRestMin, _lockedRestSec);
                return;
            }
            SaveSettings();
        };
        _numRestSec.ValueChanged += (_, _) =>
        {
            if (_settingsLocked)
            {
                RevertIfChanged(_numRestMin, _numRestSec, _lockedRestMin, _lockedRestSec);
                return;
            }
            SaveSettings();
        };

        panel.Controls.Add(_lblWork, 0, 0);
        panel.Controls.Add(_numWorkMin, 1, 0);
        panel.Controls.Add(_lblWorkMinUnit, 2, 0);
        panel.Controls.Add(_numWorkSec, 3, 0);
        panel.Controls.Add(_lblWorkSecUnit, 4, 0);
        panel.Controls.Add(_lblRest, 0, 1);
        panel.Controls.Add(_numRestMin, 1, 1);
        panel.Controls.Add(_lblRestMinUnit, 2, 1);
        panel.Controls.Add(_numRestSec, 3, 1);
        panel.Controls.Add(_lblRestSecUnit, 4, 1);
        return panel;
    }

    private void ConfigureMinutes(NumericUpDown num, int maxMinutes, int defaultValue)
    {
        num.Minimum = 0;
        num.Maximum = maxMinutes;
        num.Value = defaultValue;
        num.Width = S(64);
        // 保证布局单元格不小于控件宽度，避免右侧上下微调箭头被裁剪
        num.MinimumSize = new Size(S(64), 0);
        num.BackColor = ControlBgColor;
        num.ForeColor = FgColor;
        num.TextAlign = HorizontalAlignment.Center;
        num.Margin = new Padding(0, S(5), 0, S(5));
    }

    private void ConfigureSeconds(NumericUpDown num, int defaultValue)
    {
        num.Minimum = 0;
        num.Maximum = 59;
        num.Value = defaultValue;
        num.Width = S(56);
        num.MinimumSize = new Size(S(56), 0);
        num.BackColor = ControlBgColor;
        num.ForeColor = FgColor;
        num.TextAlign = HorizontalAlignment.Center;
        num.Margin = new Padding(0, S(5), 0, S(5));
    }

    private void ConfigureUnit(Label unit, string text)
    {
        unit.Text = text;
        unit.AutoSize = true;
        unit.ForeColor = DimColor;
        unit.Font = new Font("Segoe UI", 9.5f);
        unit.Anchor = AnchorStyles.Right;
        unit.Margin = new Padding(S(6), S(9), S(6), 0);
    }

    /// <summary>运行中锁定设置：任一输入偏离锁定值时，分/秒整体回弹。</summary>
    private static void RevertIfChanged(NumericUpDown minutes, NumericUpDown seconds, int lockedMin, int lockedSec)
    {
        if ((int)minutes.Value != lockedMin || (int)seconds.Value != lockedSec)
        {
            minutes.Value = lockedMin;
            seconds.Value = lockedSec;
        }
    }

    private void OnAction()
    {
        if (!_engine.IsRunning)
        {
            _lockedWorkMin = (int)_numWorkMin.Value;
            _lockedWorkSec = (int)_numWorkSec.Value;
            _lockedRestMin = (int)_numRestMin.Value;
            _lockedRestSec = (int)_numRestSec.Value;
            _engine.WorkSeconds = TotalSecondsOf(_lockedWorkMin, _lockedWorkSec);
            _engine.RestSeconds = TotalSecondsOf(_lockedRestMin, _lockedRestSec);
            _settingsLocked = true;
            _engine.Start();
            _lnkReset.Visible = true;
            _timer.Start();
            BeepPlayer.PlayStart();
            RefreshRunningUi(_engine.Poll());
        }
        else
        {
            SwitchToNextPhase();
        }
    }

    /// <summary>结束当前阶段并进入下一阶段（仅主窗口按钮触发）。</summary>
    private void SwitchToNextPhase()
    {
        _engine.EndCurrentPhase();
        BeepPlayer.PlayStart();
        RefreshRunningUi(_engine.Poll());
    }

    private void ResetToIdle()
    {
        _engine.Reset();
        _timer.Stop();
        _settingsLocked = false;
        UpdateIdleUi();
    }

    private void UpdateIdleUi()
    {
        _lblPhase.Text = "准备开始";
        _lblPhase.ForeColor = DimColor;
        UpdateIdlePreview();
        _btnAction.Text = "开始";
        _btnAction.Accent = WorkColor;
        UpdateActionButtonSize();
        _lnkReset.Visible = false;
    }

    /// <summary>空闲时按当前设置预览总时长。</summary>
    private void UpdateIdlePreview()
    {
        int total = TotalSecondsOf((int)_numWorkMin.Value, (int)_numWorkSec.Value);
        _ring.SetState(FormatRemaining(total), 1.0, overtime: false, pulse: false, IdleRingColor);
    }

    private void RefreshRunningUi(PhaseSnapshot snap)
    {
        bool isWork = snap.Phase == Phase.Work;
        Color phaseColor = isWork ? WorkColor : RestColor;

        _lblPhase.Text = isWork ? "工作中" : "休息中";
        _lblPhase.ForeColor = phaseColor;

        if (snap.IsOvertime)
        {
            bool pulse = _tickCount / 2 % 2 == 0;
            _ring.SetState(FormatOvertime(snap.OvertimeSeconds), 1.0, overtime: true, pulse, OvertimeColor);
        }
        else
        {
            int target = isWork ? _engine.WorkSeconds : _engine.RestSeconds;
            double progress = target > 0 ? (double)snap.RemainingSeconds / target : 0;
            _ring.SetState(FormatRemaining(snap.RemainingSeconds), progress, overtime: false, pulse: false, phaseColor);
        }

        _btnAction.Text = isWork ? "结束，进入休息" : "结束，进入工作";
        _btnAction.Accent = isWork ? RestColor : WorkColor;
        UpdateActionButtonSize();
    }

    private void OnTick()
    {
        PhaseSnapshot snap = _engine.Poll();
        if (snap.Phase == Phase.Idle)
        {
            return;
        }

        _tickCount++;
        RefreshRunningUi(snap);

        if (snap.JustFiredWarn)
        {
            BeepPlayer.PlayWarn();
            string phaseName = snap.Phase == Phase.Work ? "工作" : "休息";
            ShowBalloon("还剩 1 分钟", $"{phaseName}阶段还剩 1 分钟", ToolTipIcon.Info);
            ShowAlert(new AlertSpec(
                Emoji: "⏳",
                Title: "最后 1 分钟",
                Subtitle: $"{phaseName}阶段还剩 1 分钟",
                Accent: snap.Phase == Phase.Work ? WorkColor : RestColor,
                AutoCloseSeconds: 10,
                ActivateOnShow: false));
            FlashTaskbar();
        }

        if (snap.JustFiredEnd)
        {
            BeepPlayer.PlayEnd();
            bool endOfWork = snap.Phase == Phase.Work;
            string msg = endOfWork
                ? "工作时间到！需要休息请在主窗口点击“结束，进入休息”"
                : "休息时间到！需要工作请在主窗口点击“结束，进入工作”";
            ShowBalloon("时间到", msg, ToolTipIcon.Warning);
            RestoreAndActivate();
            ShowAlert(new AlertSpec(
                Emoji: endOfWork ? "🌿" : "🍅",
                Title: endOfWork ? "工作时间到！" : "休息时间到！",
                Subtitle: endOfWork
                    ? "已进入超时计时，需要休息请在主窗口点击“结束，进入休息”"
                    : "已进入超时计时，需要工作请在主窗口点击“结束，进入工作”",
                Accent: endOfWork ? RestColor : WorkColor,
                AutoCloseSeconds: 0,
                ActivateOnShow: true));
            FlashTaskbar();
        }
    }

    private void UpdateActionButtonSize()
    {
        Size textSize = TextRenderer.MeasureText(
            _btnAction.Text, _btnAction.Font, Size.Empty, TextFormatFlags.NoPadding);
        _btnAction.Size = new Size(
            Math.Max(S(250), textSize.Width + S(70)),
            textSize.Height + S(28));
    }

    /// <summary>将分/秒设置换算为总秒数，至少 1 秒。</summary>
    internal static int TotalSecondsOf(int minutes, int seconds) => Math.Max(1, minutes * 60 + seconds);

    /// <summary>把当前输入框的设置写入磁盘（空闲状态下改动时调用）。</summary>
    private void SaveSettings()
    {
        SettingsStore.Save(new AppSettings(
            (int)_numWorkMin.Value,
            (int)_numWorkSec.Value,
            (int)_numRestMin.Value,
            (int)_numRestSec.Value));
    }

    private static string FormatRemaining(int seconds) => $"{seconds / 60:D2}:{seconds % 60:D2}";

    private static string FormatOvertime(int seconds)
    {
        int mins = seconds / 60;
        int secs = seconds % 60;
        return mins >= 100 ? $"+{mins / 60}h{mins % 60:D2}" : $"+{mins:D2}:{secs:D2}";
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        _notifyIcon.ShowBalloonTip(5000, title, text, icon);
    }

    private void RestoreAndActivate()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }
        Show();
        Win32Interop.ForceToForeground(Handle);
        Activate();
    }

    /// <summary>弹出置顶提醒卡片（同一时刻最多一张，新卡片顶替旧卡片）。</summary>
    private void ShowAlert(AlertSpec spec)
    {
        if (_activeAlert is not null)
        {
            _activeAlert.FormClosed -= OnAlertClosed;
            _activeAlert.Close();
            _activeAlert.Dispose();
            _activeAlert = null;
        }

        var alert = new AlertForm(spec);
        _activeAlert = alert;
        alert.FormClosed += OnAlertClosed;
        alert.Show(this);
    }

    private void OnAlertClosed(object? sender, FormClosedEventArgs e)
    {
        if (ReferenceEquals(sender, _activeAlert))
        {
            _activeAlert = null;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // 默认即最小尺寸，并在当前屏幕工作区内居中
        Size = MinimumSize;
        Rectangle workArea = Screen.FromControl(this).WorkingArea;
        Location = new Point(
            workArea.X + (workArea.Width - Width) / 2,
            workArea.Y + (workArea.Height - Height) / 2);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int dark = 1;
            if (DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)) != 0)
            {
                _ = DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
            }
        }
        catch
        {
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        // 点叉（含 Alt+F4）直接关闭应用，不再隐藏到托盘。
        // 系统关机/注销（WindowsShutDown / TaskManagerClosing 等）也走这里，正常关闭即可。
        // 计时状态随进程结束而丢失，这是刻意的：与「关闭即结束」的直觉一致。
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _trayMenu.Dispose();
        _notifyIcon.Dispose();
        base.OnFormClosed(e);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    #region 任务栏闪烁

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_ALL = 0x3;

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    private void FlashTaskbar()
    {
        try
        {
            var info = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = Handle,
                dwFlags = FLASHW_ALL,
                uCount = 5,
                dwTimeout = 0
            };
            _ = FlashWindowEx(ref info);
        }
        catch
        {
        }
    }

    #endregion

    private sealed class TimerRing : Control
    {
        private const float StartAngle = -90f;

        private string _text = string.Empty;
        private double _progress;
        private bool _overtime;
        private bool _pulse;
        private Color _ringColor = Color.Gray;

        public TimerRing(int sizePx)
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            Size = new Size(sizePx, sizePx);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Font TimeFont { get; set; } = SystemFonts.DefaultFont;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color TrackColor { get; set; } = Color.FromArgb(0x2E, 0x2E, 0x2E);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color TextColor { get; set; } = Color.White;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color OvertimeColor { get; set; } = Color.Orange;

        public void SetState(string text, double progress, bool overtime, bool pulse, Color ringColor)
        {
            _text = text;
            _progress = progress;
            _overtime = overtime;
            _pulse = pulse;
            _ringColor = ringColor;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            float thickness = Math.Max(8f, Width / 17f);
            var bounds = new RectangleF(
                thickness / 2f + 1f,
                thickness / 2f + 1f,
                Width - thickness - 2f,
                Height - thickness - 2f);

            using (var track = new Pen(TrackColor, thickness))
            {
                g.DrawArc(track, bounds, 0f, 360f);
            }

            if (_overtime)
            {
                Color color = _pulse ? OvertimeColor : Color.FromArgb(130, OvertimeColor);
                using var pen = new Pen(color, thickness)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                g.DrawArc(pen, bounds, StartAngle, 359.9f);
            }
            else if (_progress > 0.002)
            {
                using var pen = new Pen(_ringColor, thickness)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                g.DrawArc(pen, bounds, StartAngle, (float)(Math.Min(1.0, _progress) * 359.9));
            }

            Font font = GetFittedFont(thickness);

            TextRenderer.DrawText(
                g,
                _text,
                font,
                ClientRectangle,
                _overtime ? OvertimeColor : TextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private Font? _fittedFont;
        private float _fittedPt = -1f;

        private Font GetFittedFont(float thickness)
        {
            // 内接矩形约束：文本对角线不超过圈内正方形对角线，保证任何文本都不压圆弧
            float inner = Math.Max(10f, Width - 2f * thickness - 4f);
            double maxDiagSq = (double)(inner * 0.92f) * (inner * 0.92f);
            float pt = Math.Max(9f, Width * 0.24f * 72f / DeviceDpi);
            Font font = GetFont(pt);
            Size ts = TextRenderer.MeasureText(_text, font, Size.Empty, TextFormatFlags.NoPadding);
            while (pt > 8f)
            {
                double diagSq = (double)ts.Width * ts.Width + (double)ts.Height * ts.Height;
                if (diagSq <= maxDiagSq)
                {
                    break;
                }
                pt = Math.Max(8f, pt - Math.Max(0.5f, pt * 0.06f));
                font = GetFont(pt);
                ts = TextRenderer.MeasureText(_text, font, Size.Empty, TextFormatFlags.NoPadding);
            }
            return font;
        }

        private Font GetFont(float pt)
        {
            if (_fittedFont == null || Math.Abs(pt - _fittedPt) > 0.25f)
            {
                _fittedFont?.Dispose();
                _fittedFont = new Font(TimeFont.FontFamily, pt, TimeFont.Style);
                _fittedPt = pt;
            }
            return _fittedFont;
        }
    }

}
