
using CommunityToolkit.Mvvm.ComponentModel;
using NetXP.ImageGeneratorAI;
using PropertyChanged;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace TTSToVideo.WPF.Models
{

    [AddINotifyPropertyChangedInterface]
    public class TtsToVideoModel
    {
        public string? Prompt { get; set; }

        public string? NegativePrompt { get; set; }

        public string? AditionalPrompt { get; set; }

        public string? ProjectName { get; set; }

        public ProjectModel? ProjectNameSelected { get; set; }

        public string? PortraitBackgroundColor { get; set; }

        public string? PortraitText { get; set; }

        public string? PortraitVoice { get; set; }

        public string? PortraitImagePath { get; set; }

        [JsonIgnore]
        public BitmapImage? PortraitImage { get; set; }

        public bool PortraitEnabled { get; set; }

        public string? PortraitPrompt { get; set; }

        [JsonIgnore]
        public string PortraitVideoPath { get; set; } = "portrait-video.mp4";

        [JsonIgnore]
        public ObservableCollection<StatementModel>? Statements { get; set; }

        public ImageModel? ImageModelSelected { get; set; }
        public VoiceModel? VoiceModelSelected { get; set; }
        public string? PortraitImageFullPath { get; set; }
    }
}
