using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TTSToVideo.WPF.Models;

namespace TTSToVideo.WPF.ViewsModels
{
    [AddINotifyPropertyChangedInterface]

    public class MainWindowViewModel : ObservableRecipient
    {

        public MainWindowViewModel(TTSToVideoViewModel mainPage, ConfigurationViewModel configurationViewModel)
        {
            MainPage = mainPage;
            VMConf = configurationViewModel;
        }

        public TTSToVideoViewModel MainPage { get; set; }

        public string Message { get; set; }

        public ConfigurationViewModel VMConf { get; set; }

        public int ProgressBarValue { get; set; }
    }
}
