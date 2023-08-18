using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Ribbon;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TTSToVideo.WPF;
using TTSToVideo.WPF.Pages;
using TTSToVideo.WPF.ViewModel;

namespace TTSToVideo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IMainWindow
    {
        public IMainPage mainPage;
        private readonly IPage<ConfigurationPage> confPage;

        public IVMMainWindow ViewModel { get; }

        public MainWindow(
              IMainPage mainPage
            , IPage<ConfigurationPage> confPage
            , IVMMainWindow vmMainWindow)
        {
            InitializeComponent();

            this.mainPage = mainPage;
            this.mainFrame.Navigate(mainPage);

            this.DataContext = vmMainWindow;
            this.confPage = confPage;
            this.ViewModel = vmMainWindow;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            double targetWidth = screenWidth * 0.6;
            double targetHeight = screenHeight * 0.6;

            Width = targetWidth;
            Height = targetHeight;

            // Center the window on the screen
            Left = (screenWidth - targetWidth) / 2;
            Top = (screenHeight - targetHeight) / 2;
        }


        private void RibbonWin_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && this.mainFrame != null)
            {
                var selectedTab = e.AddedItems[0] as RibbonTab;
                if (selectedTab != null)
                {
                    // Perform actions based on the selected tab
                    if (selectedTab.Name == "tabHome")
                    {
                        this.mainFrame.Navigate(mainPage);
                    }
                    else if (selectedTab.Name == "tabConfiguration")
                    {
                        this.mainFrame.Navigate(confPage);
                    }
                    // Add more conditions for other tabs as needed
                }
            }
        }
    }
}
