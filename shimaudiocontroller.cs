using System;
using Windows.Media;
using Windows.Media.Control;
using VlcShimDebugFr;

namespace VlcShimDebugFr
{
    internal sealed class ShimAudioController
    {
        private readonly SystemMediaTransportControls _smtc;

        public event Action? PlayPressed;
        public event Action? PausePressed;
        public event Action? NextPressed;
        public event Action? PreviousPressed;

        public ShimAudioController(SystemMediaTransportControls smtc)
        {
            _smtc = smtc;
            _smtc.ButtonPressed += OnButtonPressed;
        }

        private void OnButtonPressed(
            SystemMediaTransportControls sender,
            SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    PlayPressed?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    PausePressed?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    NextPressed?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    PreviousPressed?.Invoke();
                    break;
            }
        }

        public void UpdatePlaybackState(bool isPlaying)
        {
            _smtc.PlaybackStatus = isPlaying
                ? MediaPlaybackStatus.Playing
                : MediaPlaybackStatus.Paused;
        }

        public void UpdateMetadata(string title, string artist)
        {
            var updater = _smtc.DisplayUpdater;
            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = title;
            updater.MusicProperties.Artist = artist;
            updater.Update();
        }
        public void UpdateTimeline(int positionSeconds, int lengthSeconds)
        {
            var timeline = new SystemMediaTransportControlsTimelineProperties
            {
                StartTime = TimeSpan.Zero,
                Position = TimeSpan.FromSeconds(Math.Max(0, positionSeconds)),
                EndTime = TimeSpan.FromSeconds(Math.Max(0, lengthSeconds))
            };

            _smtc.UpdateTimelineProperties(timeline);
        }
        public void Clear()
        {
            _smtc.PlaybackStatus = MediaPlaybackStatus.Stopped;   // recommended for no "Why do birds suddenly appear Everytime you are near? Just like me, they long to be" ahh moment
            _smtc.DisplayUpdater.ClearAll();
            _smtc.DisplayUpdater.Update();
        }
    }
}
