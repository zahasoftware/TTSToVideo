
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
        public bool UseOnlyFirstImage { get; set; }

        public bool UseTextForPrompt { get; set; }

        public bool CreateVideo { get; set; }

        public string? Prompt { get; set; }

        public string? NegativePrompt { get; set; }

        public string? AditionalPrompt { get; set; }

        public string? ProjectName { get; set; }

        public ProjectModel? ProjectNameSelected { get; set; }

        public bool PortraitEnabled { get; set; }

        public double MusicVolume { get; set; }

        public ImageModel? ImageModelSelected { get; set; }
        public VoiceModel? VoiceModelSelected { get; set; }
        public MusicModel? MusicModelSelected { get; set; }

        [JsonIgnore]
        public ObservableCollection<StatementModel>? Statements { get; set; } = [];
    }
}
