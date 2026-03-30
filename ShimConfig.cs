namespace VlcShimDebugFr;

internal sealed class ShimConfig
{
    public string PlayerProfileId { get; set; } = PlayerIdentityProfiles.Default.Id;

    public string CustomPlayerDisplayName { get; set; } = string.Empty;

    public string CustomAppUserModelId { get; set; } = string.Empty;

    public bool ShowStartupToast { get; set; } = true;

    public string LogViewerThemeId { get; set; } = LogViewerThemes.Default.Id;
}
