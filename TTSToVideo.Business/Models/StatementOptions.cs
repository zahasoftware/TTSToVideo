using TTSToVideo.Helpers.Implementations.Ffmpeg;

namespace TTSToVideo.Business.Models
{
    public class StatementOptions
    {
        public FfmpegFontStyle? FontStyle { get; set; }
        public int Index { get; set; }
        public string Id { get; set; }
        public string? PromptDebug { get; set; }
    }
}