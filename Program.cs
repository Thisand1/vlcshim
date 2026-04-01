using System.Windows.Forms;
using Windows.Media;
using VlcShimDebugFr;
using System.Text;
using System.Net;
using System.Drawing;

internal static partial class Program
{
    private sealed class IconHolder
    {
        public IconHolder(Icon icon)
        {
            Icon = icon;
        }

        public Icon Icon { get; set; }
    }

    private sealed class VlcConnectionSettings
    {
        public required string Password { get; init; }

        public required int[] ExtraPorts { get; init; }

        public required string PortsKey { get; init; }
    }

    [STAThread]
    private static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var config = ConfigStore.Load();
        var identity = ShellIdentity.ApplyConfiguredIdentity(config);
        ApplicationConfiguration.Initialize();

        using var cts = new CancellationTokenSource();
        using var hostSession = new SmtcHostSession();
        var trayIconHolder = new IconHolder(PlayerIcons.LoadForProfile(config));
        using var rainmeterBridgeController = new RainmeterBridgeController();
        var ctx = new ApplicationContext();
        var logPath = GetLogPath();

        VerboseLogger.Init(logPath);
        VerboseLogger.StartTail(logPath, LogViewerThemes.Get(config.LogViewerThemeId), config);
        VerboseLogger.Info("🚀 VLC shim booting up.");
        VerboseLogger.Info($"🪪 Shell identity {(identity.Applied ? "applied" : "fallback failed")}: {identity.DisplayName} [{identity.AppUserModelId}]");
        VerboseLogger.Info("🎚️ SMTC host session ready.");

