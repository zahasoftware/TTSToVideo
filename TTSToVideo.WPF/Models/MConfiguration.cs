using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.WPF.Models
{
    public class MConfiguration : ObservableRecipient
    {
        private string? projectBaseDir;
        public string? ProjectBaseDir { get => projectBaseDir; set => SetProperty(ref projectBaseDir, value, true); }

        public string? ProjectBaseDirPrefix { get => projectBaseDirPrefix; set => SetProperty(ref projectBaseDirPrefix, value, true); }
        private string? projectBaseDirPrefix;

        private string? musicDir;
        public string? MusicDir { get => musicDir; set => SetProperty(ref musicDir, value, true); }

          }
}
