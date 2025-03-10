﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.Helpers.Implementations.Ffmpeg
{
    public class FfmpegOptions
    {
        public FfmpegFontStyle FontStyle { get; set; }

        public int WidthResolution { get; set; }
        public int HeightResolution { get; set; }
        public object AdditionalArgs { get; internal set; }
        public TimeSpan? MarginEndDuration { get; set; }
    }
}
