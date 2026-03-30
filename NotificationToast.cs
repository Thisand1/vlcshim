using System.Drawing.Text;
using System.Media;
using VlcShimDebugFr;
namespace VlcShimDebugFr
{
    internal static class NotificationToast
    {
        private const string Message =
            "Fallback: VLC volume reset to 100%-90%, please use device volume or volume mixer to do volume changes, please do not use VLC volume further when using this shim (also clean off the contents of user/AppData/Local/vlcshimdebugfr from time to time)";

        public static void ShowVolumeWarning()
        {
            Thread toastThread = new Thread(() =>
            {
                using var form = BuildForm(out var closeButton);
                using var soundTimer = new System.Windows.Forms.Timer { Interval = 2000 };
                using var closeTimer = new System.Windows.Forms.Timer { Interval = 16000 };

                soundTimer.Tick += (_, _) => SystemSounds.Asterisk.Play();
                closeTimer.Tick += (_, _) =>
                {
                    soundTimer.Stop();
                    closeTimer.Stop();
                    form.Close();
                };

                closeButton.Click += (_, _) =>
                {
                    var result = MessageBox.Show(form, "Please read the contents of the notification before confirming.", "Close notification?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        soundTimer.Stop();
                        closeTimer.Stop();
                        form.Close();
                    }
                };

                SystemSounds.Asterisk.Play();
                soundTimer.Start();
                closeTimer.Start();

                Application.Run(form);
            });

            toastThread.SetApartmentState(ApartmentState.STA);
            toastThread.IsBackground = true;
            toastThread.Start();
        }

        private static Form BuildForm(out Button closeButton)
        {
            var width = 360;
            var height = 110;
            Rectangle workArea = Screen.PrimaryScreen?.WorkingArea ?? Rectangle.Empty;
            var location = new Point(
                Math.Max(0, workArea.Right - width - 10),
                Math.Max(0, workArea.Bottom - height - 10));

            var form = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(width, height),
                Location = location,
                TopMost = true,
                ShowInTaskbar = false,
                BackColor = Color.FromArgb(0xFD, 0xF6, 0xE3), // Solarized base3
                ForeColor = Color.FromArgb(0x58, 0x6E, 0x75), // Solarized text
                Padding = new Padding(12)
            };

            FontFamily fontFamily = SystemFonts.MessageBoxFont?.FontFamily ?? SystemFonts.DefaultFont.FontFamily;
            // Create one Cascadia font instance and apply it to the whole toast so any nested control inherits it.
            var cascFont = CreateCascadiaFont(9f, FontStyle.Regular, fontFamily);
            form.Font = cascFont;

            var label = new Label
            {
                Dock = DockStyle.Fill,
                Text = Message,
                ForeColor = Color.FromArgb(0x58, 0x6E, 0x75),
                Font = cascFont,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoSize = false
            };

            closeButton = new Button
            {
                Text = "Understood",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                BackColor = Color.FromArgb(0xEE, 0xE8, 0xD5), // base2
                ForeColor = Color.FromArgb(0x58, 0x6E, 0x75),
                FlatStyle = FlatStyle.Flat,
                Font = CreateCascadiaFont(9f, FontStyle.Bold, fontFamily),
                Padding = new Padding(8, 6, 8, 6),
                Margin = new Padding(0, 8, 0, 0)
            };
            closeButton.FlatAppearance.BorderSize = 0;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(label, 0, 0);
            layout.Controls.Add(closeButton, 0, 1);

            form.Controls.Add(layout);

            form.Paint += (_, e) =>
            {
                using var borderPen = new Pen(Color.FromArgb(0x93, 0xA1, 0xA1)); // Solarized base1
                e.Graphics.DrawRectangle(borderPen, 0, 0, form.Width - 1, form.Height - 1);
            };

            return form;
        }

        private static Font CreateCascadiaFont(float size, FontStyle style, FontFamily fallbackFamily)
        {
            try
            {
                using var ifc = new InstalledFontCollection();
                if (ifc.Families.Any(f => f.Name.Equals("Cascadia Code", StringComparison.OrdinalIgnoreCase)))
                {
                    return new Font("Cascadia Code", size, style);
                }
            }
            catch
            {
                // ignore and use fallback
            }

            return new Font(fallbackFamily, size, style);
        }
    }

}