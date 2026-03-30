using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VlcShimDebugFr;

internal sealed class LogViewerForm : Form
{
    private const int MaxDisplayedChars = 200_000;
    private const int InitialLineCount = 200;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    private readonly string _logPath;
    private readonly TextBox _textBox;
    private readonly LogViewerTheme _theme;

    public LogViewerForm(string logPath, LogViewerTheme theme)
    {
        _logPath = logPath;
        _theme = theme;

        Text = "VLC Shim Logs";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 360);
        Size = new Size(980, 560);
        BackColor = Color.FromArgb(_theme.BackgroundArgb);

        _textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BackColor = Color.FromArgb(_theme.BackgroundArgb),
            ForeColor = Color.FromArgb(_theme.ForegroundArgb),
            BorderStyle = BorderStyle.None,
            Font = CreateFont()
        };
        _textBox.HideSelection = false;

        Controls.Add(_textBox);
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

        if (_textBox.TextLength > 0)
        {
            _textBox.AppendText(Environment.NewLine);
        }

        _textBox.AppendText(line);

        if (_textBox.TextLength > MaxDisplayedChars)
        {
            _textBox.Text = _textBox.Text[^MaxDisplayedChars..];
        }

        _textBox.SelectionStart = _textBox.TextLength;
        _textBox.ScrollToCaret();
        ApplySelectionTheme();
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

            _textBox.Lines = queue.ToArray();
            _textBox.SelectionStart = _textBox.TextLength;
            _textBox.ScrollToCaret();
            ApplySelectionTheme();
        }
        catch
        {
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyWindowChromeTheme();
    }

    private void ApplySelectionTheme()
    {
        // WinForms TextBox has no direct selection color API; setting focus highlights
        // with the system selection color. We still keep the theme's intended contrast
        // by selecting from the end with the control unfocused.
        if (ContainsFocus)
        {
            return;
        }

        _textBox.DeselectAll();
    }

    private void ApplyWindowChromeTheme()
    {
        try
        {
            int enabled = 1;
            int background = ToColorRef(Color.FromArgb(_theme.BackgroundArgb));
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

    private static Font CreateFont()
    {
        foreach (string candidate in new[] { "Cascadia Mono", "Cascadia Code", "Consolas" })
        {
            try
            {
                return new Font(candidate, 9f, FontStyle.Regular);
            }
            catch
            {
            }
        }

        Font fallback = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        return new Font(fallback.FontFamily, 9f, FontStyle.Regular);
    }
}
