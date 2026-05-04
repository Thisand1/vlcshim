using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AvaloniaColor = Avalonia.Media.Color;

namespace VlcShimDebugFr.AvaloniaUi.ViewModels;

internal partial class SettingsViewModel : ViewModelBase
{
    private readonly ShimConfig _initialConfig;

    public SettingsViewModel(ShimConfig config)
    {
        PlayerProfiles = new ObservableCollection<PlayerIdentityProfile>(PlayerIdentityProfiles.All);
        LogThemes = new ObservableCollection<LogViewerTheme>(LogViewerThemes.All);
        _initialConfig = ConfigStore.Clone(config);
        LoadFromConfig(_initialConfig);
        StatusMessage = "Edit settings and save to apply them back in the running shim.";
    }

    public ObservableCollection<PlayerIdentityProfile> PlayerProfiles { get; }

    public ObservableCollection<LogViewerTheme> LogThemes { get; }

    public event Action<ShimConfig>? SaveRequested;

    public event Action? CancelRequested;

    public event Action<LogViewerTheme>? ThemePreviewChanged;

    [ObservableProperty]
    private PlayerIdentityProfile? selectedPlayerProfile;

    [ObservableProperty]
    private LogViewerTheme? selectedLogTheme;

    [ObservableProperty]
    private string customPlayerDisplayName = string.Empty;

    [ObservableProperty]
    private string customAppUserModelId = string.Empty;

    [ObservableProperty]
    private string vlcHttpPassword = string.Empty;

    [ObservableProperty]
    private string vlcHttpPorts = string.Empty;

    [ObservableProperty]
    private string logViewerBackgroundImagePath = string.Empty;

    [ObservableProperty]
    private int logViewerBackgroundOpacityPercent = 18;

    [ObservableProperty]
    private int logViewerBackgroundDimPercent = 68;

    [ObservableProperty]
    private bool showStartupToast = true;

    [ObservableProperty]
    private bool allowCompatibilityControlCommands;

    [ObservableProperty]
    private string statusMessage = "Ready.";

    [ObservableProperty]
    private string previewDisplayName = PlayerIdentityProfiles.Default.DisplayName;

    [ObservableProperty]
    private string previewAppUserModelId = PlayerIdentityProfiles.Default.FallbackAppUserModelId;

    [ObservableProperty]
    private string previewThemeLabel = LogViewerThemes.Default.Label;

    [ObservableProperty]
    private string previewPortsLabel = "8080, 4212";

    [ObservableProperty]
    private SettingsThemePalette theme = SettingsThemePalette.Create(LogViewerThemes.Default);

    public bool IsCustomProfile => SelectedPlayerProfile?.IsCustom == true;

