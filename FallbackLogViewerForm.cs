using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VlcShimDebugFr;

internal sealed class FallbackLogViewerForm : Form
{
    private const int InitialLineCount = 200;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    private readonly string _logPath;
    private readonly TextBox _textBox;
    private LogViewerTheme _theme;

    public FallbackLogViewerForm(string logPath, LogViewerTheme theme)
    {
        _logPath = logPath;
        _theme = theme;

        Text = "VLC Shim Logs (Fallback)";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 360);
        Size = new Size(980, 560);
        Padding = new Padding(1);

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

        Controls.Add(_textBox);
        ApplyConfig(theme);
        Shown += (_, _) => LoadExistingLines();
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
        ApplyWindowChromeTheme();
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

    private static int ToColorRef(Color color)
    {
        return color.R | (color.G << 8) | (color.B << 16);
    }
}
