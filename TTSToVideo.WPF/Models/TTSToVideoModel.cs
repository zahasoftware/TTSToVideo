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
using PropertyChanged;

namespace TTSToVideo.WPF.Models
{

    [AddINotifyPropertyChangedInterface]
    public class TTSToVideoModel : ObservableRecipient
    {
        public string? Prompt { get; set; }

        public string? NegativePrompt { get; set; }

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
    }
}
