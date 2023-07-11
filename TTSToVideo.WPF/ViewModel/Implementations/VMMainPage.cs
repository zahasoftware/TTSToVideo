using GalaSoft.MvvmLight.Command;
using NetXP.ImageGeneratorAI;
using NetXP.TTS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.WPF.ViewModel.Implementations
{

    public class VMMainPage : GalaSoft.MvvmLight.ViewModelBase, IVMMainPage
    {
        private readonly IImageGeneratorAI imageGeneratorAI;
        private readonly ITTS tts;

        public RelayCommand ProcessCommand { get; set; }

        public string _text;
        public string Text { get  => _text; set {
                _text = value;
                RaisePropertyChanged(nameof(Text)); 
            } }


        public VMMainPage(IImageGeneratorAI imageGeneratorAI,
                          ITTS tts
        ) 
        {
            ProcessCommand = new RelayCommand(ProcessCommandExecute);
            this.imageGeneratorAI = imageGeneratorAI;
            this.tts = tts;
            this.Text = "ctm";
        }

        private void ProcessCommandExecute()
        {

        }
    }
}
