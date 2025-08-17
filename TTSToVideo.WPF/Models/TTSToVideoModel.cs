
using CommunityToolkit.Mvvm.ComponentModel;
using NetXP.IAs.ImageGeneratorAI;
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

        public string? ChatPrompt { get; set; }

        public string? NegativePrompt { get; set; }

        public string? AditionalPrompt { get; set; }

        //public ProjectModel? ProjectSelected{ get; set; }//Cannot be here because it is reloaded from coniguration and if configuration is empty this back to empty as well

        public bool PortraitEnabled { get; set; }

        public double MusicVolume { get; set; }

        public ImageModel? ImageModelSelected { get; set; }
        public VoiceModel? VoiceModelSelected { get; set; }
        public MusicModel? MusicModelSelected { get; set; }
        public VideoModel? VideoModel { get; set; }

        public int SubtitleSize { get; set; } = TTSToVideo.Helpers.Constants.SUBTITTLE_SIZE_DEFAULT;

        [JsonIgnore]
        public ObservableCollection<StatementModel>? Statements { get; set; } = [];
        public string PromptOriginal { get; internal set; }
    }
}
