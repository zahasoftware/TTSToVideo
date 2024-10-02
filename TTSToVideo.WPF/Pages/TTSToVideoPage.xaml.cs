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
using TTSToVideo.WPF.Models;
using TTSToVideo.WPF.Pages;
using TTSToVideo.WPF.ViewsModels;

namespace TTSToVideo.WPF
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class TTSToVideoPage : Page
    {
        public TTSToVideoPage(TTSToVideoViewModel ttsToVideo, IServiceProvider serviceProvider)
        {
            InitializeComponent();
            this.DataContext = ttsToVideo;
            this.ttsToVideo = ttsToVideo;
            ServiceProvider = serviceProvider;
            this.Loaded += MainPage_Loaded;
        }

        public TTSToVideoViewModel ttsToVideo { get; }
        public IServiceProvider ServiceProvider { get; }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            await this.ttsToVideo.Init();
        }

        private void OpenFontStyleWindow(object sender, RoutedEventArgs e)
        {
            var window = (ServiceProvider.GetService(typeof(FontStyleWindowsView)) as FontStyleWindowsView);
            //Get the current item of the datagrid
            var item = (sender as Button).DataContext as StatementModel;
            (window.DataContext as FontStyleViewModel).Statement = item;
            window.ShowDialog();
        }
    }
}
