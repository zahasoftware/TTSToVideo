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
        public TTSToVideoPage(IVMMainPage MainPageViewModel)
        {
            InitializeComponent();
            this.DataContext = MainPageViewModel;
            this.MainPageViewModel = MainPageViewModel;

            this.Loaded += MainPage_Loaded;
        }

        public IVMMainPage MainPageViewModel { get; }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.MainPageViewModel.Init();
        }
    }
}
