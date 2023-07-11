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
using TTSToVideo.WPF.ViewModel;

namespace TTSToVideo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IMainWindow
    {
        IMainPage mainPage;

        public MainWindow(IMainPage mainPage, IVMMainWindow vmMainWindow)
        {
            InitializeComponent();

            this.mainPage = mainPage;
            this.mainFrame.Navigate(mainPage);

            this.DataContext = vmMainWindow;
        }

        private void RibbonWin_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && this.mainFrame != null)
            {
                var selectedTab = e.AddedItems[0] as RibbonTab;
                if (selectedTab != null)
                {
                    // Perform actions based on the selected tab
                    if (selectedTab.Name == "Tab1")
                    {
                        this.mainFrame.Navigate(mainPage);
                    }
                    else if (selectedTab.Name == "SettingsTab")
                    {
                        // Handle Settings tab selection
                    }
                    // Add more conditions for other tabs as needed
                }
            }
        }
    }
}
