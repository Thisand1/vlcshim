using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Windows.Media;

namespace VlcShimDebugFr;

internal sealed class RainmeterAimpBridge : IDisposable
{
    private const string AimpRemoteInfoName = "AIMP2_RemoteInfo";
    private const string WinampWindowClass = "Winamp v1.x";
    private const int FileMapSize = 2048;
    private const int HeaderSize = 88;
    private const int VlcFullVolume = 256;

    private const uint WM_USER = 0x0400;
    private const int WM_CLOSE = 0x0010;
    private const int WM_AIMP_COMMAND = unchecked((int)(WM_USER + 0x75));
    private const int WM_WA_IPC = unchecked((int)WM_USER);

    private const int WM_AIMP_STATUS_GET = 1;
    private const int WM_AIMP_STATUS_SET = 2;
    private const int WM_AIMP_CALLFUNC = 3;

    private const int AIMP_STS_VOLUME = 1;
    private const int AIMP_STS_PLAYER = 4;
    private const int AIMP_STS_REPEAT = 29;
    private const int AIMP_STS_POS = 31;
    private const int AIMP_STS_LENGTH = 32;
    private const int AIMP_STS_SHUFFLE = 41;

    private const int AIMP_PLAY = 15;
    private const int AIMP_PAUSE = 16;
    private const int AIMP_STOP = 17;
    private const int AIMP_NEXT = 18;
    private const int AIMP_PREV = 19;

    private const int IPC_SETRATING = 639;
    private const int IPC_GETRATING = 640;

    private const uint FILE_MAP_READ = 0x0004;
    private const uint FILE_MAP_WRITE = 0x0002;
    private const uint PAGE_READWRITE = 0x04;

    private readonly object _sync = new();
    private readonly BridgeWindow _aimpWindow;
    private readonly BridgeWindow _winampWindow;
    private readonly Action _requestExit;
    private readonly byte[] _mapBuffer = new byte[FileMapSize];

    private IntPtr _fileMapHandle;
    private IntPtr _fileMapView;
    private StatusShim _status = StatusShim.CreateStopped();
    private VlcHttpClient? _vlc;
    private int _rating;
    private ulong _playbackSessionVersion = 1;
    private bool _disposed;

    public RainmeterAimpBridge(Action requestExit)
    {
        _requestExit = requestExit;

        if (FindWindow(AimpRemoteInfoName, AimpRemoteInfoName) != IntPtr.Zero)
        {
            throw new InvalidOperationException("An existing AIMP-compatible remote window is already running.");
        }

        _fileMapHandle = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, PAGE_READWRITE, 0, FileMapSize, AimpRemoteInfoName);
        if (_fileMapHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create the AIMP remote shared memory block.");
        }

        _fileMapView = MapViewOfFile(_fileMapHandle, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, new UIntPtr(FileMapSize));
        if (_fileMapView == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to map the AIMP remote shared memory block.");
        }

        _aimpWindow = new BridgeWindow(AimpRemoteInfoName, AimpRemoteInfoName, HandleAimpWindowMessage);
        _winampWindow = new BridgeWindow(WinampWindowClass, WinampWindowClass, HandleWinampWindowMessage);

