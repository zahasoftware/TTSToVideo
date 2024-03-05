using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using TTSToVideo.WPF.Models;

namespace TTSToVideo.WPF.ViewModel
{
    public interface IVMTTSToVideoPage : INotifyPropertyChanged
    {
        TTSToVideoModel Model { get; set; }
        IVMConfiguration VMConf { get; }

        AsyncRelayCommand ProcessCommand { get; set; }
        AsyncRelayCommand SaveCommand { get; set; }
        AsyncRelayCommand CancelCommand { get; set; }
        AsyncRelayCommand OpenExplorerCommand { get; set; }
        AsyncRelayCommand OpenVideoCommand { get; set; }
        AsyncRelayCommand UploadImageCommand { get; set; }
        AsyncRelayCommand GeneratePortraitImageCommand { get; set; }
        AsyncRelayCommand<string> ProjectNameSelectionChangedCommand { get; set; }

        Task Init();
    }
}
