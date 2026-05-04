using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace VlcShimDebugFr;

internal static class ConfigStore
{
    private const string ProtectedPasswordPrefix = "dpapi:";
    private static readonly byte[] PasswordEntropy = Encoding.UTF8.GetBytes("vlcshimdebugfr:vlc-http-password:v1");

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

            var config = JsonSerializer.Deserialize<ShimConfig>(File.ReadAllText(path), JsonOptions) ?? new ShimConfig();
            config.VlcHttpPassword = UnprotectPassword(config.VlcHttpPassword);
            return config;
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
        var persisted = Clone(config);
        persisted.VlcHttpPassword = ProtectPassword(config.VlcHttpPassword);
        File.WriteAllText(tempPath, JsonSerializer.Serialize(persisted, JsonOptions));

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
            AllowCompatibilityControlCommands = config.AllowCompatibilityControlCommands,
            ShowStartupToast = config.ShowStartupToast,
            LogViewerThemeId = config.LogViewerThemeId,
            LogViewerBackgroundImagePath = config.LogViewerBackgroundImagePath,
            LogViewerBackgroundOpacityPercent = config.LogViewerBackgroundOpacityPercent,
            LogViewerBackgroundDimPercent = config.LogViewerBackgroundDimPercent
        };
    }

    private static string ProtectPassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return string.Empty;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(password);
        byte[] protectedBytes = ProtectedData.Protect(bytes, PasswordEntropy, DataProtectionScope.CurrentUser);
        return ProtectedPasswordPrefix + Convert.ToBase64String(protectedBytes);
    }

    private static string UnprotectPassword(string? persisted)
    {
        if (string.IsNullOrWhiteSpace(persisted))
        {
            return string.Empty;
        }

        if (!persisted.StartsWith(ProtectedPasswordPrefix, StringComparison.Ordinal))
        {
            return persisted;
        }

        string payload = persisted[ProtectedPasswordPrefix.Length..];
        try
        {
            byte[] protectedBytes = Convert.FromBase64String(payload);
            byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, PasswordEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}
