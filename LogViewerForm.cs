using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Avalonia.Win32.Interoperability;
using VlcShimDebugFr.AvaloniaUi;
using VlcShimDebugFr.AvaloniaUi.Logs;

namespace VlcShimDebugFr;

internal sealed class LogViewerForm : Form
{
    internal const int MaxDisplayedChars = 200_000;
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
    private readonly WinFormsAvaloniaControlHost _host;
    private readonly LogViewerView _logView;
    private readonly LogViewerViewModel _viewModel;
    private readonly Panel _titleBar;
    private readonly Label _titleLabel;
    private readonly Button _maximizeButton;
    private readonly Button _closeButton;
    private LogViewerTheme _theme;

    public LogViewerForm(string logPath, LogViewerTheme theme, ShimConfig config, IReadOnlyCollection<string> initialLines)
    {
        _logPath = logPath;
        _theme = theme;
        AvaloniaBootstrap.EnsureInitialized();

        Text = "vlcshim logvwr";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = true;
        MinimizeBox = false;
        MinimumSize = new Size(760, 360);
        Size = new Size(980, 560);
        BackColor = Color.FromArgb(_theme.SurfaceArgb);
        Padding = new Padding(1);

        _viewModel = new LogViewerViewModel(_theme, config);
        _logView = new LogViewerView
        {
            DataContext = _viewModel
        };
        _titleBar = BuildTitleBar();
        _maximizeButton = CreateCaptionButton("□", ToggleMaximizeRestore, Color.FromArgb(_theme.SurfaceArgb), ToOpaqueColor(_theme.StripeArgb));
        _closeButton = CreateCaptionButton("✕", () => Close(), Color.FromArgb(_theme.SurfaceArgb), Color.FromArgb(176, 52, 52));
        _titleLabel = BuildTitleLabel();

        _host = new WinFormsAvaloniaControlHost
        {
            Dock = DockStyle.Fill,
            Content = _logView
        };
        _textBox.HideSelection = false;

        _titleBar.Controls.Add(_closeButton);
        _titleBar.Controls.Add(_maximizeButton);
        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(BuildIconBox());

        Controls.Add(_host);
        Controls.Add(_titleBar);

        Resize += (_, __) => UpdateCaptionButtons();
        UpdateCaptionButtons();
        if (initialLines.Count > 0)
        {
            _viewModel.LoadLines(initialLines);
            Shown += (_, _) => _logView.ScrollToEnd();
        }
        else
        {
            Shown += (_, _) => LoadExistingLines();
        }
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

        bool pinToBottom = _logView.IsNearBottom();
        _viewModel.AppendLine(line);
        if (pinToBottom)
        {
            _logView.ScrollToEnd();
        }
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

        _viewModel.LoadLines(lines);
        _logView.ScrollToEnd();
    }

    public void ApplyConfig(LogViewerTheme theme, ShimConfig config)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(new Action<LogViewerTheme, ShimConfig>(ApplyConfig), theme, config);
            }
            catch
            {
            }

            return;
        }

        _theme = theme;
        BackColor = Color.FromArgb(_theme.SurfaceArgb);
        _viewModel.ApplyConfig(theme, config);
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

            _viewModel.LoadLines(queue);
            _logView.ScrollToEnd();
        }
        catch
        {
        }
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