        WriteMapLocked();
        VerboseLogger.Info("🌧️ Rainmeter AIMP bridge ready.");
    }

    public void SetTransport(VlcHttpClient? vlc)
    {
        lock (_sync)
        {
            _vlc = vlc;
        }
    }

    public void Update(StatusShim status)
    {
        lock (_sync)
        {
            bool wasStopped = IsStopped(_status.State);
            bool isStopped = IsStopped(status.State);
            if (wasStopped && !isStopped)
            {
                _playbackSessionVersion++;
            }

            _status = status;
            WriteMapLocked();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _status = StatusShim.CreateStopped();
            WriteMapLocked();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _winampWindow.Dispose();
        _aimpWindow.Dispose();

        if (_fileMapView != IntPtr.Zero)
        {
            UnmapViewOfFile(_fileMapView);
            _fileMapView = IntPtr.Zero;
        }

        if (_fileMapHandle != IntPtr.Zero)
        {
            CloseHandle(_fileMapHandle);
            _fileMapHandle = IntPtr.Zero;
        }
    }

    private IntPtr HandleAimpWindowMessage(ref Message message)
    {
        switch (message.Msg)
        {
            case WM_AIMP_COMMAND:
                message.Result = HandleAimpCommand(message.WParam, message.LParam);
                return message.Result;
            case WM_CLOSE:
                _requestExit();
                message.Result = IntPtr.Zero;
                return message.Result;
            default:
                return IntPtr.Zero;
        }
    }

    private IntPtr HandleWinampWindowMessage(ref Message message)
    {
        if (message.Msg != WM_WA_IPC)
        {
            return IntPtr.Zero;
        }

        int command = message.LParam.ToInt32();
        lock (_sync)
        {
            if (command == IPC_GETRATING)
            {
                message.Result = new IntPtr(_rating);
                return message.Result;
            }

            if (command == IPC_SETRATING)
            {
                _rating = Math.Clamp(message.WParam.ToInt32(), 0, 5);
                WriteMapLocked();
                message.Result = new IntPtr(_rating);
                return message.Result;
            }
        }

        return IntPtr.Zero;
    }

    private IntPtr HandleAimpCommand(IntPtr wParam, IntPtr lParam)
    {
        int action = wParam.ToInt32();
        switch (action)
        {
            case WM_AIMP_STATUS_GET:
                return new IntPtr(GetStatusValue(lParam.ToInt32()));
            case WM_AIMP_STATUS_SET:
                ApplyStatusSet(lParam.ToInt32());
                return IntPtr.Zero;
            case WM_AIMP_CALLFUNC:
                ApplyCallFunction(lParam.ToInt32());
                return IntPtr.Zero;
            default:
                return IntPtr.Zero;
        }
    }

    private int GetStatusValue(int statusType)
    {
        lock (_sync)
        {
            return statusType switch
            {
                AIMP_STS_VOLUME => _status.Volume,
                AIMP_STS_PLAYER => TranslateState(_status.State),
                AIMP_STS_REPEAT => _status.RepeatMode != MediaPlaybackAutoRepeatMode.None ? 1 : 0,
                AIMP_STS_POS => _status.Time,
                AIMP_STS_LENGTH => _status.Length,
                AIMP_STS_SHUFFLE => _status.IsShuffleEnabled ? 1 : 0,
                _ => 0
            };
        }
    }

    private void ApplyStatusSet(int packedValue)
    {
        int value = packedValue & 0xFFFF;
        int statusType = (packedValue >> 16) & 0xFFFF;

        _ = statusType switch
        {
            AIMP_STS_POS => SetPositionAsync(value),
            AIMP_STS_VOLUME => SetVolumeAsync(value),
            AIMP_STS_SHUFFLE => SetShuffleAsync(value != 0),
            AIMP_STS_REPEAT => SetRepeatAsync(value != 0),
            _ => Task.CompletedTask
        };
    }

    private void ApplyCallFunction(int functionId)
    {
        _ = functionId switch
        {
            AIMP_PLAY => SendCommandAsync("pl_play"),
            AIMP_PAUSE => SendCommandAsync("pl_pause"),
            AIMP_STOP => SendCommandAsync("pl_stop"),
            AIMP_NEXT => SendCommandAsync("pl_next"),
            AIMP_PREV => SendCommandAsync("pl_previous"),
            _ => Task.CompletedTask
        };
    }

    private async Task SetPositionAsync(int positionSeconds)
    {
        VlcHttpClient? vlc;
        int length;

        lock (_sync)
        {
            vlc = _vlc;
            length = _status.Length;
        }

        if (vlc is null || length <= 0)
        {
            return;
        }

        double percent = Math.Clamp(positionSeconds * 100d / length, 0d, 100d);
        await vlc.SeekAsync(percent);
    }

    private async Task SetVolumeAsync(int volume)
    {
        VlcHttpClient? vlc;
        lock (_sync)
        {
            vlc = _vlc;
        }

        if (vlc is null)
        {
            return;
        }

        int rawVolume = Math.Clamp((int)Math.Round(Math.Clamp(volume, 0, 100) * VlcFullVolume / 100d), 0, VlcFullVolume);
        await vlc.SendCommandAsync("volume", new Dictionary<string, string>
        {
            ["val"] = rawVolume.ToString()
        });
    }

    private async Task SetShuffleAsync(bool enabled)
    {
        VlcHttpClient? vlc;
        bool currentValue;

        lock (_sync)
        {
            vlc = _vlc;
            currentValue = _status.IsShuffleEnabled;
        }

        if (vlc is null || currentValue == enabled)
        {
            return;
        }

        await vlc.SendCommandAsync("pl_random");
    }

    private async Task SetRepeatAsync(bool enabled)
    {
        VlcHttpClient? vlc;
        bool currentValue;

        lock (_sync)
        {
            vlc = _vlc;
            currentValue = _status.RepeatMode != MediaPlaybackAutoRepeatMode.None;
        }

        if (vlc is null || currentValue == enabled)
        {
            return;
        }

        await vlc.SendCommandAsync("pl_repeat");
    }

    private Task SendCommandAsync(string command)
    {
        VlcHttpClient? vlc;
        lock (_sync)
        {
            vlc = _vlc;
        }

        return vlc is null ? Task.CompletedTask : vlc.SendCommandAsync(command);
    }

    private void WriteMapLocked()
    {
        if (_fileMapView == IntPtr.Zero)
        {
            return;
        }

        Array.Clear(_mapBuffer);

        if (!IsStopped(_status.State))
        {
            WritePlayingMap();
        }

        Marshal.Copy(_mapBuffer, 0, _fileMapView, _mapBuffer.Length);
    }

    private void WritePlayingMap()
    {
        int remainingChars = (FileMapSize - HeaderSize) / 2;
        string album = TakeChars(_status.Album, 96, ref remainingChars);
        string artist = TakeChars(_status.Artist, 128, ref remainingChars);
        string date = TakeChars(_status.Date, 32, ref remainingChars);
        string filePath = TakeChars(_status.FilePath ?? _status.Filename ?? _status.Title, 256, ref remainingChars);
        string genre = TakeChars(_status.Genre, 64, ref remainingChars);
        string title = TakeChars(_status.Title, remainingChars, ref remainingChars);

        WriteInt32(0, HeaderSize);
        WriteInt32(4, 1);
        WriteInt32(8, 0);
        WriteInt32(12, 2);
        WriteInt32(16, Math.Max(0, _status.Length) * 1000);
        WriteInt64(20, unchecked((long)ComputeTrackToken(album, artist, date, filePath, genre, title)));
        WriteInt32(28, _rating);
        WriteInt32(32, 0);
        WriteInt32(36, unchecked((int)(_playbackSessionVersion & 0x7FFFFFFF)));
        WriteInt32(40, album.Length);
        WriteInt32(44, artist.Length);
        WriteInt32(48, date.Length);
        WriteInt32(52, filePath.Length);
        WriteInt32(56, genre.Length);
        WriteInt32(60, title.Length);

        int offset = HeaderSize;
        offset = WriteString(offset, album);
        offset = WriteString(offset, artist);
        offset = WriteString(offset, date);
        offset = WriteString(offset, filePath);
        offset = WriteString(offset, genre);
        WriteString(offset, title);
    }

    private static int TranslateState(string state)
    {
        return state switch
        {
            "playing" => 1,
            "paused" => 2,
            _ => 0
        };
    }

    private static bool IsStopped(string? state)
    {
        return !string.Equals(state, "playing", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(state, "paused", StringComparison.OrdinalIgnoreCase);
    }

    private static string TakeChars(string? value, int limit, ref int remainingChars)
    {
        if (remainingChars <= 0 || limit <= 0 || string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        int length = Math.Min(value.Trim().Length, Math.Min(limit, remainingChars));
        remainingChars -= length;
        return value.Trim()[..length];
    }

    private ulong ComputeTrackToken(string album, string artist, string date, string filePath, string genre, string title)
    {
        ulong hash = 14695981039346656037UL;

        HashString(ref hash, album);
        HashString(ref hash, artist);
        HashString(ref hash, date);
        HashString(ref hash, filePath);
        HashString(ref hash, genre);
        HashString(ref hash, title);
        HashString(ref hash, _playbackSessionVersion.ToString());

        return hash == 0 ? 1UL : hash;
    }

    private static void HashString(ref ulong hash, string value)
    {
        foreach (char ch in value)
        {
            hash ^= ch;
            hash *= 1099511628211UL;
        }
    }

    private void WriteInt32(int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(_mapBuffer.AsSpan(offset, sizeof(int)), value);
    }

    private void WriteInt64(int offset, long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(_mapBuffer.AsSpan(offset, sizeof(long)), value);
    }

    private int WriteString(int offset, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return offset;
        }

        byte[] bytes = Encoding.Unicode.GetBytes(value);
        bytes.CopyTo(_mapBuffer, offset);
        return offset + bytes.Length;
    }

    private sealed class BridgeWindow : NativeWindow, IDisposable
    {
        private readonly MessageHandler _handler;

        public BridgeWindow(string className, string windowName, MessageHandler handler)
        {
            _handler = handler;
            EnsureWindowClassRegistered(className);
            IntPtr handle = CreateWindowEx(
                0,
                className,
                windowName,
                0,
                0,
                0,
                0,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero);

            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to create compatibility window '{className}'.");
            }

            AssignHandle(handle);
        }

        protected override void WndProc(ref Message m)
        {
            IntPtr result = _handler(ref m);
            if (result != IntPtr.Zero || m.Msg == WM_CLOSE || m.Msg == WM_AIMP_COMMAND || m.Msg == WM_WA_IPC)
            {
                m.Result = result;
                return;
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyWindow(Handle);
                ReleaseHandle();
            }
        }
    }

    private static void EnsureWindowClassRegistered(string className)
    {
        if (GetClassInfoEx(GetModuleHandle(null), className, out _))
        {
            return;
        }

        var windowClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(DefWindowProcDelegateInstance),
            hInstance = GetModuleHandle(null),
            lpszClassName = className
        };

        ushort atom = RegisterClassEx(ref windowClass);
        if (atom == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 1410)
            {
                throw new InvalidOperationException($"Failed to register compatibility window class '{className}' ({error}).");
            }
        }
    }

    private static readonly WndProcDelegate DefWindowProcDelegateInstance = DefWindowProcThunk;

    private delegate IntPtr MessageHandler(ref Message message);

    private static IntPtr DefWindowProcThunk(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx([In] ref WNDCLASSEX windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetClassInfoEx(IntPtr hInstance, string className, out WNDCLASSEX windowClass);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpFileMappingAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
