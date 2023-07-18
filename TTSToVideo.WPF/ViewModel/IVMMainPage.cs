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
        public IVMConfiguration VMConfiguration { get; }
        public AsyncRelayCommand ProcessCommand { get; set; }
        public AsyncRelayCommand<string> ProjectNameSelectionChangedCommand { get; set; }
        public string Text { get; set; }
        public string ProjectName { get; set; }
        public ObservableCollection<string> ProjectsNames { get; set; }
        public string ProjectNameSelected { get; set; }

    }
}
