namespace TTSToVideo.Helpers.Implementations.Ffmpeg
{
    public class FfmpegFontStyle
    {
        public FfmpegAlignment? Alignment { get; set; }

        //MarginV is the vertical margin from the bottom or top of the screen
        public int? MarginV { get; set; } = 20;

        //MarginL is the horizontal margin from the left side of the screen
        public int? MarginL { get; set; }

        //MarginR is the margin from the right side of the screen
        public int? MarginR { get; set; }

        public int? FontSize { get; set; }
    }
}