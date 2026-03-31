namespace VlcShimDebugFr;

internal sealed class RainmeterBridgeController : IDisposable
{
    private readonly object _sync = new();
    private RainmeterAimpBridge? _bridge;

    public bool IsEnabled
    {
        get
        {
            lock (_sync)
            {
                return _bridge is not null;
            }
        }
    }

    public void Replace(RainmeterAimpBridge? bridge)
    {
        RainmeterAimpBridge? previous;

        lock (_sync)
        {
            previous = _bridge;
            _bridge = bridge;
        }

        if (previous is not null)
        {
            try { previous.SetTransport(null); } catch { }
            try { previous.Clear(); } catch { }
            previous.Dispose();
        }
    }

    public void SetTransport(VlcHttpClient? vlc)
    {
        lock (_sync)
        {
            _bridge?.SetTransport(vlc);
        }
    }

    public void Update(StatusShim status)
    {
        lock (_sync)
        {
            _bridge?.Update(status);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _bridge?.Clear();
        }
    }

    public void Dispose()
    {
        Replace(null);
    }
}
