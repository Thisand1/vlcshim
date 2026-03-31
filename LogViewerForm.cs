using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VlcShimDebugFr;

internal sealed class LogViewerForm : Form
{
    internal const int MaxDisplayedChars = 200_000;
    private const int InitialLineCount = 200;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    private readonly string _logPath;
    private readonly DirectWriteLogControl _logView;
    private LogViewerTheme _theme;

    public LogViewerForm(string logPath, LogViewerTheme theme, ShimConfig config)
    {
        _logPath = logPath;
        _theme = theme;

        Text = "VLC Shim Logs";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 360);
        Size = new Size(980, 560);
        BackColor = Color.FromArgb(_theme.SurfaceArgb);
        Padding = new Padding(1);

        _logView = new DirectWriteLogControl(_theme, config)
        {
            Dock = DockStyle.Fill
        };

        Controls.Add(_logView);
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

        _logView.AppendLine(line);
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
        _logView.ApplyConfig(theme, config);
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

            _logView.LoadLines(queue);
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
