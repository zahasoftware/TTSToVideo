namespace TTSToVideo.Business.Models
{
    public class TTSToVideoOptions
    {
        public TtsToVideoImageOptions ImageOptions { get; set; } = new();
        public TtsToVideoMusicOptions MusicaOptions { get; set; } = new();

        public TimeSpan? DurationBetweenVideo { get; set; }
        public TimeSpan? DurationEndVideo { get; set; }
        public string? MusicDir { get; set; }
        public double MusicVolume { get; set; }
    }
}