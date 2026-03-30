using Windows.Media;

namespace VlcShimDebugFr;

internal sealed class SmtcShimPublisher : IDisposable
{
    private readonly SystemMediaTransportControls _smtc;
    private readonly VlcHttpClient _vlc;
    private string? _lastLoggedState;
    private string? _lastLoggedTitle;
    private string? _lastLoggedArtist;

    public SmtcShimPublisher(SystemMediaTransportControls smtc, VlcHttpClient vlc)
    {
        _smtc = smtc;
        _vlc = vlc;

        _smtc.IsEnabled = true;
        _smtc.IsPlayEnabled = true;
        _smtc.IsPauseEnabled = true;
        _smtc.IsNextEnabled = true;
        _smtc.IsPreviousEnabled = true;
        _smtc.IsStopEnabled = true;

        _smtc.ButtonPressed += OnButtonPressed;
    }

    public void Update(StatusShim status)
    {
        _smtc.PlaybackStatus = status.State switch
        {
            "playing" => MediaPlaybackStatus.Playing,
            "paused" => MediaPlaybackStatus.Paused,
            _ => MediaPlaybackStatus.Stopped
        };

        var updater = _smtc.DisplayUpdater;
        updater.Type = MediaPlaybackType.Music;
        updater.MusicProperties.Title = status.Title;
        updater.MusicProperties.Artist = status.Artist ?? string.Empty;
        updater.Update();

        LogStatus(status);

        if (status.Length > 0)
        {
            _smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties
            {
                StartTime = TimeSpan.Zero,
                EndTime = TimeSpan.FromSeconds(status.Length),
                Position = TimeSpan.FromSeconds(status.Time)
            });
        }
    }

    public void Clear()
    {
        _smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;
        _smtc.DisplayUpdater.ClearAll();
        _smtc.DisplayUpdater.Update();
        _smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties());
        _lastLoggedState = null;
        _lastLoggedTitle = null;
        _lastLoggedArtist = null;
    }

    private void LogStatus(StatusShim status)
    {
        string loggedState = _smtc.PlaybackStatus switch
        {
            MediaPlaybackStatus.Playing => "playing",
            MediaPlaybackStatus.Paused => "paused",
            _ => "stopped"
        };

        bool isStopped = _smtc.PlaybackStatus == MediaPlaybackStatus.Stopped;
        string loggedTitle = isStopped ? "none" : status.Title;
        string loggedArtist = isStopped
            ? "none"
            : string.IsNullOrWhiteSpace(status.Artist) ? "none" : status.Artist;

        if (!string.Equals(loggedState, _lastLoggedState, StringComparison.Ordinal) ||
            !string.Equals(loggedTitle, _lastLoggedTitle, StringComparison.Ordinal) ||
            !string.Equals(loggedArtist, _lastLoggedArtist, StringComparison.Ordinal))
        {
            VerboseLogger.Info($"📡 SMTC sync: {loggedState} | {loggedTitle} | {loggedArtist}");
            _lastLoggedState = loggedState;
            _lastLoggedTitle = loggedTitle;
            _lastLoggedArtist = loggedArtist;
        }
    }

    private void OnButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        _ = HandleButtonAsync(args.Button);
    }

    private async Task HandleButtonAsync(SystemMediaTransportControlsButton button)
    {
        try
        {
            VerboseLogger.Info($"🎛️ SMTC button: {button}");

            switch (button)
            {
                case SystemMediaTransportControlsButton.Play:
                    await _vlc.SendCommandAsync("pl_play");
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    await _vlc.SendCommandAsync("pl_pause");
                    break;
                case SystemMediaTransportControlsButton.Stop:
                    await _vlc.SendCommandAsync("pl_stop");
                    break;
                case SystemMediaTransportControlsButton.Next:
                    await _vlc.SendCommandAsync("pl_next");
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    await _vlc.SendCommandAsync("pl_previous");
                    break;
            }
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        _smtc.ButtonPressed -= OnButtonPressed;
    }
}
