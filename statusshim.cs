using Windows.Media;

namespace VlcShimDebugFr;

internal sealed class StatusShim
{
    public required string State { get; init; }

    public required string Title { get; init; }

    public string? Artist { get; init; }

    public string? Album { get; init; }

    public string? Genre { get; init; }

    public string? Date { get; init; }

    public string? Filename { get; init; }

    public string? FilePath { get; init; }

    public int Time { get; init; }

    public int Length { get; init; }

    public int RawVolume { get; init; }

    public int Volume { get; init; }

    public double Position { get; init; }

    public MediaPlaybackAutoRepeatMode RepeatMode { get; init; }

    public bool IsShuffleEnabled { get; init; }

    public double Rate { get; init; } = 1.0;

    public static StatusShim CreateStopped() => new()
    {
        State = "stopped",
        Title = "VLC",
        Volume = 100
    };
}