        if (ShouldEnableRainmeterAimpBridge(config))
        {
            try
            {
                rainmeterBridgeController.Replace(CreateRainmeterBridge(cts, ctx));
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
            Icon = trayIconHolder.Icon,
        };
        tray.ContextMenuStrip = BuildMenu(
            tray,
            cts,
            ctx,
            () => config,
            updated => config = updated,
            (previousConfig, updatedConfig) => ApplyRuntimeConfig(previousConfig, updatedConfig, tray, trayIconHolder, rainmeterBridgeController, cts, ctx));

        ctx.ThreadExit += (_, __) =>
        {
            try { cts.Cancel(); } catch { }
            rainmeterBridgeController.Dispose();
            tray.Visible = false;
            tray.Dispose();
            trayIconHolder.Icon.Dispose();
            VerboseLogger.Info("🛑 VLC shim shutting down.");
            VerboseLogger.Shutdown();
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await RunBridgeAsync(hostSession.Smtc, rainmeterBridgeController, args, () => config, cts.Token);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
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
        Action<ShimConfig> setConfig,
        Action<ShimConfig, ShimConfig> applyRuntimeConfig)
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

            var previousConfig = ConfigStore.Clone(getConfig());
            var updatedConfig = ConfigStore.Clone(form.Config);
            ConfigStore.Save(updatedConfig);
            setConfig(updatedConfig);
            applyRuntimeConfig(previousConfig, updatedConfig);

            VerboseLogger.Info($"⚙️ Config saved. Player profile: {PlayerIdentityProfiles.GetDisplayName(updatedConfig)}");
            if (DidConnectionSettingsChange(previousConfig, updatedConfig))
            {
                VerboseLogger.Info("⚙️ VLC HTTP connection settings changed. Reconnecting if needed.");
            }
            if (DidIdentitySettingsChange(previousConfig, updatedConfig))
            {
                VerboseLogger.Info("⚙️ Identity-related settings changed. Windows shell surfaces may refresh lazily.");
            }
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
        RainmeterBridgeController rainmeterBridgeController,
        string[] args,
        Func<ShimConfig> getConfig,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            VlcHttpClient? vlc = null;
            SmtcShimPublisher? publisher = null;
            VlcConnectionSettings? connectionSettings = null;

            try
            {
                (vlc, connectionSettings) = await WaitForVlcAsync(args, getConfig, ct);
                VerboseLogger.Info($"🔌 Connected to VLC at {vlc.BaseUrl}");
                publisher = new SmtcShimPublisher(smtc, vlc);
                rainmeterBridgeController.SetTransport(vlc);
                VerboseLogger.Info("📡 Polling VLC -> SMTC started.");
                await PollLoopAsync(vlc, publisher, rainmeterBridgeController, () => ResolveConnectionSettings(args, getConfig), connectionSettings, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch when (!ct.IsCancellationRequested)
            {
                VerboseLogger.Info("⚠️ VLC connection lost. Re-probing in 1 second.");
                await Task.Delay(1000, ct);
            }
            finally
            {
                if (publisher is not null)
                {
                    VerboseLogger.Info("🧹 Clearing SMTC session.");
                }
                rainmeterBridgeController.SetTransport(null);
                rainmeterBridgeController.Clear();
                publisher?.Clear();
                publisher?.Dispose();
                vlc?.Dispose();
            }
        }
    }

    private static async Task<(VlcHttpClient Client, VlcConnectionSettings Settings)> WaitForVlcAsync(
        string[] args,
        Func<ShimConfig> getConfig,
        CancellationToken ct)
    {
        VlcConnectionSettings? lastLoggedSettings = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            VlcConnectionSettings settings = ResolveConnectionSettings(args, getConfig);
            if (!HasSameConnectionSettings(lastLoggedSettings, settings))
            {
                VerboseLogger.Info($"🔎 Waiting for VLC HTTP on ports: {string.Join(", ", GetPortsToProbe(settings))}");
                lastLoggedSettings = settings;
            }

            try
            {
                return (await VlcHttpClient.CreateAsync(settings.Password, settings.ExtraPorts, ct), settings);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
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
        RainmeterBridgeController rainmeterBridgeController,
        Func<VlcConnectionSettings> getConnectionSettings,
        VlcConnectionSettings connectedSettings,
        CancellationToken ct)
    {
        const int pollMs = 250;

        while (!ct.IsCancellationRequested)
        {
            if (!HasSameConnectionSettings(connectedSettings, getConnectionSettings()))
            {
                VerboseLogger.Info("⚙️ VLC HTTP connection settings changed. Reconnecting now.");
                return;
            }

            var json = await vlc.GetStatusJsonAsync(ct);
            var status = StatusShimParser.Parse(json);
            publisher.Update(status);
            rainmeterBridgeController.Update(status);
            await Task.Delay(pollMs, ct);
        }
    }

    private static void ApplyRuntimeConfig(
        ShimConfig previousConfig,
        ShimConfig updatedConfig,
        NotifyIcon tray,
        IconHolder trayIconHolder,
        RainmeterBridgeController rainmeterBridgeController,
        CancellationTokenSource cts,
        ApplicationContext ctx)
    {
        ApplyTrayConfig(tray, trayIconHolder, updatedConfig);
        VerboseLogger.ApplyViewerConfig(LogViewerThemes.Get(updatedConfig.LogViewerThemeId), updatedConfig);
        ApplyRainmeterBridgeConfig(previousConfig, updatedConfig, rainmeterBridgeController, cts, ctx);

        if (DidIdentitySettingsChange(previousConfig, updatedConfig))
        {
            var identity = ShellIdentity.ApplyConfiguredIdentity(updatedConfig);
            VerboseLogger.Info($"🪪 Shell identity live refresh {(identity.Applied ? "applied" : "failed")}: {identity.DisplayName} [{identity.AppUserModelId}]");
        }
    }

    private static void ApplyTrayConfig(NotifyIcon tray, IconHolder trayIconHolder, ShimConfig config)
    {
        tray.Text = BuildTrayText(config);

        Icon newIcon = PlayerIcons.LoadForProfile(config);
        Icon oldIcon = trayIconHolder.Icon;
        trayIconHolder.Icon = newIcon;
        tray.Icon = newIcon;
        oldIcon.Dispose();
    }

    private static void ApplyRainmeterBridgeConfig(
        ShimConfig previousConfig,
        ShimConfig updatedConfig,
        RainmeterBridgeController rainmeterBridgeController,
        CancellationTokenSource cts,
        ApplicationContext ctx)
    {
        bool wasEnabled = ShouldEnableRainmeterAimpBridge(previousConfig);
        bool shouldEnable = ShouldEnableRainmeterAimpBridge(updatedConfig);

        if (wasEnabled == shouldEnable)
        {
            return;
        }

        if (!shouldEnable)
        {
            rainmeterBridgeController.Replace(null);
            VerboseLogger.Info("🌧️ Rainmeter AIMP bridge disabled live.");
            return;
        }

        try
        {
            rainmeterBridgeController.Replace(CreateRainmeterBridge(cts, ctx));
            VerboseLogger.Info("🌧️ Rainmeter AIMP bridge enabled live.");
        }
        catch (Exception ex)
        {
            VerboseLogger.Error("🌧️ Failed to enable the Rainmeter AIMP bridge live.", ex);
            MessageBox.Show(
                $"The Rainmeter AIMP bridge could not be enabled live.\n\n{ex.Message}",
                "VLC Shim Settings",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static RainmeterAimpBridge CreateRainmeterBridge(CancellationTokenSource cts, ApplicationContext ctx)
    {
        return new RainmeterAimpBridge(() =>
        {
            try { cts.Cancel(); } catch { }
            ctx.ExitThread();
        });
    }

    private static bool DidIdentitySettingsChange(ShimConfig previousConfig, ShimConfig updatedConfig)
    {
        return !string.Equals(previousConfig.PlayerProfileId, updatedConfig.PlayerProfileId, StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(previousConfig.CustomPlayerDisplayName, updatedConfig.CustomPlayerDisplayName, StringComparison.Ordinal) ||
               !string.Equals(previousConfig.CustomAppUserModelId, updatedConfig.CustomAppUserModelId, StringComparison.Ordinal);
    }

    private static bool DidConnectionSettingsChange(ShimConfig previousConfig, ShimConfig updatedConfig)
    {
        return !string.Equals(previousConfig.VlcHttpPassword, updatedConfig.VlcHttpPassword, StringComparison.Ordinal) ||
               !string.Equals(previousConfig.VlcHttpPorts, updatedConfig.VlcHttpPorts, StringComparison.Ordinal);
    }

    private static VlcConnectionSettings ResolveConnectionSettings(string[] args, Func<ShimConfig> getConfig)
    {
        ShimConfig config = getConfig();
        string password =
            ParsePassword(args) ??
            Environment.GetEnvironmentVariable("VLC_HTTP_PASSWORD") ??
            (string.IsNullOrWhiteSpace(config.VlcHttpPassword) ? null : config.VlcHttpPassword) ??
            "ineedair";

        int[] extraPorts = ParsePorts(args);
        if (extraPorts.Length == 0)
        {
            extraPorts = ParsePortsValue(config.VlcHttpPorts);
        }

        return new VlcConnectionSettings
        {
            Password = password,
            ExtraPorts = extraPorts,
            PortsKey = string.Join(",", extraPorts)
        };
    }

    private static bool HasSameConnectionSettings(VlcConnectionSettings? left, VlcConnectionSettings? right)
    {
        return left is not null &&
               right is not null &&
               string.Equals(left.Password, right.Password, StringComparison.Ordinal) &&
               string.Equals(left.PortsKey, right.PortsKey, StringComparison.Ordinal);
    }

    private static IEnumerable<int> GetPortsToProbe(VlcConnectionSettings settings)
    {
        var ports = new List<int> { 8080, 4212 };
        ports.AddRange(settings.ExtraPorts.Where(p => p > 0));
        return ports.Distinct();
    }

    private static int[] ParsePortsValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Array.Empty<int>();
        }

        var ports = new List<int>();
        foreach (string rawPort in rawValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(rawPort, out var port) && IsValidPort(port) && !ports.Contains(port))
            {
                ports.Add(port);
            }
        }

        return ports.ToArray();
    }
}
