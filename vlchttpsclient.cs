using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace VlcShimDebugFr;

internal sealed class VlcHttpClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _baseUri;

    private VlcHttpClient(HttpClient http, Uri baseUri)
    {
        EnsureLoopbackBaseUri(baseUri);
        _http = http;
        _baseUri = baseUri;
    }

    public string BaseUrl => _baseUri.GetLeftPart(UriPartial.Authority);

    public static async Task<VlcHttpClient> CreateAsync(string password, int[]? preferredPorts, CancellationToken ct)
    {
        var ports = new List<int> { 8080, 4212 };
        if (preferredPorts is not null)
        {
            ports.AddRange(preferredPorts.Where(IsValidPort));
        }

        foreach (int port in ports.Distinct())
        {
            var http = BuildClient(password);
            Uri baseUri = BuildLoopbackBaseUri(port);
            var client = new VlcHttpClient(http, baseUri);

            try
            {
                _ = await client.GetStatusJsonAsync(ct);
                return client;
            }
            catch
            {
                client.Dispose();
            }
        }

        throw new InvalidOperationException("Could not connect to VLC HTTP interface on any tested local port (8080/4212/custom).");
    }

    private static HttpClient BuildClient(string password)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(2),
            ConnectCallback = ConnectLoopbackAsync
        };

        var http = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{password}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        return http;
    }

    public async Task<string> GetStatusJsonAsync(CancellationToken ct)
    {
        using var resp = await _http.GetAsync(BuildStatusUri(), ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    public Task SendCommandAsync(string command, CancellationToken ct = default)
        => SendCommandAsync(command, null, ct);

    public async Task SendCommandAsync(string command, Dictionary<string, string>? parameters, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(BuildStatusUri(command, parameters), ct);
        resp.EnsureSuccessStatusCode();
    }

    public Task SeekAsync(double percent, CancellationToken ct = default)
    {
        percent = Math.Clamp(percent, 0, 100);
        return SendCommandAsync("seek", new() { ["val"] = $"{percent:0}%" }, ct);
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private Uri BuildStatusUri(string? command = null, Dictionary<string, string>? parameters = null)
    {
        var builder = new UriBuilder(_baseUri)
        {
            Path = "/requests/status.json"
        };

        var queryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(command))
        {
            queryParts.Add($"command={Uri.EscapeDataString(command)}");
        }

        if (parameters is not null)
        {
            foreach ((string key, string value) in parameters)
            {
                queryParts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
        }

        builder.Query = string.Join("&", queryParts);
        Uri uri = builder.Uri;
        EnsureLoopbackBaseUri(new Uri(uri.GetLeftPart(UriPartial.Authority)));
        return uri;
    }

    private static Uri BuildLoopbackBaseUri(int port)
    {
        if (!IsValidPort(port))
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        return new UriBuilder(Uri.UriSchemeHttp, IPAddress.Loopback.ToString(), port).Uri;
    }

    private static void EnsureLoopbackBaseUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            !IPAddress.TryParse(uri.Host, out var address) ||
            !IPAddress.IsLoopback(address))
        {
            throw new InvalidOperationException("Refusing to connect to a non-loopback VLC endpoint.");
        }
    }

    private static bool IsValidPort(int port)
    {
        return port is >= IPEndPoint.MinPort and <= IPEndPoint.MaxPort;
    }

    private static async ValueTask<Stream> ConnectLoopbackAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        if (!IPAddress.TryParse(context.DnsEndPoint.Host, out var address) || !IPAddress.IsLoopback(address))
        {
            throw new HttpRequestException("Refusing non-loopback HTTP target.");
        }

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(address, context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
