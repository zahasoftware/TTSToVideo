using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TTSToVideo.WPF.Models;

namespace TTSToVideo.WPF.ViewModel
{
    public interface IVMConfiguration : INotifyPropertyChanged
    {
        MConfiguration Model { get; set; }
        Task Init();
        AsyncRelayCommand SaveCommand { get; set; }

    }
}
