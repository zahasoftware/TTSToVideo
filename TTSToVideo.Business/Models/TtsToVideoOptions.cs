namespace TTSToVideo.Business.Models
{
    public class TTSToVideoOptions
    {
        public TimeSpan? DurationBetweenVideo { get; set; }
        public TimeSpan? DurationEndVideo { get; set; }
        public string? MusicDir { get; set; }
    }
}