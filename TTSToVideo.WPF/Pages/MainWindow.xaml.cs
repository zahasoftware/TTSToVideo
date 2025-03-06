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
using TTSToVideo.Helpers;
using TTSToVideo.WPF;
using TTSToVideo.WPF.Pages;
using TTSToVideo.WPF.ViewsModels;

namespace TTSToVideo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public TTSToVideoPage mainPage;
        private readonly ConfigurationPage confPage;

        public MainWindowViewModel ViewModel { get; }
        public IServiceProvider ServiceProvider { get; }

        public MainWindow(
              TTSToVideoPage mainPage
            , ConfigurationPage confPage
            , NewProjectWindow newProjectWindow
            , MainWindowViewModel vmMainWindow
            , IServiceProvider serviceProvider
            , IProgressBar progressBar
            )
        {
            InitializeComponent();

            this.mainPage = mainPage;
            this.mainFrame.Navigate(mainPage);

            this.DataContext = vmMainWindow;
            this.confPage = confPage;
            this.ViewModel = vmMainWindow;
            ServiceProvider = serviceProvider;

            // Set the window to full screen
            WindowState = WindowState.Maximized;

            progressBar.Incrementing += (s, e) =>
            {
                this.ViewModel.ProgressBarValue = e;
            };

            progressBar.MessageChanged += (s, e) =>
            {
                this.ViewModel.Message = e;
            };
        }

        private void RibbonWin_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && this.mainFrame != null)
            {
                if (e.AddedItems[0] is RibbonTab selectedTab)
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

        private void OpenNewProjectWindow(object sender, RoutedEventArgs e)
        {
            NewProjectWindow? newProjectWindow = ServiceProvider.GetService(typeof(NewProjectWindow)) as NewProjectWindow;
            if (newProjectWindow != null)
            {
                newProjectWindow.Owner = this;
                newProjectWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                newProjectWindow?.ShowDialog();
            }
            else
            {
                // Handle the case where the service is not available
                MessageBox.Show("Unable to open new project window. Service not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

    }
}
