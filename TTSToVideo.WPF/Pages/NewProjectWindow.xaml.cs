using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
using System.Windows.Shapes;
using TTSToVideo.WPF.ViewsModels;

namespace TTSToVideo.WPF.Pages
{
    public partial class NewProjectWindow : System.Windows.Window
    {
        private readonly NewProjectViewModel newProjectViewModel;
        private readonly IServiceProvider serviceProvider;
        private readonly TTSToVideoViewModel ttsToVideoViewModel;

        public ConfigurationViewModel ConfViewModel { get; }

        // ...
        public NewProjectWindow(ConfigurationViewModel confViewModel
            , NewProjectViewModel newProjectViewModel
            , IServiceProvider serviceProvider
            , TTSToVideoViewModel ttsToVideoViewModel)
        {
            InitializeComponent();
            ConfViewModel = confViewModel;
            this.newProjectViewModel = newProjectViewModel;
            this.serviceProvider = serviceProvider;
            this.ttsToVideoViewModel = ttsToVideoViewModel;
            this.DataContext = newProjectViewModel;

            // Center the window in the parent window
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            this.Loaded += (sender, args) =>
            {
                newProjectViewModel.CloseNewProject += (s, p) =>
                {
                    ttsToVideoViewModel.CleanProject();
                    this.Close();
                };
            };
        }

        private void NewCategory_Click(object sender, RoutedEventArgs e)
        {
            var newCategoryWindow = serviceProvider.GetService(typeof(NewCategoryView)) as NewCategoryView;
            if (newCategoryWindow == null)
            {
                MessageBox.Show("Failed to create new category window.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            newCategoryWindow.Owner = this;
            newCategoryWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            newCategoryWindow.ShowDialog();
        }
    }

}
