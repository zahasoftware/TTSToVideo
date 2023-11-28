using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TTSToVideo.WPF.ViewModel;

namespace TTSToVideo.WPF
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class TTSToVideoPage : Page, IMainPage
    {
        public TTSToVideoPage(IVMTTSToVideoPage ttsToVideo)
        {
            InitializeComponent();
            this.DataContext = ttsToVideo;
            this.ttsToVideo = ttsToVideo;

            this.Loaded += MainPage_Loaded;
        }

        public IVMTTSToVideoPage ttsToVideo { get; }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.ttsToVideo.Init();
        }

    }
}
