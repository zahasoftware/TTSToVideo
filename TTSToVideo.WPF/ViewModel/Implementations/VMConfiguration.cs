using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.WPF.ViewModel.Implementations
{
    public class VMConfiguration : ObservableRecipient, IVMConfiguration
    {
        public void Init()
        {
            if (string.IsNullOrEmpty(this.ProjectBaseDirPrefix))
            {
                this.ProjectBaseDirPrefix = "P-";
            }

            if (string.IsNullOrEmpty(this.ProjectBaseDir))
            {
                this.ProjectBaseDir = Directory.GetCurrentDirectory();
            }
        }

        private string? projectBaseDir;
        public string? ProjectBaseDir { get => projectBaseDir; set => SetProperty(ref projectBaseDir, value, true); }

        public string? ProjectBaseDirPrefix { get => projectBaseDirPrefix; set => SetProperty(ref projectBaseDirPrefix, value, true); }
        private string? projectBaseDirPrefix;

    }
}
