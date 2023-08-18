using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.WPF.ViewModel.Implementations
{
    public class VMMainWindow : ObservableRecipient, IVMMainWindow
    {
        private string message;
        public VMMainWindow(IVMMainPage mainPage)
        {
            MainPage = mainPage;
            this.VMConf = mainPage.VMConf;

            WeakReferenceMessenger.Default.Register<Message>(this, (o, a) =>
            {
                this.Message = a.Text;
            });
        }

        public IVMMainPage MainPage { get; set; }
        public string Message { get => message; set => SetProperty(ref message, value, true); }
        public IVMConfiguration VMConf { get; set; }
    }
}
