using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using AvaloniaBitmapInterpolationMode = Avalonia.Media.Imaging.BitmapInterpolationMode;

namespace VlcShimDebugFr.AvaloniaUi.Logs;

internal sealed partial class LogViewerViewModel : ObservableObject
{
    private const int MaxDisplayedChars = LogViewerForm.MaxDisplayedChars;
    private const int MaxDisplayedLines = 600;
    private const int MaxBackgroundDecodeWidth = 1600;

    private readonly ObservableCollection<LogLineViewModel> _lines = new();
    private int _displayedChars;
    private LogViewerTheme _theme;
    private AvaloniaBitmap? _backgroundBitmap;
    private LogThemePalette _palette;

    public LogViewerViewModel(LogViewerTheme theme, ShimConfig config)
    {
        _theme = theme;
        _palette = LogThemePalette.Create(theme, unchecked((int)0x00000000));
        Lines = new ReadOnlyObservableCollection<LogLineViewModel>(_lines);
        ApplyConfig(theme, config);
    }

    public ReadOnlyObservableCollection<LogLineViewModel> Lines { get; }

    [ObservableProperty]
    private IBrush backgroundBrush = Avalonia.Media.Brushes.Black;

    [ObservableProperty]
    private IBrush surfaceBrush = Avalonia.Media.Brushes.Black;

    [ObservableProperty]
    private IBrush accentBrush = Avalonia.Media.Brushes.White;

    [ObservableProperty]
    private IBrush backgroundDimBrush = Avalonia.Media.Brushes.Transparent;

    [ObservableProperty]
    private AvaloniaBitmap? backgroundImage;

    [ObservableProperty]
    private double backgroundImageOpacity;

    [ObservableProperty]
    private bool hasBackgroundImage;

    public void LoadLines(IEnumerable<string> lines)
    {
        _lines.Clear();
        _displayedChars = 0;

        foreach (string line in lines)
        {
            AddLine(line);
        }

        TrimIfNeeded();
        RecomputeLineStyles();
        OnPropertyChanged(nameof(Lines));
    }

    public void AppendLine(string line)
    {
        AddLine(line);
        if (TrimIfNeeded())
        {
            RecomputeLineStyles();
            return;
        }

        if (_lines.Count > 0)
        {
            _lines[^1].ApplyTheme(_palette, _lines.Count - 1);
        }
    }

    public void ApplyConfig(LogViewerTheme theme, ShimConfig config)
    {
        _theme = theme;
        BackgroundImageOpacity = Math.Clamp(config.LogViewerBackgroundOpacityPercent, 0, 100) / 100.0;
        int backgroundDimArgb = WithOpacity(theme.BackgroundArgb, Math.Clamp(config.LogViewerBackgroundDimPercent, 0, 100) / 100.0);
        _palette = LogThemePalette.Create(theme, backgroundDimArgb);

        BackgroundBrush = _palette.BackgroundBrush;
        SurfaceBrush = _palette.SurfaceBrush;
        AccentBrush = _palette.AccentBrush;
        BackgroundDimBrush = _palette.BackgroundDimBrush;

        ReplaceBackgroundBitmap(config.LogViewerBackgroundImagePath, BackgroundImageOpacity);
        HasBackgroundImage = BackgroundImage is not null && BackgroundImageOpacity > 0.0;

        RecomputeLineStyles();
    }

    private void AddLine(string line)
    {
        _lines.Add(ParseLine(line));
        _displayedChars += line.Length + Environment.NewLine.Length;
    }

    private bool TrimIfNeeded()
    {
        bool trimmed = false;
        while ((_displayedChars > MaxDisplayedChars || _lines.Count > MaxDisplayedLines) && _lines.Count > 0)
        {
            LogLineViewModel removed = _lines[0];
            _displayedChars -= removed.RawText.Length + Environment.NewLine.Length;
            _lines.RemoveAt(0);
            trimmed = true;
        }

        return trimmed;
    }

    private void RecomputeLineStyles()
    {
        for (int i = 0; i < _lines.Count; i++)
        {
            _lines[i].ApplyTheme(_palette, i);
        }
    }

    private void ReplaceBackgroundBitmap(string? path, double opacity)
    {
        _backgroundBitmap?.Dispose();
        _backgroundBitmap = null;

        if (opacity <= 0.0 || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            BackgroundImage = null;
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
            _backgroundBitmap = AvaloniaBitmap.DecodeToWidth(stream, MaxBackgroundDecodeWidth, AvaloniaBitmapInterpolationMode.MediumQuality);
        }
        catch
        {
            try
            {
                _backgroundBitmap = new AvaloniaBitmap(path);
            }
            catch
            {
                _backgroundBitmap = null;
            }
        }

        BackgroundImage = _backgroundBitmap;
    }

    private LogLineViewModel ParseLine(string text)
    {
        return new LogLineViewModel(text, ParseLevel(text));
    }

