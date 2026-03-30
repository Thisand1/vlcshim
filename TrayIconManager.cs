using System;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Drawing.Text;
using Windows.Media.Control;
using System.Diagnostics;
using VlcShimDebugFr;
using System.Windows.Forms;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
namespace VlcShimDebugFr
{
    internal sealed class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly CancellationTokenSource _cts;

        public event Action? PlayPressed;
        public event Action? PausePressed;
        public event Action? NextPressed;
        public event Action? PreviousPressed;
        public event Action? QuitPressed;

        public TrayIconManager(CancellationTokenSource cts)
        {
            _cts = cts;

            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "VLC Shim",
                Visible = true
            };

            var menu = new ContextMenuStrip();

            menu.Items.Add("Play", null, (_, _) => PlayPressed?.Invoke());
            menu.Items.Add("Pause", null, (_, _) => PausePressed?.Invoke());
            menu.Items.Add("Next", null, (_, _) => NextPressed?.Invoke());
            menu.Items.Add("Previous", null, (_, _) => PreviousPressed?.Invoke());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit", null, (_, _) =>
            {
                QuitPressed?.Invoke();
                _cts.Cancel();
            });

            _notifyIcon.ContextMenuStrip = menu;
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
