using System.Text.Json;

namespace VlcShimDebugFr;

internal static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
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
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
    }

    public static ShimConfig Clone(ShimConfig config)
    {
        return new ShimConfig
        {
            PlayerProfileId = config.PlayerProfileId,
            CustomPlayerDisplayName = config.CustomPlayerDisplayName,
            CustomAppUserModelId = config.CustomAppUserModelId,
            ShowStartupToast = config.ShowStartupToast
        };
    }
}
