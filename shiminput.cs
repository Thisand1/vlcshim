using System;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Drawing.Text;
using Windows.Media;
using Windows.Media.Control;
using System.Diagnostics;
using VlcShimDebugFr;
namespace VlcShimDebugFr
{
    internal class ShimInput
    {
        private readonly VlcHttpClient _vlc;

        public ShimInput(VlcHttpClient vlc)
        {
            _vlc = vlc;
        }

        public async Task<StatusShim> GetStatusAsync(CancellationToken ct = default)
        {
            string json = await _vlc.GetStatusJsonAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string state = root.TryGetProperty("state", out var stateProp) && stateProp.ValueKind == JsonValueKind.String
                ? stateProp.GetString() ?? "unknown"
                : "unknown";

            int time = root.TryGetProperty("time", out var timeProp) && timeProp.TryGetInt32(out var timeVal)
                ? timeVal
                : 0;

            int length = root.TryGetProperty("length", out var lengthProp) && lengthProp.TryGetInt32(out var lengthVal)
                ? lengthVal
                : 0;

            int volume = root.TryGetProperty("volume", out var volumeProp) && volumeProp.TryGetInt32(out var volumeVal)
                ? volumeVal
                : 0;

            double position = root.TryGetProperty("position", out var positionProp) && positionProp.TryGetDouble(out var posVal)
                ? posVal
                : 0d;

            bool isRandom = root.TryGetProperty("random", out var randomProp) && randomProp.ValueKind == JsonValueKind.True;
            bool repeatTrack = root.TryGetProperty("repeat", out var repeatProp) && repeatProp.ValueKind == JsonValueKind.True;
            bool repeatPlaylist = root.TryGetProperty("loop", out var loopProp) && loopProp.ValueKind == JsonValueKind.True;

            var repeatMode = repeatTrack
                ? MediaPlaybackAutoRepeatMode.Track
                : repeatPlaylist
                    ? MediaPlaybackAutoRepeatMode.List
                    : MediaPlaybackAutoRepeatMode.None;

            double rate = root.TryGetProperty("rate", out var rateProp) && rateProp.TryGetDouble(out var rateVal)
                ? rateVal
                : 1.0;

            string songTitle = "Nothing playing";
            string? songFilename = null;
            string? songArtist = null;

            if (root.TryGetProperty("information", out var info) &&
                info.TryGetProperty("category", out var cat) &&
                cat.TryGetProperty("meta", out var meta))
            {
                if (meta.TryGetProperty("filename", out var fileProp))
                {
                    string? rawFilename = fileProp.GetString();
                    if (!string.IsNullOrWhiteSpace(rawFilename))
                        songFilename = Path.GetFileName(rawFilename);
                }

                if (meta.TryGetProperty("title", out var titleProp))
                {
                    string? parsedTitle = titleProp.GetString();
                    if (!string.IsNullOrWhiteSpace(parsedTitle))
                        songTitle = parsedTitle;
                    else if (!string.IsNullOrWhiteSpace(songFilename))
                        songTitle = songFilename;
                }
                else if (!string.IsNullOrWhiteSpace(songFilename))
                {
                    songTitle = songFilename;
                }

                if (meta.TryGetProperty("artist", out var artistProp))
                {
                    string? rawArtist = artistProp.GetString();
                    if (!string.IsNullOrWhiteSpace(rawArtist))
                        songArtist = rawArtist;
                }
            }

            return new StatusShim
            {
                State = state,
                Title = songTitle,
                Artist = songArtist,
                Filename = string.IsNullOrWhiteSpace(songFilename) ? null : songFilename,
                Time = time,
                Length = length,
                Volume = volume,
                Position = position,
                RepeatMode = repeatMode,
                IsShuffleEnabled = isRandom,
                Rate = rate
            };
        }
    }
}
