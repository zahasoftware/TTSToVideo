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
        public string? TextColor { get; set; } = Constants.FONT_COLOR_DETAUL;

        // Background color in ARGB format (e.g., #64000000 for semi-transparent black)
        // Default: Black with 40% opacity
        public string? BackgroundColor { get; set; } = Constants.FONT_BACKCOLOR_DEFAULT;

        // Font family name (e.g., "Arial", "Times New Roman")
        // Default: Arial
        public string? Fontname { get; set; } = "Arial";

        // Secondary color in ARGB format (used for karaoke effects)
        // Default: Blue fully opaque
        public string? SecondaryColour { get; set; } = Constants.FONT_COLOR_DETAUL;

        // Outline/border color in ARGB format
        // Default: Black fully opaque
        public string? OutlineColour { get; set; } = Constants.FONT_BACKCOLOR_DEFAULT;

        // Bold text (0 = normal, 1 = bold, or weight value)
        // Default: 0 (normal weight)
        public int? Bold { get; set; } = 0;

        // Italic text (0 = normal, 1 = italic)
        // Default: 0 (normal)
        public int? Italic { get; set; } = 0;

        // Underline text (0 = no underline, 1 = underline)
        // Default: 0 (no underline)
        public int? Underline { get; set; } = 0;

        // Strike-out/strikethrough text (0 = no strike, 1 = strike)
        // Default: 0 (no strike)
        public int? StrikeOut { get; set; } = 0;

        // Horizontal text scaling percentage (100 = normal)
        // Default: 100
        public int? ScaleX { get; set; } = 100;

        // Vertical text scaling percentage (100 = normal)
        // Default: 100
        public int? ScaleY { get; set; } = 100;

        // Character spacing (0 = normal, positive = wider, negative = narrower)
        // Default: 0
        public double? Spacing { get; set; } = 0;

        // Text rotation angle in degrees (0-360)
        // Default: 0 (no rotation)
        public double? Angle { get; set; } = 0;

        // Border style (1 = outline+shadow, 3 = opaque box, 4 = outline+opaque box)
        // Default: 3 (opaque box)
        public int? BorderStyle { get; set; } = 3;

        // Outline/border width in pixels (also used as box padding when BorderStyle=3)
        // Default: 4
        public int? Outline { get; set; } = 4;

        // Shadow depth in pixels
        // Default: 0 (no shadow)
        public int? Shadow { get; set; } = 0;

        public override string ToString()
        {
            return $"Alignment: {Alignment}, MarginV: {MarginV}, MarginL: {MarginL}, MarginR: {MarginR}, FontSize: {FontSize}, " +
                   $"SubtitleVisible: {SubtitleVisible}, TextColor: {TextColor}, BackgroundColor: {BackgroundColor}, " +
                   $"Fontname: {Fontname}, Bold: {Bold}, Italic: {Italic}, Underline: {Underline}, StrikeOut: {StrikeOut}, " +
                   $"ScaleX: {ScaleX}, ScaleY: {ScaleY}, Spacing: {Spacing}, Angle: {Angle}, BorderStyle: {BorderStyle}, " +
                   $"Outline: {Outline}, Shadow: {Shadow}";
        }
    }
}