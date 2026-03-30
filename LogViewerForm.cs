using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace VlcShimDebugFr;

internal sealed class LogViewerForm : Form
{
    private const int MaxDisplayedChars = 200_000;
    private const int InitialLineCount = 200;

    private readonly string _logPath;
    private readonly TextBox _textBox;

    public LogViewerForm(string logPath)
    {
        _logPath = logPath;

        Text = "VLC Shim Logs";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 360);
        Size = new Size(980, 560);

        _textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BackColor = Color.FromArgb(20, 22, 31),
            ForeColor = Color.FromArgb(199, 255, 216),
            BorderStyle = BorderStyle.None,
            Font = CreateFont()
        };

        Controls.Add(_textBox);
        Shown += (_, _) => LoadExistingLines();
    }

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
        }
        catch
        {
        }
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
