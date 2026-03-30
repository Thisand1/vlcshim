using System.Windows.Forms;
using Windows.Media;
using VlcShimDebugFr;
using System.Text;
using System.Net;

internal static partial class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var config = ConfigStore.Load();
        var identity = ShellIdentity.ApplyConfiguredIdentity(config);
        ApplicationConfiguration.Initialize();

        using var cts = new CancellationTokenSource();
        using var hostSession = new SmtcHostSession();
        var ctx = new ApplicationContext();
        var logPath = GetLogPath();

        VerboseLogger.Init(logPath);
        VerboseLogger.StartTail(logPath);
        VerboseLogger.Info("🚀 VLC shim booting up.");
        VerboseLogger.Info($"🪪 Shell identity {(identity.Applied ? "applied" : "fallback failed")}: {identity.DisplayName} [{identity.AppUserModelId}]");
        VerboseLogger.Info("🎚️ SMTC host session ready.");

        RainmeterAimpBridge? rainmeterBridge = null;
        if (ShouldEnableRainmeterAimpBridge(config))
        {
            try
            {
                rainmeterBridge = new RainmeterAimpBridge(() =>
                {
                    try { cts.Cancel(); } catch { }
                    ctx.ExitThread();
                });
            }
            catch (Exception ex)
            {
                VerboseLogger.Error("🌧️ Failed to start the Rainmeter AIMP bridge.", ex);
            }
        }

        if (config.ShowStartupToast)
        {
            NotificationToast.ShowVolumeWarning();
            VerboseLogger.Info("🔔 Startup warning toast shown.");
        }

        var tray = new NotifyIcon
        {
            Visible = true,
            Text = BuildTrayText(config),
            Icon = System.Drawing.SystemIcons.Application,
        };
        tray.ContextMenuStrip = BuildMenu(tray, cts, ctx, () => config, updated => config = updated);

        ctx.ThreadExit += (_, __) =>
        {
            try { cts.Cancel(); } catch { }
            rainmeterBridge?.Dispose();
            tray.Visible = false;
            tray.Dispose();
            VerboseLogger.Info("🛑 VLC shim shutting down.");
            VerboseLogger.Shutdown();
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await RunBridgeAsync(hostSession.Smtc, rainmeterBridge, args, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                VerboseLogger.Error("💥 Unhandled startup error.", ex);
                MessageBox.Show(ex.ToString(), "VlcShim crashed during init");
                ctx.ExitThread();
                // the crash is a lie
                // portal reference btw
            }
        }, cts.Token);

        Application.Run(ctx);
    }

    private static ContextMenuStrip BuildMenu(
        NotifyIcon tray,
        CancellationTokenSource cts,
        ApplicationContext ctx,
        Func<ShimConfig> getConfig,
        Action<ShimConfig> setConfig)
    {
        var menu = new ContextMenuStrip();
        var configItem = new ToolStripMenuItem("Config...");
        var toastItem = new ToolStripMenuItem("Show warning toast");
        var exit = new ToolStripMenuItem("Exit");

        configItem.Click += (_, __) =>
        {
            using var form = new ConfigForm(getConfig());
            if (form.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            var updatedConfig = ConfigStore.Clone(form.Config);
            ConfigStore.Save(updatedConfig);
            setConfig(updatedConfig);
            tray.Text = BuildTrayText(updatedConfig);

            VerboseLogger.Info($"⚙️ Config saved. Player profile: {PlayerIdentityProfiles.GetDisplayName(updatedConfig)}");
            MessageBox.Show(
                "Settings saved. Restart the shim to apply player identity and compatibility bridge changes.",
                "VLC Shim Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };

        toastItem.Click += (_, __) =>
        {
            NotificationToast.ShowVolumeWarning();
            VerboseLogger.Info("🔔 Warning toast opened from tray.");
        };

        exit.Click += (_, __) =>
        {
            try { cts.Cancel(); } catch { }
            ctx.ExitThread();
        };

        menu.Items.Add(configItem);
        menu.Items.Add(toastItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        return menu;
    }

    private static string BuildTrayText(ShimConfig config)
    {
        string text = $"VlcShim ({PlayerIdentityProfiles.GetDisplayName(config)})";
        return text.Length <= 63 ? text : text[..63];
    }

    private static bool ShouldEnableRainmeterAimpBridge(ShimConfig config)
    {
        return string.Equals(config.PlayerProfileId, "aimp", StringComparison.OrdinalIgnoreCase);
    }

    private static int[] ParsePorts(string[] args)
    {
        var ports = new List<int>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var port))
                {
                    if (IsValidPort(port))
                    {
                        ports.Add(port);
                    }
                }
            }

            if (args[i].Equals("--ports", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                foreach (var rawPort in args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (int.TryParse(rawPort, out var port))
                    {
                        if (IsValidPort(port))
                        {
                            ports.Add(port);
                        }
                    }
                }
            }
        }

        return ports.ToArray();
    }

    private static string? ParsePassword(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string GetLogPath()
    {
        string baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "vlcshimdebugfr",
            "logs");

        return Path.Combine(baseDir, "latest.log");
    }

    private static bool IsValidPort(int port)
    {
        return port is >= IPEndPoint.MinPort and <= IPEndPoint.MaxPort;
    }

    private static async Task RunBridgeAsync(
        SystemMediaTransportControls smtc,
        RainmeterAimpBridge? rainmeterBridge,
        string[] args,
        CancellationToken ct)
    {
        var password = ParsePassword(args) ?? Environment.GetEnvironmentVariable("VLC_HTTP_PASSWORD") ?? "ineedair";
        var extraPorts = ParsePorts(args);
        var portsToProbe = new List<int> { 8080, 4212 };
        portsToProbe.AddRange(extraPorts.Where(p => p > 0));
        VerboseLogger.Info($"🔎 Waiting for VLC HTTP on ports: {string.Join(", ", portsToProbe.Distinct())}");

        while (!ct.IsCancellationRequested)
        {
            VlcHttpClient? vlc = null;
            SmtcShimPublisher? publisher = null;

            try
            {
                vlc = await WaitForVlcAsync(password, extraPorts, ct);
                VerboseLogger.Info($"🔌 Connected to VLC at {vlc.BaseUrl}");
                publisher = new SmtcShimPublisher(smtc, vlc);
                rainmeterBridge?.SetTransport(vlc);
                VerboseLogger.Info("📡 Polling VLC -> SMTC started.");
                await PollLoopAsync(vlc, publisher, rainmeterBridge, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch when (!ct.IsCancellationRequested)
            {
                VerboseLogger.Info("⚠️ VLC connection unavailable. Retrying in 1 second.");
                await Task.Delay(1000, ct);
            }
            finally
            {
                if (publisher is not null)
                {
                    VerboseLogger.Info("🧹 Clearing SMTC session.");
                }
                rainmeterBridge?.SetTransport(null);
                rainmeterBridge?.Clear();
                publisher?.Clear();
                publisher?.Dispose();
                vlc?.Dispose();
            }
        }
    }

    private static async Task<VlcHttpClient> WaitForVlcAsync(string password, int[] ports, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                return await VlcHttpClient.CreateAsync(password, ports, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await Task.Delay(1000, ct);
            }
        }
    }

    private static async Task PollLoopAsync(
        VlcHttpClient vlc,
        SmtcShimPublisher publisher,
        RainmeterAimpBridge? rainmeterBridge,
        CancellationToken ct)
    {
        const int pollMs = 250;

        while (!ct.IsCancellationRequested)
        {
            var json = await vlc.GetStatusJsonAsync(ct);
            var status = StatusShimParser.Parse(json);
            publisher.Update(status);
            rainmeterBridge?.Update(status);
            await Task.Delay(pollMs, ct);
        }
    }
}
