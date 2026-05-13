namespace TTSToVideo.Business.Models
{
    public class TtsToVideoImageOptions
    {
        public bool UseOnlyFirstImage { get; set; }
        public bool UseTextForPrompt { get; set; }
        public bool CreateVideo { get; set; }
        public string? Seed { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
}