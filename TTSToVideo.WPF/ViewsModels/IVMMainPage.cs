using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.WPF.ViewModel
{
    public interface IVMMainPage : INotifyPropertyChanged
    {
        IVMConfiguration VMConf { get; }
        AsyncRelayCommand ProcessCommand { get; set; }
        AsyncRelayCommand CancelCommand { get; set; }
        AsyncRelayCommand OpenExplorerCommand { get; set; }
        AsyncRelayCommand OpenVideoCommand { get; set; }
        AsyncRelayCommand<string> ProjectNameSelectionChangedCommand { get; set; }
        string Prompt { get; set; }
        string NegativePrompt { get; set; }
        string ProjectName { get; set; }
        ObservableCollection<string> ProjectsNames { get; set; }
        string ProjectNameSelected { get; set; }
        Task Init();
    }
}
