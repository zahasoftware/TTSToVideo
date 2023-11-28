using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
namespace TTSToVideo.WPF.Models
{

    public class MTTSToVideo : ObservableRecipient
    {

        public string _prompt;
        public string Prompt { get => _prompt; set => SetProperty(ref _prompt, value, true); }

        public string _negativePrompt;
        public string NegativePrompt { get => _negativePrompt; set => SetProperty(ref _negativePrompt, value); }

        public string _projectName;
        public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value, true); }

        private string _projectNameSelected;
        public string ProjectNameSelected { get => _projectNameSelected; set => SetProperty(ref _projectNameSelected, value); }

        private string portraitBackgroundColor;
        public string PortraitBackgroundColor { get => portraitBackgroundColor; set => SetProperty(ref portraitBackgroundColor, value); }

        private string portraitText;
        public string PortraitText { get => portraitText; set => SetProperty(ref portraitText, value); }

        private string portraitVoice;
        public string PortraitVoice { get => portraitVoice; set => SetProperty(ref portraitVoice, value); }

        private string portraitImagePath;
        public string PortraitImagePath { get => portraitImagePath; set => SetProperty(ref portraitImagePath, value); }

        private BitmapImage portraitImage;
        [JsonIgnore]
        public BitmapImage PortraitImage { get => portraitImage; set => SetProperty(ref portraitImage, value); }

        private bool portraitEnabled;
        public bool PortraitEnabled { get => portraitEnabled; set => SetProperty(ref portraitEnabled, value); }

        //public Image
    }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
