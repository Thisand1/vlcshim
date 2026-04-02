using System.Diagnostics;
using System.IO;
using System.Text;

namespace VlcShimDebugFr;

internal static class VerboseLogger
{
    private static readonly object Sync = new();
    private static StreamWriter? _writer;
    private static Thread? _viewerThread;
    private static Form? _viewerForm;
    private static Action? _viewerClosedHandler;
    private static bool _initialized;
    private static bool _shutdownRequested;

    public static void Init(string logPath)
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? ".");
                var stream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
                {
                    AutoFlush = true
                };

                _initialized = true;
                WriteInternal("logger", "INFO", $"Logging started -> {logPath}");
            }
            catch (Exception ex)
            {
                _writer = null;
                _initialized = false;
                Debug.WriteLine($"VerboseLogger disabled: {ex.GetType().Name} {ex.Message}");
            }
        }
    }

    public static void StartTail(string logPath, LogViewerTheme theme, ShimConfig config, Action onViewerClosed)
    {
        lock (Sync)
        {
            if (_viewerThread is { IsAlive: true })
            {
                return;
            }

            _shutdownRequested = false;
            _viewerClosedHandler = onViewerClosed;
            _viewerThread = new Thread(() =>
            {
                bool shouldRequestAppExit = false;
                Form? form = null;

                try
                {
                    form = CreateViewerForm(logPath, theme, config);
                    if (form is null)
                    {
                        return;
                    }

                    lock (Sync)
                    {
                        _viewerForm = form;
                    }

                    Application.Run(form);
                }
                finally
                {
                    lock (Sync)
                    {
                        if (ReferenceEquals(_viewerForm, form))
                        {
                            _viewerForm = null;
                        }

                        shouldRequestAppExit = form is not null && !_shutdownRequested;
                    }
                }

                if (shouldRequestAppExit)
                {
                    try
                    {
                        _viewerClosedHandler?.Invoke();
                    }
                    catch
                    {
                    }
                }
            })
            {
                IsBackground = true,
                Name = "VlcShimLogViewer"
            };
            _viewerThread.SetApartmentState(ApartmentState.STA);
            _viewerThread.Start();
        }
    }

    public static void Log(string message)
    {
        lock (Sync)
        {
            WriteInternal("app", "INFO", message);
        }
    }

    public static void Info(string message) => Log(message);

    public static void Error(string message, Exception? ex = null)
    {
        lock (Sync)
        {
            string combined = ex is null ? message : $"{message} :: {ex.GetType().Name}: {ex.Message}";
            WriteInternal("app", "ERROR", combined);
        }
    }

    public static void ApplyViewerConfig(LogViewerTheme theme, ShimConfig config)
    {
        lock (Sync)
        {
            switch (_viewerForm)
            {
                case LogViewerForm viewer:
                    viewer.ApplyConfig(theme, config);
                    break;
                case FallbackLogViewerForm fallbackViewer:
                    fallbackViewer.ApplyConfig(theme);
                    break;
            }
        }
    }

    private static void WriteInternal(string source, string level, string message)
    {
        if (_writer is null)
        {
            return;
        }

        string line = $"{DateTimeOffset.Now:O} [{level}] [{source}] [T{Environment.CurrentManagedThreadId}] {message}";
        _writer.WriteLine(line);
        Debug.WriteLine(line);
        switch (_viewerForm)
        {
            case LogViewerForm viewer:
                viewer.AppendLine(line);
                break;
            case FallbackLogViewerForm fallbackViewer:
                fallbackViewer.AppendLine(line);
                break;
        }
    }

    public static void Shutdown()
    {
        Thread? viewerThread;
        Form? viewerForm;

        lock (Sync)
        {
            _shutdownRequested = true;
            WriteInternal("logger", "INFO", "Logging shutdown");
            _writer?.Dispose();
            _writer = null;
            viewerThread = _viewerThread;
            viewerForm = _viewerForm;
            _viewerThread = null;
            _viewerClosedHandler = null;
        }

        switch (viewerForm)
        {
            case LogViewerForm viewer:
                viewer.RequestClose();
                break;
            case FallbackLogViewerForm fallbackViewer:
                fallbackViewer.RequestClose();
                break;
        }

        if (viewerThread is not null && viewerThread.IsAlive)
        {
            viewerThread.Join(1500);
        }
    }

    private static Form? CreateViewerForm(string logPath, LogViewerTheme theme, ShimConfig config)
    {
        try
        {
            return new LogViewerForm(logPath, theme, config);
        }
        catch (Exception ex)
        {
            Error("🪟 Vortice log viewer unavailable. Falling back to the basic log window.", ex);

            try
            {
                return new FallbackLogViewerForm(logPath, theme);
            }
            catch (Exception fallbackEx)
            {
                Error("🪟 Failed to open the fallback log window. Continuing without a viewer.", fallbackEx);
                return null;
            }
        }
    }
}
