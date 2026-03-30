using System.Text.Json;
using Windows.Media;
using VlcShimDebugFr;

namespace VlcShimDebugFr
{
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

        public void UpdateFromStatusJson(string json)
        {
            // Minimal parsing for title/artist/state/position/length.
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var state = root.TryGetProperty("state", out var st) ? st.GetString() : null;
            var time = root.TryGetProperty("time", out var t) ? t.GetInt32() : 0;
            var length = root.TryGetProperty("length", out var l) ? l.GetInt32() : 0;

            var meta = root.TryGetProperty("information", out var info) &&
                       info.TryGetProperty("category", out var cat) &&
                       cat.TryGetProperty("meta", out var m) ? m : default;

            string title = meta.ValueKind != JsonValueKind.Undefined && meta.TryGetProperty("title", out var mt) ? (mt.GetString() ?? "VLC") : "VLC";
            string artist = meta.ValueKind != JsonValueKind.Undefined && meta.TryGetProperty("artist", out var ma) ? (ma.GetString() ?? "") : "";

            _smtc.PlaybackStatus = state switch
            {
                "playing" => MediaPlaybackStatus.Playing,
                "paused" => MediaPlaybackStatus.Paused,
                _ => MediaPlaybackStatus.Stopped
            };

            var updater = _smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = title;
            updater.MusicProperties.Artist = artist;
            updater.Update();

            string loggedState = _smtc.PlaybackStatus switch
            {
                MediaPlaybackStatus.Playing => "playing",
                MediaPlaybackStatus.Paused => "paused",
                _ => "stopped"
            };
            bool isStopped = _smtc.PlaybackStatus == MediaPlaybackStatus.Stopped;
            string loggedTitle = isStopped ? "none" : title;
            string loggedArtist = isStopped
                ? "none"
                : string.IsNullOrWhiteSpace(artist) ? "none" : artist;

            if (!string.Equals(loggedState, _lastLoggedState, StringComparison.Ordinal) ||
                !string.Equals(loggedTitle, _lastLoggedTitle, StringComparison.Ordinal) ||
                !string.Equals(loggedArtist, _lastLoggedArtist, StringComparison.Ordinal))
            {
                VerboseLogger.Info($"📡 SMTC sync: {loggedState} | {loggedTitle} | {loggedArtist}");
                _lastLoggedState = loggedState;
                _lastLoggedTitle = loggedTitle;
                _lastLoggedArtist = loggedArtist;
            }

            // Timeline (best effort)
            if (length > 0)
            {
                _smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties
                {
                    StartTime = TimeSpan.Zero,
                    EndTime = TimeSpan.FromSeconds(length),
                    Position = TimeSpan.FromSeconds(time)
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
                    default:
                        break;
                }
            }
            catch
            {
                // swallow; you can log if you want
            }
        }

        public void Dispose()
        {
            _smtc.ButtonPressed -= OnButtonPressed;
        }
    }
}