    [RelayCommand]
    private void Save()
    {
        if (!TryNormalizePortList(VlcHttpPorts, out string normalizedPorts))
        {
            StatusMessage = "Port list is invalid. Use comma-separated TCP ports such as 8080,4212.";
            return;
        }

        try
        {
            ShimConfig config = BuildConfig(normalizedPorts);
            StatusMessage = $"Prepared settings at {DateTime.Now:t}.";
            SaveRequested?.Invoke(config);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Reset()
    {
        LoadFromConfig(ConfigStore.Clone(_initialConfig));
        StatusMessage = "Reset to the original values from when the dialog opened.";
    }

    [RelayCommand]
    private void Cancel()
    {
        StatusMessage = "Changes discarded.";
        CancelRequested?.Invoke();
    }

    partial void OnSelectedPlayerProfileChanged(PlayerIdentityProfile? value) => RefreshDerivedState();
    partial void OnSelectedLogThemeChanged(LogViewerTheme? value) => RefreshDerivedState();
    partial void OnCustomPlayerDisplayNameChanged(string value) => RefreshDerivedState();
    partial void OnCustomAppUserModelIdChanged(string value) => RefreshDerivedState();
    partial void OnVlcHttpPortsChanged(string value) => RefreshDerivedState();

    private void LoadFromConfig(ShimConfig config)
    {
        SelectedPlayerProfile = PlayerIdentityProfiles.Get(config.PlayerProfileId);
        SelectedLogTheme = LogViewerThemes.Get(config.LogViewerThemeId);
        CustomPlayerDisplayName = config.CustomPlayerDisplayName;
        CustomAppUserModelId = config.CustomAppUserModelId;
        VlcHttpPassword = config.VlcHttpPassword;
        VlcHttpPorts = config.VlcHttpPorts;
        LogViewerBackgroundImagePath = config.LogViewerBackgroundImagePath;
        LogViewerBackgroundOpacityPercent = Math.Clamp(config.LogViewerBackgroundOpacityPercent, 0, 100);
        LogViewerBackgroundDimPercent = Math.Clamp(config.LogViewerBackgroundDimPercent, 0, 100);
        AllowCompatibilityControlCommands = config.AllowCompatibilityControlCommands;
        ShowStartupToast = config.ShowStartupToast;
        RefreshDerivedState();
    }

    private ShimConfig BuildConfig(string normalizedPorts)
    {
        return new ShimConfig
        {
            PlayerProfileId = (SelectedPlayerProfile ?? PlayerIdentityProfiles.Default).Id,
            LogViewerThemeId = (SelectedLogTheme ?? LogViewerThemes.Default).Id,
            CustomPlayerDisplayName = CustomPlayerDisplayName.Trim(),
            CustomAppUserModelId = CustomAppUserModelId.Trim(),
            VlcHttpPassword = string.IsNullOrWhiteSpace(VlcHttpPassword) ? string.Empty : VlcHttpPassword,
            VlcHttpPorts = normalizedPorts,
            LogViewerBackgroundImagePath = LogViewerBackgroundImagePath.Trim(),
            LogViewerBackgroundOpacityPercent = Math.Clamp(LogViewerBackgroundOpacityPercent, 0, 100),
            LogViewerBackgroundDimPercent = Math.Clamp(LogViewerBackgroundDimPercent, 0, 100),
            AllowCompatibilityControlCommands = AllowCompatibilityControlCommands,
            ShowStartupToast = ShowStartupToast
        };
    }

    private void RefreshDerivedState()
    {
        LogViewerTheme selectedTheme = SelectedLogTheme ?? LogViewerThemes.Default;
        ShimConfig previewConfig = new()
        {
            PlayerProfileId = (SelectedPlayerProfile ?? PlayerIdentityProfiles.Default).Id,
            CustomPlayerDisplayName = CustomPlayerDisplayName.Trim(),
            CustomAppUserModelId = CustomAppUserModelId.Trim()
        };

        PreviewDisplayName = PlayerIdentityProfiles.GetDisplayName(previewConfig);
        PreviewAppUserModelId = PlayerIdentityProfiles.GetFallbackAppUserModelId(previewConfig);
        PreviewThemeLabel = selectedTheme.Label;
        PreviewPortsLabel = string.IsNullOrWhiteSpace(VlcHttpPorts) ? "8080, 4212" : VlcHttpPorts.Trim();
        Theme = SettingsThemePalette.Create(selectedTheme);
        ThemePreviewChanged?.Invoke(selectedTheme);
        OnPropertyChanged(nameof(IsCustomProfile));
    }

    private static bool TryNormalizePortList(string rawValue, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return true;
        }

        var ports = new List<int>();
        foreach (string rawPort in rawValue.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(rawPort, out int port) || port < 1 || port > 65535)
            {
                return false;
            }

            if (!ports.Contains(port))
            {
                ports.Add(port);
            }
        }

        normalized = string.Join(",", ports);
        return true;
    }
}

internal sealed class SettingsThemePalette
{
    private SettingsThemePalette(
        IBrush windowBackgroundBrush,
        IBrush cardBrush,
        IBrush cardBorderBrush,
        IBrush heroBrush,
        IBrush heroBorderBrush,
        IBrush badgeBrush,
        IBrush eyebrowBrush,
        IBrush titleBrush,
        IBrush mutedBrush,
        IBrush fieldLabelBrush,
        IBrush monoBrush,
        IBrush highlightBrush,
        IBrush inputBackgroundBrush,
        IBrush inputBorderBrush,
        IBrush primaryButtonBrush,
        IBrush primaryButtonForegroundBrush,
        IBrush ghostButtonBrush,
        IBrush ghostButtonForegroundBrush,
        IBrush chromeBrush,
        IBrush chromeForegroundBrush,
        IBrush chromeHoverBrush)
    {
        WindowBackgroundBrush = windowBackgroundBrush;
        CardBrush = cardBrush;
        CardBorderBrush = cardBorderBrush;
        HeroBrush = heroBrush;
        HeroBorderBrush = heroBorderBrush;
        BadgeBrush = badgeBrush;
        EyebrowBrush = eyebrowBrush;
        TitleBrush = titleBrush;
        MutedBrush = mutedBrush;
        FieldLabelBrush = fieldLabelBrush;
        MonoBrush = monoBrush;
        HighlightBrush = highlightBrush;
        InputBackgroundBrush = inputBackgroundBrush;
        InputBorderBrush = inputBorderBrush;
        PrimaryButtonBrush = primaryButtonBrush;
        PrimaryButtonForegroundBrush = primaryButtonForegroundBrush;
        GhostButtonBrush = ghostButtonBrush;
        GhostButtonForegroundBrush = ghostButtonForegroundBrush;
        ChromeBrush = chromeBrush;
        ChromeForegroundBrush = chromeForegroundBrush;
        ChromeHoverBrush = chromeHoverBrush;
    }

