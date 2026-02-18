using System.Windows;
using System.Windows.Input;
using TTSToVideo.WPF.ViewsModels;

namespace TTSToVideo.WPF.Pages
{
    public partial class FontStyleWindowsView : Window
    {
        public FontStyleWindowsView(FontStyleViewModel fontStyleViewModel)
        {
            InitializeComponent();
            DataContext = fontStyleViewModel;

            // Attach the PreviewKeyDown event handler
            this.PreviewKeyDown += FontStyleWindowsView_PreviewKeyDown;
        }

        private void FontStyleWindowsView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Check if the Esc key was pressed
            if (e.Key == Key.Escape)
            {
                this.Close(); // Close the window
            }
        }
    }
}