    private static LogLevelKind ParseLevel(string text)
    {
        int cursor = 0;
        while (cursor < text.Length)
        {
            int start = text.IndexOf('[', cursor);
            if (start < 0)
            {
                break;
            }

            int end = text.IndexOf(']', start + 1);
            if (end < 0)
            {
                break;
            }

            string token = text.Substring(start + 1, end - start - 1);
            LogLevelKind level = token.ToUpperInvariant() switch
            {
                "INFO" => LogLevelKind.Info,
                "WARN" or "WARNING" => LogLevelKind.Warning,
                "ERROR" => LogLevelKind.Error,
                _ => LogLevelKind.Neutral
            };

            if (level != LogLevelKind.Neutral)
            {
                return level;
            }

            cursor = end + 1;
        }

        return LogLevelKind.Neutral;
    }

    private static int WithOpacity(int argb, double opacity)
    {
        uint color = unchecked((uint)argb);
        byte alpha = (byte)Math.Clamp((int)Math.Round(Math.Clamp(opacity, 0.0, 1.0) * 255.0), 0, 255);
        return unchecked((int)((color & 0x00FFFFFFu) | ((uint)alpha << 24)));
    }

    internal enum LogLevelKind
    {
        Neutral,
        Info,
        Warning,
        Error
    }
}

internal sealed partial class LogLineViewModel : ObservableObject
{
    private readonly LogViewerViewModel.LogLevelKind _level;

    public LogLineViewModel(string rawText, LogViewerViewModel.LogLevelKind level)
    {
        RawText = rawText;
        _level = level;
    }

    public string RawText { get; }

    [ObservableProperty]
    private IBrush rowBrush = Avalonia.Media.Brushes.Transparent;

    [ObservableProperty]
    private IBrush markerBrush = Avalonia.Media.Brushes.White;

    [ObservableProperty]
    private IBrush foreground = Avalonia.Media.Brushes.White;

    public void ApplyTheme(LogThemePalette palette, int lineIndex)
    {
        RowBrush = (lineIndex & 1) == 1 ? palette.StripeBrush : palette.TransparentBrush;
        MarkerBrush = _level switch
        {
            LogViewerViewModel.LogLevelKind.Info => palette.InfoBrush,
            LogViewerViewModel.LogLevelKind.Warning => palette.WarningBrush,
            LogViewerViewModel.LogLevelKind.Error => palette.ErrorBrush,
            _ => palette.AccentBrush
        };

        Foreground = _level switch
        {
            LogViewerViewModel.LogLevelKind.Warning => palette.WarningBrush,
            LogViewerViewModel.LogLevelKind.Error => palette.ErrorBrush,
            _ => palette.ForegroundBrush
        };
    }
}

internal sealed class LogThemePalette
{
    private LogThemePalette(
        IBrush backgroundBrush,
        IBrush surfaceBrush,
        IBrush accentBrush,
        IBrush backgroundDimBrush,
        IBrush foregroundBrush,
        IBrush infoBrush,
        IBrush warningBrush,
        IBrush errorBrush,
        IBrush stripeBrush,
        IBrush transparentBrush)
    {
        BackgroundBrush = backgroundBrush;
        SurfaceBrush = surfaceBrush;
        AccentBrush = accentBrush;
        BackgroundDimBrush = backgroundDimBrush;
        ForegroundBrush = foregroundBrush;
        InfoBrush = infoBrush;
        WarningBrush = warningBrush;
        ErrorBrush = errorBrush;
        StripeBrush = stripeBrush;
        TransparentBrush = transparentBrush;
    }

    public IBrush BackgroundBrush { get; }

    public IBrush SurfaceBrush { get; }

    public IBrush AccentBrush { get; }

    public IBrush BackgroundDimBrush { get; }

    public IBrush ForegroundBrush { get; }

    public IBrush InfoBrush { get; }

    public IBrush WarningBrush { get; }

    public IBrush ErrorBrush { get; }

    public IBrush StripeBrush { get; }

    public IBrush TransparentBrush { get; }

    public static LogThemePalette Create(LogViewerTheme theme, int backgroundDimArgb)
    {
        return new LogThemePalette(
            CreateBrush(theme.BackgroundArgb),
            CreateBrush(theme.SurfaceArgb),
            CreateBrush(theme.AccentArgb),
            CreateBrush(backgroundDimArgb),
            CreateBrush(theme.ForegroundArgb),
            CreateBrush(theme.InfoArgb),
            CreateBrush(theme.WarningArgb),
            CreateBrush(theme.ErrorArgb),
            CreateBrush(theme.StripeArgb),
            Avalonia.Media.Brushes.Transparent);
    }

    private static SolidColorBrush CreateBrush(int argb)
    {
        return new(Avalonia.Media.Color.FromUInt32(unchecked((uint)argb)));
    }
}
