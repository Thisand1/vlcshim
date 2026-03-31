using System.Text.Json;
using Windows.Media;

namespace VlcShimDebugFr;

internal static class StatusShimParser
{
    private const int VlcFullVolume = 256;

    public static StatusShim Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Parse(doc.RootElement);
    }

    public static StatusShim Parse(JsonElement root)
    {
        string state = GetString(root, "state") ?? "stopped";
        int time = GetInt32(root, "time");
        int length = GetInt32(root, "length");
        int currentPlaylistId = GetInt32(root, "currentplid", -1);
        int rawVolume = GetInt32(root, "volume");
        double position = GetDouble(root, "position");
        bool isShuffleEnabled = GetBoolean(root, "random");
        bool repeatTrack = GetBoolean(root, "repeat");
        bool repeatPlaylist = GetBoolean(root, "loop");
        double rate = GetDouble(root, "rate", 1.0);

        var meta = TryGetMeta(root);
        string? filePath = GetString(meta, "filename") ?? GetString(meta, "url");
        string? filename = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFileName(filePath);
        string? parsedTitle = GetString(meta, "title");
        string title = ResolveTitle(state, currentPlaylistId, length, parsedTitle, filename, filePath);

        return new StatusShim
        {
            State = state,
            Title = title,
            Artist = NormalizeOptionalString(GetString(meta, "artist")),
            Album = NormalizeOptionalString(GetString(meta, "album")),
            Genre = NormalizeOptionalString(GetString(meta, "genre")),
            Date = NormalizeOptionalString(GetString(meta, "date") ?? GetString(meta, "year")),
            Filename = NormalizeOptionalString(filename),
            FilePath = NormalizeOptionalString(filePath),
            Time = Math.Max(0, time),
            Length = Math.Max(0, length),
            RawVolume = Math.Max(0, rawVolume),
            Volume = NormalizeVolume(rawVolume),
            Position = Math.Clamp(position, 0d, 1d),
            RepeatMode = repeatTrack
                ? MediaPlaybackAutoRepeatMode.Track
                : repeatPlaylist
                    ? MediaPlaybackAutoRepeatMode.List
                    : MediaPlaybackAutoRepeatMode.None,
            IsShuffleEnabled = isShuffleEnabled,
            Rate = rate > 0 ? rate : 1.0
        };
    }

    private static JsonElement TryGetMeta(JsonElement root)
    {
        if (!root.TryGetProperty("information", out var info) ||
            !info.TryGetProperty("category", out var category) ||
            !category.TryGetProperty("meta", out var meta))
        {
            return default;
        }

        return meta;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.ValueKind != JsonValueKind.Undefined &&
               element.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int GetInt32(JsonElement element, string name)
    {
        return GetInt32(element, name, 0);
    }

    private static int GetInt32(JsonElement element, string name, int defaultValue)
    {
        return element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)
            ? value
            : defaultValue;
    }

    private static double GetDouble(JsonElement element, string name, double defaultValue = 0d)
    {
        return element.TryGetProperty(name, out var property) && property.TryGetDouble(out var value)
            ? value
            : defaultValue;
    }

    private static bool GetBoolean(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) &&
               (property.ValueKind == JsonValueKind.True ||
                (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value) && value != 0));
    }

    private static string? NormalizeOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string ResolveTitle(
        string state,
        int currentPlaylistId,
        int length,
        string? parsedTitle,
        string? filename,
        string? filePath)
    {
        string? normalizedTitle = NormalizeOptionalString(parsedTitle);
        bool hasMediaContext =
            currentPlaylistId >= 0 ||
            length > 0 ||
            !string.IsNullOrWhiteSpace(filePath) ||
            !string.IsNullOrWhiteSpace(filename);

        if (!hasMediaContext)
        {
            if (string.IsNullOrWhiteSpace(normalizedTitle) || LooksLikeVlcBranding(normalizedTitle))
            {
                return "VLC";
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return normalizedTitle;
        }

        if (!string.IsNullOrWhiteSpace(filename))
        {
            return filename;
        }

        return state == "stopped" ? "VLC" : "Unknown";
    }

    private static bool LooksLikeVlcBranding(string value)
    {
        string compact = new string(value
            .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            .ToArray())
            .Trim();

        return compact.Equals("VLC", StringComparison.OrdinalIgnoreCase) ||
               compact.Equals("VLC media player", StringComparison.OrdinalIgnoreCase) ||
               compact.Equals("VLC media player skinned", StringComparison.OrdinalIgnoreCase) ||
               compact.Contains("VLC media player", StringComparison.OrdinalIgnoreCase);
    }

    private static int NormalizeVolume(int rawVolume)
    {
        if (rawVolume <= 0)
        {
            return 0;
        }

        double scaled = rawVolume * 100d / VlcFullVolume;
        return Math.Clamp((int)Math.Round(scaled), 0, 100);
    }
}
