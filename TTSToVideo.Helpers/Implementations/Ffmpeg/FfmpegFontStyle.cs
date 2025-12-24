namespace TTSToVideo.Helpers.Implementations.Ffmpeg
{
    public class FfmpegFontStyle
    {
        public FfmpegAlignment? Alignment { get; set; } = FfmpegAlignment.TopCenter;

        //MarginV is the vertical margin from the bottom or top of the screen
        public int? MarginV { get; set; } 

        //MarginL is the horizontal margin from the left side of the screen
        public int? MarginL { get; set; }

        //MarginR is the margin from the right side of the screen
        public int? MarginR { get; set; }

        public int? FontSize { get; set; }

        public bool? SubtitleVisible { get; set; } = true;

        // Text color in ARGB format (e.g., #FFFFFFFF for white)
        // Default: White fully opaque
        public string? TextColor { get; set; } = "#FFFFFFFF";

        // Background color in ARGB format (e.g., #64000000 for semi-transparent black)
        // Default: Black with 40% opacity
        public string? BackgroundColor { get; set; } = "#FF000000";

        public override string ToString()
        {
            return $"Alignment: {Alignment}, MarginV: {MarginV}, MarginL: {MarginL}, MarginR: {MarginR}, FontSize: {FontSize}, SubtitleVisible: {SubtitleVisible}, TextColor: {TextColor}, BackgroundColor: {BackgroundColor}";
        }
    }
}