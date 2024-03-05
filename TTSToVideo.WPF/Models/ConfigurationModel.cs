using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.WPF.Models
{
    [AddINotifyPropertyChangedInterface]

    public class ConfigurationModel : ObservableRecipient
    {
        public string? ProjectBaseDir { get; set; }

        public string? MusicDir { get; set; }

        [JsonIgnore]
        public ObservableCollection<string>? ProjectsNames { get; set; }

    }
}
