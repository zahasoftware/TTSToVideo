using CommunityToolkit.Mvvm.ComponentModel;
using NetXP.IAs.ImageGeneratorAI;
using PropertyChanged;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

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
        public string? ImageSeed { get; set; }
        public int? ImageWidth { get; set; }
        public int? ImageHeight { get; set; }

        public ImageModel? ImageModelSelected { get; set; }
        public VoiceModel? VoiceModelSelected { get; set; }
        public MusicModel? MusicModelSelected { get; set; }
        public VideoModel? VideoModel { get; set; }

        [JsonIgnore]
        public ObservableCollection<StatementModel>? Statements { get; set; } = [];
        [JsonIgnore]

        private ObservableCollection<StatementModel>? _statementsFiltered;
        [JsonIgnore]
        public ObservableCollection<StatementModel>? StatementsFiltered
        {
            get => Statements == null
                ? (_statementsFiltered ??= [])
                : new ObservableCollection<StatementModel>(Statements.Where(static o => !string.IsNullOrEmpty(o.Text)));
            set => _statementsFiltered = value;
        }

        public string PromptOriginal { get; internal set; }
    }
}
