using System.Windows.Forms;
using Windows.Media;
using VlcShimDebugFr;
using System.Text;

internal static partial class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        ShellIdentity.ApplyVlcIdentity();
        ApplicationConfiguration.Initialize();

        using var cts = new CancellationTokenSource();
        using var hostSession = new SmtcHostSession();
        var ctx = new ApplicationContext();
        var logPath = GetLogPath();

        VerboseLogger.Init(logPath);
        VerboseLogger.StartTail(logPath);
        VerboseLogger.Info("🚀 VLC shim booting up.");
        VerboseLogger.Info("🪪 Shell identity applied.");
        VerboseLogger.Info("🎚️ SMTC host session ready.");

        var tray = new NotifyIcon
        {
            Visible = true,
            Text = "VlcShim",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = BuildMenu(cts, ctx)
        };

        ctx.ThreadExit += (_, __) =>
        {
            try { cts.Cancel(); } catch { }
            tray.Visible = false;
            tray.Dispose();
            VerboseLogger.Info("🛑 VLC shim shutting down.");
            VerboseLogger.Shutdown();
        };

        _ = Task.Run(async () =>
        {
            try
            {
                await RunBridgeAsync(hostSession.Smtc, args, cts.Token);
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

    private static ContextMenuStrip BuildMenu(CancellationTokenSource cts, ApplicationContext ctx)
    {
        var menu = new ContextMenuStrip();
        var exit = new ToolStripMenuItem("Exit");

        exit.Click += (_, __) =>
        {
            try { cts.Cancel(); } catch { }
            ctx.ExitThread();
        };

        menu.Items.Add(exit);
        return menu;
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
                    ports.Add(port);
                }
            }

            if (args[i].Equals("--ports", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                foreach (var rawPort in args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (int.TryParse(rawPort, out var port))
                    {
                        ports.Add(port);
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

    private static async Task RunBridgeAsync(SystemMediaTransportControls smtc, string[] args, CancellationToken ct)
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
                VerboseLogger.Info("📡 Polling VLC -> SMTC started.");
                await PollLoopAsync(vlc, publisher, ct);
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

    private static async Task PollLoopAsync(VlcHttpClient vlc, SmtcShimPublisher publisher, CancellationToken ct)
    {
        const int pollMs = 250;

        while (!ct.IsCancellationRequested)
        {
            var json = await vlc.GetStatusJsonAsync(ct);
            publisher.UpdateFromStatusJson(json);
            await Task.Delay(pollMs, ct);
        }
    }
}
