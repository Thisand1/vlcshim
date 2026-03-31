using System.Diagnostics;
using System.IO;
using System.Text;

namespace VlcShimDebugFr;

internal static class VerboseLogger
{
    private static readonly object Sync = new();
    private static StreamWriter? _writer;
    private static Thread? _viewerThread;
    private static LogViewerForm? _viewerForm;
    private static bool _initialized;

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

    public static void StartTail(string logPath, LogViewerTheme theme, ShimConfig config)
    {
        lock (Sync)
        {
            if (_viewerThread is { IsAlive: true })
            {
                return;
            }

            _viewerThread = new Thread(() =>
            {
                using var form = new LogViewerForm(logPath, theme, config);

                lock (Sync)
                {
                    _viewerForm = form;
                }

                Application.Run(form);

                lock (Sync)
                {
                    if (ReferenceEquals(_viewerForm, form))
                    {
                        _viewerForm = null;
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
            _viewerForm?.ApplyConfig(theme, config);
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
        _viewerForm?.AppendLine(line);
    }

    public static void Shutdown()
    {
        Thread? viewerThread;
        LogViewerForm? viewerForm;

        lock (Sync)
        {
            WriteInternal("logger", "INFO", "Logging shutdown");
            _writer?.Dispose();
            _writer = null;
            viewerThread = _viewerThread;
            viewerForm = _viewerForm;
            _viewerThread = null;
        }

        viewerForm?.RequestClose();

        if (viewerThread is not null && viewerThread.IsAlive)
        {
            viewerThread.Join(1500);
        }
    }
}
