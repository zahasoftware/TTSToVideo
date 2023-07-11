using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.WPF.ViewModel
{
    public interface IVMMainPage: INotifyPropertyChanged
    {
        public RelayCommand ProcessCommand { get; set; }
        public string Text  { get; set; }
    }
}
