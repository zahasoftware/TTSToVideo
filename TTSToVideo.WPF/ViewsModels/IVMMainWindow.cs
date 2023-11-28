using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.WPF.ViewModel
{
    public interface IVMMainWindow
    {
        IVMTTSToVideoPage MainPage { get; set; }
        IVMConfiguration VMConf { get; set; }
        string Message { get; set; }
    }
}
