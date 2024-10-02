using System.IO;

namespace TTSToVideo.WPF.Models
{
    public class MusicModel
    {
        public string? Display => $"{Path.GetFileName(FilePath)}";

        public string? FilePath { get; set; }

    }
}