using Avalonia.Win32.Interoperability;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VlcShimDebugFr.AvaloniaUi;
using VlcShimDebugFr.AvaloniaUi.ViewModels;
using VlcShimDebugFr.AvaloniaUi.Views;

namespace VlcShimDebugFr;

internal sealed class ConfigForm : Form
{
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

    private readonly WinFormsAvaloniaControlHost _host;
    private readonly SettingsViewModel _viewModel;
    private readonly Panel _titleBar;
    private readonly Label _titleLabel;
    private readonly Button _maximizeButton;
    private readonly Button _closeButton;
    private LogViewerTheme _theme;

    public ConfigForm(ShimConfig config)
    {
        Config = ConfigStore.Clone(config);
        _theme = LogViewerThemes.Get(Config.LogViewerThemeId);
        AvaloniaBootstrap.EnsureInitialized();

        Text = "vlcshim settings";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(1100, 720);
        ClientSize = new Size(1260, 820);
        BackColor = Color.FromArgb(_theme.SurfaceArgb);
        Padding = new Padding(1);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        _viewModel = new SettingsViewModel(Config);
        _viewModel.SaveRequested += HandleSaveRequested;
        _viewModel.CancelRequested += HandleCancelRequested;
        _viewModel.ThemePreviewChanged += HandleThemePreviewChanged;

        _titleBar = BuildTitleBar();
        _maximizeButton = CreateCaptionButton("□", ToggleMaximizeRestore, Color.FromArgb(_theme.SurfaceArgb), ToOpaqueColor(_theme.StripeArgb));
        _closeButton = CreateCaptionButton("✕", () => Close(), Color.FromArgb(_theme.SurfaceArgb), Color.FromArgb(176, 52, 52));
        _titleLabel = BuildTitleLabel();

        _host = new WinFormsAvaloniaControlHost
        {
            Dock = DockStyle.Fill,
            Content = new SettingsView
            {
                DataContext = _viewModel
            }
        };

        _titleBar.Controls.Add(_closeButton);
        _titleBar.Controls.Add(_maximizeButton);
        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(BuildIconBox());

        Controls.Add(_host);
        Controls.Add(_titleBar);

        Resize += (_, __) => UpdateCaptionButtons();
        UpdateCaptionButtons();
        ApplyThemePreview(_theme);
    }

    public ShimConfig Config { get; private set; }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel.SaveRequested -= HandleSaveRequested;
            _viewModel.CancelRequested -= HandleCancelRequested;
            _viewModel.ThemePreviewChanged -= HandleThemePreviewChanged;
        }

        base.Dispose(disposing);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateMaximizedBounds();
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        if (WindowState == FormWindowState.Normal)
        {
            UpdateMaximizedBounds();
        }
    }

    private void HandleSaveRequested(ShimConfig config)
    {
        Config = ConfigStore.Clone(config);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void HandleThemePreviewChanged(LogViewerTheme theme)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action<LogViewerTheme>(HandleThemePreviewChanged), theme);
            return;
        }

        ApplyThemePreview(theme);
    }

    private void HandleCancelRequested()
    {
        DialogResult = DialogResult.Cancel;
        Close();
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
            Height = 46,
            BackColor = Color.FromArgb(_theme.SurfaceArgb),
            Padding = new Padding(12, 8, 8, 8)
        };

        titleBar.MouseDown += HandleTitleBarMouseDown;
        titleBar.DoubleClick += (_, __) => ToggleMaximizeRestore();
        return titleBar;
    }

    private Control BuildIconBox()
    {
        var iconBox = new PictureBox
        {
            Dock = DockStyle.Left,
            Width = 30,
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
            Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Regular, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };

        titleLabel.MouseDown += HandleTitleBarMouseDown;
        titleLabel.DoubleClick += (_, __) => ToggleMaximizeRestore();
        return titleLabel;
    }

    private Button CreateCaptionButton(string text, Action onClick, Color baseColor, Color hoverColor)
    {
        var button = new Button
        {
            Dock = DockStyle.Right,
            Width = 44,
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
        button.Click += (_, __) => onClick();
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

    private void ApplyThemePreview(LogViewerTheme theme)
    {
        _theme = theme;
        Color surface = Color.FromArgb(_theme.SurfaceArgb);
        Color foreground = Color.FromArgb(_theme.ForegroundArgb);
        Color hover = ToOpaqueColor(_theme.StripeArgb);

        BackColor = surface;
        _titleBar.BackColor = surface;
        _titleLabel.ForeColor = foreground;
        _maximizeButton.BackColor = surface;
        _maximizeButton.ForeColor = foreground;
        _maximizeButton.FlatAppearance.MouseDownBackColor = hover;
        _maximizeButton.FlatAppearance.MouseOverBackColor = hover;
        _closeButton.BackColor = surface;
        _closeButton.ForeColor = foreground;
        Invalidate(true);
    }

    private void UpdateMaximizedBounds()
    {
        Screen screen = IsHandleCreated
            ? Screen.FromHandle(Handle)
            : Screen.FromPoint(Location);
        MaximizedBounds = screen.WorkingArea;
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

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
