using System.Windows;
using TTSToVideo.WPF.ViewsModels;

namespace TTSToVideo.WPF.Pages
{
    public partial class FontStyleWindowsView : Window
    {
        public FontStyleWindowsView(FontStyleViewModel fontStyleViewModel)
        {
            InitializeComponent();
            DataContext = fontStyleViewModel;

        }

    }
}
