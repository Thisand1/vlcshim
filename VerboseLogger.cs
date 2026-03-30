using System;
using System.Diagnostics;
using System.IO;
namespace VlcShimDebugFr
{
    internal static class VerboseLogger
    {
        private static readonly object Sync = new();
        private static StreamWriter? _writer;
        private static Process? _tailProcess;
        private static bool _initialized;

        public static void Init(string logPath)
        {
            lock (Sync)
            {
                if (_initialized)
                    return;

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? ".");
                    var stream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    _writer = new StreamWriter(stream)
                    {
                        AutoFlush = true
                    };

                    _initialized = true;
                    WriteInternal("logger", "INFO", $"Logging started -> {logPath}");
                }
                catch (Exception ex)
                {
                    // If we cannot open the log, keep running without crashing.
                    _writer = null;
                    _initialized = false;
                    Debug.WriteLine($"VerboseLogger disabled: {ex.GetType().Name} {ex.Message}");
                }
            }
        }

        public static void StartTail(string logPath)
        {
            if (_tailProcess != null && !_tailProcess.HasExited)
            {
                return;
            }

            try
            {
                string escaped = logPath.Replace("'", "''");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoExit -Command \"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $Host.UI.RawUI.WindowTitle = 'VLC Shim Logs'; Get-Content -Path '{escaped}' -Wait -Tail 20\"",
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                _tailProcess = Process.Start(startInfo);
            }
            catch
            {
                Console.Write("Failed to start log tail.");
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

        private static void WriteInternal(string source, string level, string message)
        {
            if (_writer is null)
                return;

            string line = $"{DateTimeOffset.Now:O} [{level}] [{source}] [T{Environment.CurrentManagedThreadId}] {message}";
            _writer.WriteLine(line);
            Console.WriteLine(line);
            Debug.WriteLine(line);
        }

        public static void Shutdown()
        {
            lock (Sync)
            {
                WriteInternal("logger", "INFO", "Logging shutdown");
                _writer?.Dispose();
                _writer = null;
            }

            try
            {
                if (_tailProcess is { HasExited: false })
                {
                    _tailProcess.Kill(true);
                }
            }
            catch
            {
            }
            finally
            {
                _tailProcess?.Dispose();
                _tailProcess = null;
            }
        }
    }

}
