using System.Text.Json;

namespace VlcShimDebugFr;

internal static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static string GetConfigPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "vlcshimdebugfr",
            "config.json");
    }

    public static ShimConfig Load()
    {
        try
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
            {
                return new ShimConfig();
            }

            var config = JsonSerializer.Deserialize<ShimConfig>(File.ReadAllText(path), JsonOptions);
            return config ?? new ShimConfig();
        }
        catch
        {
            return new ShimConfig();
        }
    }

    public static void Save(ShimConfig config)
    {
        string path = GetConfigPath();
        string directory = Path.GetDirectoryName(path) ?? ".";
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(directory, $"{Path.GetFileName(path)}.tmp");
        File.WriteAllText(tempPath, JsonSerializer.Serialize(config, JsonOptions));

        if (File.Exists(path))
        {
            File.Copy(tempPath, path, overwrite: true);
            File.Delete(tempPath);
            return;
        }

        File.Move(tempPath, path);
    }

    public static ShimConfig Clone(ShimConfig config)
    {
        return new ShimConfig
        {
            PlayerProfileId = config.PlayerProfileId,
            CustomPlayerDisplayName = config.CustomPlayerDisplayName,
            CustomAppUserModelId = config.CustomAppUserModelId,
            VlcHttpPassword = config.VlcHttpPassword,
            VlcHttpPorts = config.VlcHttpPorts,
            ShowStartupToast = config.ShowStartupToast,
            LogViewerThemeId = config.LogViewerThemeId,
            LogViewerBackgroundImagePath = config.LogViewerBackgroundImagePath,
            LogViewerBackgroundOpacityPercent = config.LogViewerBackgroundOpacityPercent,
            LogViewerBackgroundDimPercent = config.LogViewerBackgroundDimPercent
        };
    }
}