    public IBrush WindowBackgroundBrush { get; }
    public IBrush CardBrush { get; }
    public IBrush CardBorderBrush { get; }
    public IBrush HeroBrush { get; }
    public IBrush HeroBorderBrush { get; }
    public IBrush BadgeBrush { get; }
    public IBrush EyebrowBrush { get; }
    public IBrush TitleBrush { get; }
    public IBrush MutedBrush { get; }
    public IBrush FieldLabelBrush { get; }
    public IBrush MonoBrush { get; }
    public IBrush HighlightBrush { get; }
    public IBrush InputBackgroundBrush { get; }
    public IBrush InputBorderBrush { get; }
    public IBrush PrimaryButtonBrush { get; }
    public IBrush PrimaryButtonForegroundBrush { get; }
    public IBrush GhostButtonBrush { get; }
    public IBrush GhostButtonForegroundBrush { get; }
    public IBrush ChromeBrush { get; }
    public IBrush ChromeForegroundBrush { get; }
    public IBrush ChromeHoverBrush { get; }

    public static SettingsThemePalette Create(LogViewerTheme theme)
    {
        const int white = unchecked((int)0xFFFFFFFF);

        int title = Mix(theme.ForegroundArgb, white, 0.24);
        int body = Mix(theme.ForegroundArgb, white, 0.10);
        int muted = Mix(theme.MutedArgb, title, 0.58);
        int fieldLabel = Mix(theme.MutedArgb, title, 0.72);
        int badge = Mix(theme.SurfaceArgb, theme.AccentArgb, 0.22);
        int border = WithOpacity(Mix(theme.AccentArgb, title, 0.16), 0.82);
        int heroBorder = WithOpacity(theme.AccentArgb, 0.90);
        int inputBackground = Mix(theme.BackgroundArgb, theme.SurfaceArgb, 0.42);
        int inputBorder = WithOpacity(Mix(theme.AccentArgb, title, 0.28), 0.78);
        int ghost = Mix(theme.BackgroundArgb, theme.SurfaceArgb, 0.64);
        int primary = theme.AccentArgb;
        int chromeHover = WithOpacity(theme.StripeArgb, 0.92);

        return new SettingsThemePalette(
            CreateBrush(theme.BackgroundArgb),
            CreateBrush(theme.SurfaceArgb),
            CreateBrush(border),
            CreateBrush(theme.SurfaceArgb),
            CreateBrush(heroBorder),
            CreateBrush(badge),
            CreateBrush(theme.AccentArgb),
            CreateBrush(title),
            CreateBrush(muted),
            CreateBrush(fieldLabel),
            CreateBrush(body),
            CreateBrush(body),
            CreateBrush(inputBackground),
            CreateBrush(inputBorder),
            CreateBrush(primary),
            CreateBrush(theme.BackgroundArgb),
            CreateBrush(ghost),
            CreateBrush(body),
            CreateBrush(theme.SurfaceArgb),
            CreateBrush(body),
            CreateBrush(chromeHover));
    }

    private static SolidColorBrush CreateBrush(int argb)
    {
        return new(ColorFromArgb(argb));
    }

    private static AvaloniaColor ColorFromArgb(int argb)
    {
        return AvaloniaColor.FromUInt32(unchecked((uint)argb));
    }

    private static int Mix(int firstArgb, int secondArgb, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        AvaloniaColor first = ColorFromArgb(firstArgb);
        AvaloniaColor second = ColorFromArgb(secondArgb);

        byte a = (byte)Math.Round(first.A + ((second.A - first.A) * amount));
        byte r = (byte)Math.Round(first.R + ((second.R - first.R) * amount));
        byte g = (byte)Math.Round(first.G + ((second.G - first.G) * amount));
        byte b = (byte)Math.Round(first.B + ((second.B - first.B) * amount));
        return unchecked((int)(((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b));
    }

    private static int WithOpacity(int argb, double opacity)
    {
        uint color = unchecked((uint)argb);
        byte alpha = (byte)Math.Clamp((int)Math.Round(Math.Clamp(opacity, 0.0, 1.0) * 255.0), 0, 255);
        return unchecked((int)((color & 0x00FFFFFFu) | ((uint)alpha << 24)));
    }
}
