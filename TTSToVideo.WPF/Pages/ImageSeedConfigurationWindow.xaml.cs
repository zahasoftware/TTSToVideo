using System.Windows;
using TTSToVideo.WPF.ViewsModels;

namespace TTSToVideo.WPF.Pages
{
    public partial class ImageSeedConfigurationWindow : Window
    {
        public ImageSeedConfigurationWindow(TTSToVideoViewModel ttsToVideoViewModel)
        {
            InitializeComponent();
            DataContext = ttsToVideoViewModel;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is TTSToVideoViewModel vm && vm.SaveCommand != null && vm.ProjectSelected != null)
            {
                await vm.SaveCommand.ExecuteAsync(null);
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
