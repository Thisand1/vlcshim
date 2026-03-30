using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using VlcShimDebugFr;

namespace VlcShimDebugFr
{
    internal class StatusShim
    {
        // Required bs, like state (playing, paused or stopped) and title
        public required string State { get; set; }
        public required string Title { get; set; }
        public string? Artist { get; set; }
        public string? Filename { get; set; }
        // non-required bs
        public int Time { get; set; }
        public int Length { get; set; }
        public int Volume { get; set; }
        public double Position { get; set; } // 0.0-1.0
        public MediaPlaybackAutoRepeatMode RepeatMode { get; set; }
        public bool IsShuffleEnabled { get; set; }
        public double Rate { get; set; } = 1.0;
    }
}
