namespace VlcShimDebugFr;

public sealed record PlayerIdentityProfile(
    string Id,
    string Label,
    string DisplayName,
    string FallbackAppUserModelId,
    bool UseExplicitAppUserModelId = true,
    bool PreferInstalledAppMatch = true,
    bool IsCustom = false);

internal static class PlayerIdentityProfiles
{
    public static readonly PlayerIdentityProfile Default = new(
        "vlc",
        "VLC",
        "VLC",
        "VlcShim.VLC",
        UseExplicitAppUserModelId: true,
        PreferInstalledAppMatch: false);

    private static readonly IReadOnlyList<PlayerIdentityProfile> Profiles = new[]
    {
        Default,
        new PlayerIdentityProfile("spotify", "Spotify", "Spotify", "Spotify"),
        new PlayerIdentityProfile("spicetify", "Spicetify", "Spicetify", "Spicetify"),
        new PlayerIdentityProfile("aimp", "AIMP", "AIMP", "AIMP"),
        new PlayerIdentityProfile("custom", "Custom", string.Empty, "VlcShim.Custom", IsCustom: true)
    };

    public static IReadOnlyList<PlayerIdentityProfile> All => Profiles;

    public static PlayerIdentityProfile Get(string? id)
    {
        return Profiles.FirstOrDefault(profile => string.Equals(profile.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? Default;
    }

    public static string GetDisplayName(ShimConfig config)
    {
        var profile = Get(config.PlayerProfileId);
        if (!profile.IsCustom)
        {
            return profile.DisplayName;
        }

        return string.IsNullOrWhiteSpace(config.CustomPlayerDisplayName)
            ? "Custom player"
            : config.CustomPlayerDisplayName.Trim();
    }

    public static string GetFallbackAppUserModelId(ShimConfig config)
    {
        var profile = Get(config.PlayerProfileId);
        if (!profile.IsCustom)
        {
            return profile.FallbackAppUserModelId;
        }

        if (!string.IsNullOrWhiteSpace(config.CustomAppUserModelId))
        {
            return config.CustomAppUserModelId.Trim();
        }

        string sanitizedName = new string(GetDisplayName(config)
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitizedName)
            ? profile.FallbackAppUserModelId
            : $"VlcShim.{sanitizedName}";
    }
}
