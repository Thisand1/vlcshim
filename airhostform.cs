using Windows.Media;
using Windows.Media.Playback;

namespace VlcShimDebugFr;

internal sealed class SmtcHostSession : IDisposable
{
    private readonly MediaPlayer _mediaPlayer;

    public SmtcHostSession()
    {
        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.CommandManager.IsEnabled = false;
        Smtc = _mediaPlayer.SystemMediaTransportControls;
    }

    public SystemMediaTransportControls Smtc { get; }

    public void Dispose()
    {
        _mediaPlayer.Dispose();
    }
}
// theairisfire