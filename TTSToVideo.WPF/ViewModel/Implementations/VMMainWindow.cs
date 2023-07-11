using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.WPF.ViewModel.Implementations
{
    public class VMMainWindow : IVMMainWindow
    {
        public VMMainWindow(IVMMainPage mainPage)
        {
            MainPage = mainPage;
        }

        public IVMMainPage MainPage { get; set; }
    }
}
