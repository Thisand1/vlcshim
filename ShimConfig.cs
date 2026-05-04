namespace VlcShimDebugFr;

internal sealed class ShimConfig
{
    public string PlayerProfileId { get; set; } = PlayerIdentityProfiles.Default.Id;

    public string CustomPlayerDisplayName { get; set; } = string.Empty;

    public string CustomAppUserModelId { get; set; } = string.Empty;

    public string VlcHttpPassword { get; set; } = string.Empty;

    public string VlcHttpPorts { get; set; } = string.Empty;

    public bool AllowCompatibilityControlCommands { get; set; } = false;

    public bool ShowStartupToast { get; set; } = true;

    public string LogViewerThemeId { get; set; } = LogViewerThemes.Default.Id;

    public string LogViewerBackgroundImagePath { get; set; } = string.Empty;

    public int LogViewerBackgroundOpacityPercent { get; set; } = 18;

    public int LogViewerBackgroundDimPercent { get; set; } = 68;
}
