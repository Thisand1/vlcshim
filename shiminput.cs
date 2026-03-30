namespace VlcShimDebugFr;

internal sealed class ShimInput
{
    private readonly VlcHttpClient _vlc;

    public ShimInput(VlcHttpClient vlc)
    {
        _vlc = vlc;
    }

    public async Task<StatusShim> GetStatusAsync(CancellationToken ct = default)
    {
        string json = await _vlc.GetStatusJsonAsync(ct);
        return StatusShimParser.Parse(json);
    }
}
