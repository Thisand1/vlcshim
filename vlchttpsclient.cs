using System.Net.Http.Headers;
using System.Text;
using VlcShimDebugFr;
using System;

namespace VlcShimDebugFr
{
    internal sealed class VlcHttpClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        private VlcHttpClient(HttpClient http, string baseUrl)
        {
            _http = http;
            _baseUrl = baseUrl.TrimEnd('/');
        }

        public string BaseUrl => _baseUrl;

        public static async Task<VlcHttpClient> CreateAsync(string password, int[]? preferredPorts, CancellationToken ct)
        {
            // Try ports in a sane order: 8080 default, then 4212, then extras.
            var ports = new List<int> { 8080, 4212 };
            if (preferredPorts != null) ports.AddRange(preferredPorts.Where(p => p > 0));
            ports = ports.Distinct().ToList();

            foreach (var port in ports)
            {
                var http = BuildClient(password);
                var baseUrl = $"http://127.0.0.1:{port}";
                var client = new VlcHttpClient(http, baseUrl);

                try
                {
                    // Probe status.json
                    _ = await client.GetStatusJsonAsync(ct);
                    return client;
                }
                catch
                {
                    client.Dispose();
                }
            }

            throw new InvalidOperationException("Could not connect to VLC HTTP interface on any tested port (8080/4212/custom).");
        }

        private static HttpClient BuildClient(string password)
        {
            var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };

            // VLC uses Basic auth. Username is usually empty.
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{password}"));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            return http;
        }

        public async Task<string> GetStatusJsonAsync(CancellationToken ct)
        {
            var url = $"{_baseUrl}/requests/status.json";
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct);
        }

        public Task SendCommandAsync(string command, CancellationToken ct = default)
            => SendCommandAsync(command, null, ct);

        public async Task SendCommandAsync(string command, Dictionary<string, string>? parameters, CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            sb.Append($"{_baseUrl}/requests/status.json?command={Uri.EscapeDataString(command)}");

            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    sb.Append('&')
                      .Append(Uri.EscapeDataString(kv.Key))
                      .Append('=')
                      .Append(Uri.EscapeDataString(kv.Value));
                }
            }

            using var resp = await _http.GetAsync(sb.ToString(), ct);
            resp.EnsureSuccessStatusCode();
        }

        public Task SeekAsync(double percent, CancellationToken ct = default)
        {
            percent = Math.Clamp(percent, 0, 100);
            return SendCommandAsync("seek", new() { ["val"] = $"{percent:0}%" }, ct);
        }

        public void Dispose() => _http.Dispose();
    }
}
