using System.ComponentModel;

namespace PomodoroTimer;

/// <summary>提醒卡片的内容与行为配置。卡片仅起通知作用，不做任何业务操作。</summary>
internal sealed record AlertSpec(
    string Emoji,
    string Title,
    string Subtitle,
    Color Accent,
    int AutoCloseSeconds,
    bool ActivateOnShow);

/// <summary>
/// 强制置顶提醒卡片：无边框 TopMost 窗口，绘制在所有普通窗口之上。
/// 到时提醒需手动点击关闭并尝试夺取前台焦点；提前提醒不抢焦点、可自动关闭。
/// </summary>
internal sealed class AlertForm : Form
{
    private static readonly Color BgColor = Color.FromArgb(0x26, 0x26, 0x2B);
    private static readonly Color FgColor = Color.FromArgb(0xEA, 0xEA, 0xEA);
    private static readonly Color DimColor = Color.FromArgb(0x9A, 0x9A, 0x9A);

    private readonly AlertSpec _spec;
    private readonly System.Windows.Forms.Timer? _autoCloseTimer;

    public AlertForm(AlertSpec spec)
    {
        _spec = spec;

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = BgColor;
        Font = new Font("Segoe UI", 10.5f);
        KeyPreview = true;
        ClientSize = new Size(S(460), S(330));

        BuildUi();
        PositionOnScreen();

        if (spec.AutoCloseSeconds > 0)
        {
            _autoCloseTimer = new System.Windows.Forms.Timer { Interval = spec.AutoCloseSeconds * 1000 };
            _autoCloseTimer.Tick += (_, _) => Close();
        }
    }

    /// <summary>提前提醒不抢焦点；到时提醒允许激活。</summary>
    protected override bool ShowWithoutActivation => !_spec.ActivateOnShow;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW：软阴影
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Win32Interop.PreferRoundedCorners(Handle);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_spec.ActivateOnShow)
        {
            Win32Interop.TryActivate(Handle);
            Activate();
        }
        _autoCloseTimer?.Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Esc 只关闭卡片，不触发主操作，防止误触切换阶段
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        PositionOnScreen();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _autoCloseTimer?.Stop();
        _autoCloseTimer?.Dispose();
        base.OnFormClosed(e);
    }

    private int S(int px) => (int)Math.Round(px * (DeviceDpi / 96f));

    private void BuildUi()
    {
        var strip = new Panel
        {
            Dock = DockStyle.Top,
            Height = S(6),
            BackColor = _spec.Accent
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = BgColor,
            Padding = new Padding(S(24), S(20), S(24), S(24))
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // emoji
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 标题
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 副标题
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // 弹性
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 按钮

        var emoji = new Label
        {
            Text = _spec.Emoji,
            Font = new Font("Segoe UI Emoji", 38f),
            ForeColor = FgColor,
            AutoSize = true,
            Anchor = AnchorStyles.None
        };

        var title = new Label
        {
            Text = _spec.Title,
            Font = new Font("Segoe UI", 19f, FontStyle.Bold),
            ForeColor = _spec.Accent,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, S(4), 0, S(6))
        };

        var subtitle = new Label
        {
            Text = _spec.Subtitle,
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = DimColor,
            AutoSize = true,
            Anchor = AnchorStyles.None,
            MaximumSize = new Size(S(380), 0)
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.None,
            BackColor = BgColor,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        var dismissFont = new Font("Segoe UI", 12f, FontStyle.Bold);
        var dismiss = new AccentButton
        {
            Text = "知道了",
            Font = dismissFont,
            ForeColor = Color.White,
            Accent = _spec.Accent,
            Size = MeasureButton("知道了", dismissFont, S(190))
        };
        dismiss.Click += (_, _) => Close();
        buttons.Controls.Add(dismiss);

        root.Controls.Add(emoji, 0, 0);
        root.Controls.Add(title, 0, 1);
        root.Controls.Add(subtitle, 0, 2);
        root.Controls.Add(buttons, 0, 4);

        Controls.Add(root);
        Controls.Add(strip);
    }

    private Size MeasureButton(string text, Font font, int minWidth)
    {
        Size textSize = TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPadding);
        return new Size(Math.Max(minWidth, textSize.Width + S(52)), textSize.Height + S(26));
    }

    private void PositionOnScreen()
    {
        // 跟随主窗口所在屏幕弹出；Owner 在 Show(owner) 时才赋值，故 OnLoad 再定位一次
        Screen screen = Owner is null
            ? Screen.PrimaryScreen ?? Screen.FromControl(this)
            : Screen.FromControl(Owner);
        Rectangle workArea = screen.WorkingArea;
        Location = new Point(
            workArea.X + (workArea.Width - Width) / 2,
            workArea.Y + (int)(workArea.Height * 0.30) - Height / 2);
    }
}
