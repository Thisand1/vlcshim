namespace VlcShimDebugFr;

internal static class PlayerIconResolver
{
    public static string ResolveIconPath(ShimConfig config)
    {
        foreach (string candidate in GetCandidates(config))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Environment.ProcessPath ?? AppContext.BaseDirectory;
    }

    private static IEnumerable<string> GetCandidates(ShimConfig config)
    {
        string profileId = config.PlayerProfileId ?? string.Empty;
        string repoAssetsPath = Path.Combine(AppContext.BaseDirectory, "assets", "vlc.ico");
        string sourceAssetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "vlc.ico");
        string projectAssetsPath = Path.Combine(Environment.CurrentDirectory, "assets", "vlc.ico");

        if (string.Equals(profileId, "vlc", StringComparison.OrdinalIgnoreCase))
        {
            yield return repoAssetsPath;
            yield return sourceAssetsPath;
            yield return projectAssetsPath;
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "VideoLAN",
                "VLC",
                "lua",
                "http",
                "favicon.ico");
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "VideoLAN",
                "VLC",
                "vlc.exe");
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "VideoLAN",
                "VLC",
                "vlc.exe");
        }
        else if (string.Equals(profileId, "aimp", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AIMP",
                "AIMP.exe");
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "AIMP",
                "AIMP.exe");
        }
        else if (string.Equals(profileId, "spotify", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(profileId, "spicetify", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Spotify",
                "Spotify.exe");
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "WindowsApps",
                "Spotify.exe");
        }

        yield return Environment.ProcessPath ?? string.Empty;
    }
}
