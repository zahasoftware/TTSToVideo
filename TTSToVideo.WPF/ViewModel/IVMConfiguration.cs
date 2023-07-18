using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.WPF.ViewModel
{
    public interface IVMConfiguration : INotifyPropertyChanged
    {
        public string ProjectBaseDir { get; set; }
        public string ProjectBaseDirPrefix { get; set; }
        void Init();
    }
}
