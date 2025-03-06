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
using TTSToVideo.WPF.ViewsModels;

namespace TTSToVideo.WPF.Pages
{
    /// <summary>
    /// Interaction logic for ConfigurationPage.xaml
    /// </summary>
    public partial class ConfigurationPage : Page
    {
        private readonly ConfigurationViewModel configuration;

        public ConfigurationPage(ConfigurationViewModel configuration)
        {
            InitializeComponent();
            this.DataContext = configuration;

            Loaded += ConfigurationPage_Loaded;
            this.configuration = configuration;
        }

        private void ConfigurationPage_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
