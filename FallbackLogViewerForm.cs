using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VlcShimDebugFr;

internal sealed class FallbackLogViewerForm : Form
{
    private const int InitialLineCount = 200;
    private const int ResizeBorderThickness = 8;
    private const int WM_NCHITTEST = 0x84;
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION = 0x02;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;
    private const int CS_DROPSHADOW = 0x00020000;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    private readonly string _logPath;
    private readonly TextBox _textBox;
    private readonly Panel _titleBar;
    private readonly Label _titleLabel;
    private readonly Button _maximizeButton;
    private readonly Button _closeButton;
    private LogViewerTheme _theme;

    public FallbackLogViewerForm(string logPath, LogViewerTheme theme)
    {
        _logPath = logPath;
        _theme = theme;

        Text = "vlcshim logs (fallback)";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = true;
        MinimizeBox = false;
        MinimumSize = new Size(760, 360);
        Size = new Size(980, 560);
        Padding = new Padding(1);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        _textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 10.0f, FontStyle.Regular, GraphicsUnit.Point)
        };

        _titleBar = BuildTitleBar();
        _maximizeButton = CreateCaptionButton("□", ToggleMaximizeRestore, Color.FromArgb(_theme.SurfaceArgb), ToOpaqueColor(_theme.StripeArgb));
        _closeButton = CreateCaptionButton("✕", () => Close(), Color.FromArgb(_theme.SurfaceArgb), Color.FromArgb(176, 52, 52));
        _titleLabel = BuildTitleLabel();

        _titleBar.Controls.Add(_closeButton);
        _titleBar.Controls.Add(_maximizeButton);
        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(BuildIconBox());

        Controls.Add(_textBox);
        Controls.Add(_titleBar);

        Resize += (_, __) => UpdateCaptionButtons();
        UpdateCaptionButtons();
        ApplyConfig(theme);
        Shown += (_, _) => LoadExistingLines();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public void AppendLine(string line)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action<string>(AppendLine), line);
            }
            catch
            {
            }

            return;
        }

        string text = _textBox.TextLength == 0 ? line : $"{Environment.NewLine}{line}";
        _textBox.AppendText(text);
        TrimTextIfNeeded();
        _textBox.SelectionStart = _textBox.TextLength;
        _textBox.ScrollToCaret();
    }

    public void LoadSnapshot(IReadOnlyCollection<string> lines)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action<IReadOnlyCollection<string>>(LoadSnapshot), lines);
            }
            catch
            {
            }

            return;
        }

        if (lines.Count == 0)
        {
            return;
        }

        _textBox.Text = string.Join(Environment.NewLine, lines);
        _textBox.SelectionStart = _textBox.TextLength;
        _textBox.ScrollToCaret();
    }

    public void ApplyConfig(LogViewerTheme theme)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action<LogViewerTheme>(ApplyConfig), theme);
            }
            catch
            {
            }

            return;
        }

        _theme = theme;
        BackColor = Color.FromArgb(_theme.SurfaceArgb);
        _textBox.BackColor = Color.FromArgb(_theme.BackgroundArgb);
        _textBox.ForeColor = Color.FromArgb(_theme.ForegroundArgb);
        ApplyTitleBarTheme();
        ApplyWindowChromeTheme();
        Invalidate(true);
    }

    public void RequestClose()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action(RequestClose));
            }
            catch
            {
            }

            return;
        }

        Close();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateMaximizedBounds();
        ApplyWindowChromeTheme();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        if (WindowState == FormWindowState.Normal)
        {
            UpdateMaximizedBounds();
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST && WindowState != FormWindowState.Maximized)
        {
            base.WndProc(ref m);
            if ((int)m.Result == 1)
            {
                Point clientPoint = PointToClient(GetPointFromLParam(m.LParam));
                m.Result = (IntPtr)GetResizeHitTest(clientPoint);
                if ((int)m.Result != 1)
                {
                    return;
                }
            }

            return;
        }

        base.WndProc(ref m);
    }

    private Panel BuildTitleBar()
    {
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 42,
            BackColor = Color.FromArgb(_theme.SurfaceArgb),
            Padding = new Padding(12, 6, 6, 6)
        };

        titleBar.MouseDown += HandleTitleBarMouseDown;
        titleBar.DoubleClick += (_, _) => ToggleMaximizeRestore();
        return titleBar;
    }

    private Control BuildIconBox()
    {
        var iconBox = new PictureBox
        {
            Dock = DockStyle.Left,
            Width = 28,
            Margin = new Padding(0, 0, 10, 0),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Image = Icon?.ToBitmap()
        };

        iconBox.MouseDown += HandleTitleBarMouseDown;
        return iconBox;
    }

    private Label BuildTitleLabel()
    {
        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = Text,
            ForeColor = Color.FromArgb(_theme.ForegroundArgb),
            Font = new Font("Segoe UI Semibold", 10.0f, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };

        titleLabel.MouseDown += HandleTitleBarMouseDown;
        titleLabel.DoubleClick += (_, _) => ToggleMaximizeRestore();
        return titleLabel;
    }

    private Button CreateCaptionButton(string text, Action onClick, Color baseColor, Color hoverColor)
    {
        var button = new Button
        {
            Dock = DockStyle.Right,
            Width = 42,
            FlatStyle = FlatStyle.Flat,
            BackColor = baseColor,
            ForeColor = Color.FromArgb(_theme.ForegroundArgb),
            Font = new Font("Segoe UI Symbol", 10f, FontStyle.Regular, GraphicsUnit.Point),
            Text = text,
            TabStop = false
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseDownBackColor = hoverColor;
        button.FlatAppearance.MouseOverBackColor = hoverColor;
        button.Click += (_, _) => onClick();
        return button;
    }

    private void ToggleMaximizeRestore()
    {
        UpdateMaximizedBounds();
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
        UpdateCaptionButtons();
    }

    private void UpdateCaptionButtons()
    {
        _maximizeButton.Text = WindowState == FormWindowState.Maximized ? "❐" : "□";
    }

    private void HandleTitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
    }

    private void LoadExistingLines()
    {
        try
        {
            if (!File.Exists(_logPath))
            {
                return;
            }

            var queue = new Queue<string>();
            foreach (string line in File.ReadLines(_logPath, Encoding.UTF8))
            {
                queue.Enqueue(line);
                if (queue.Count > InitialLineCount)
                {
                    queue.Dequeue();
                }
            }

            if (queue.Count == 0)
            {
                return;
            }

            _textBox.Text = string.Join(Environment.NewLine, queue);
            _textBox.SelectionStart = _textBox.TextLength;
            _textBox.ScrollToCaret();
        }
        catch
        {
        }
    }

    private void TrimTextIfNeeded()
    {
        if (_textBox.TextLength <= LogViewerForm.MaxDisplayedChars)
        {
            return;
        }

        int excess = _textBox.TextLength - LogViewerForm.MaxDisplayedChars;
        int trimLength = Math.Min(excess + 4096, _textBox.TextLength);
        _textBox.Select(0, trimLength);
        _textBox.SelectedText = string.Empty;
    }

    private void ApplyWindowChromeTheme()
    {
        try
        {
            int enabled = 1;
            int background = ToColorRef(Color.FromArgb(_theme.SurfaceArgb));
            int foreground = ToColorRef(Color.FromArgb(_theme.ForegroundArgb));

            DwmSetWindowAttribute(Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
            DwmSetWindowAttribute(Handle, DwmwaCaptionColor, ref background, sizeof(int));
            DwmSetWindowAttribute(Handle, DwmwaBorderColor, ref background, sizeof(int));
            DwmSetWindowAttribute(Handle, DwmwaTextColor, ref foreground, sizeof(int));
        }
        catch
        {
        }
    }

    private void ApplyTitleBarTheme()
    {
        Color surface = Color.FromArgb(_theme.SurfaceArgb);
        Color foreground = Color.FromArgb(_theme.ForegroundArgb);
        Color hover = ToOpaqueColor(_theme.StripeArgb);

        _titleBar.BackColor = surface;
        _titleLabel.ForeColor = foreground;
        _maximizeButton.BackColor = surface;
        _maximizeButton.ForeColor = foreground;
        _maximizeButton.FlatAppearance.MouseDownBackColor = hover;
        _maximizeButton.FlatAppearance.MouseOverBackColor = hover;
        _closeButton.BackColor = surface;
        _closeButton.ForeColor = foreground;
    }

    private void UpdateMaximizedBounds()
    {
        Screen screen = IsHandleCreated
            ? Screen.FromHandle(Handle)
            : Screen.FromPoint(Location);
        MaximizedBounds = screen.WorkingArea;
    }

    private static int ToColorRef(Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }

    private static Color ToOpaqueColor(int argb)
    {
        uint color = unchecked((uint)argb);
        return Color.FromArgb(
            255,
            (int)((color >> 16) & 0xFF),
            (int)((color >> 8) & 0xFF),
            (int)(color & 0xFF));
    }

    private static Point GetPointFromLParam(IntPtr lParam)
    {
        int value = lParam.ToInt32();
        return new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
    }

    private int GetResizeHitTest(Point point)
    {
        bool left = point.X <= ResizeBorderThickness;
        bool right = point.X >= ClientSize.Width - ResizeBorderThickness;
        bool top = point.Y <= ResizeBorderThickness;
        bool bottom = point.Y >= ClientSize.Height - ResizeBorderThickness;

        if (left && top) return HTTOPLEFT;
        if (right && top) return HTTOPRIGHT;
        if (left && bottom) return HTBOTTOMLEFT;
        if (right && bottom) return HTBOTTOMRIGHT;
        if (left) return HTLEFT;
        if (right) return HTRIGHT;
        if (top) return HTTOP;
        if (bottom) return HTBOTTOM;

        return 1;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
