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
using System.Windows.Shapes;
using TTSToVideo.WPF.Models;
using TTSToVideo.WPF.ViewsModels;

namespace TTSToVideo.WPF.Pages
{
    /// <summary>
    /// Interaction logic for NewCategory.xaml
    /// </summary>
    public partial class NewCategoryView : Window
    {
        public NewCategoryView(NewCategoryViewModel categoryViewModel)
        {
            InitializeComponent();
            DataContext = categoryViewModel;
            categoryViewModel.Close += CategoryViewModel_Close;
        }

        private void CategoryViewModel_Close(CategoryModel obj)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
