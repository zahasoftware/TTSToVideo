namespace TTSToVideo.Business.Models
{
    public class TTSToVideoPortraitParams
    {
        public bool Enable { get; set; }
        public string? Text { get; set; }
        public string? Voice { get; set; }
        public string? ImagePath { get; set; }
        public string? VideoPath { get; set; }
    }
}