using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TTSToVideo.Helpers.Implementations.Ffmpeg
{
    public static class FFMPEGDefinitions
    {
        public static int WidthResolution { get; private set; } = 512;
        public static int HeightResolution { get; private set; } = 904;
    }
}
