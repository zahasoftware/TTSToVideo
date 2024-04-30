using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TTSToVideo.WPF.Models;

namespace TTSToVideo.WPF.ViewsModels
{
    public class VMMainWindow : ObservableRecipient
    {
        private string? message;
        private VMTTSToVideoPage? mainPage;
        private VMConfiguration? _VMConf;

        public VMMainWindow(VMTTSToVideoPage mainPage)
        {
            MainPage = mainPage;
            VMConf = mainPage.VMConf;

            WeakReferenceMessenger.Default.Register<Message>(this, (o, a) =>
            {
                Message = a.Text;
            });
        }

        public VMTTSToVideoPage MainPage
        {
            get => mainPage ?? throw new ArgumentNullException(nameof(MainPage));
            set => SetProperty(ref mainPage, value);
        }

        public string Message
        {
            get => message ?? "";
            set => SetProperty(ref message, value, true);
        }

        public VMConfiguration VMConf
        {
            get => _VMConf ?? throw new ArgumentNullException(nameof(_VMConf));
            set => SetProperty(ref _VMConf, value, true);
        }

    }
}
