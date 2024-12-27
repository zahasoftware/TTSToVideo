using CommunityToolkit.Mvvm.Input;
using NetXP.Exceptions;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TTSToVideo.WPF.Models;

namespace TTSToVideo.WPF.ViewsModels
{
    [AddINotifyPropertyChangedInterface]
    public class NewCategoryViewModel
    {
        public NewCategoryViewModel(ConfigurationViewModel configurationViewModel)
        {
            CreateCategoryCommand = new AsyncRelayCommand(CreateCategory);
            ConfigurationViewModel = configurationViewModel;
        }

        public CategoryModel? Model { get; set; } = new CategoryModel();
        public event Action<CategoryModel>? Close;

        public AsyncRelayCommand CreateCategoryCommand { get; set; }
        public ConfigurationViewModel ConfigurationViewModel { get; }

        private void Validate()
        {
            if (string.IsNullOrEmpty(Model.CategoryName))
            {
                throw new CustomApplicationException("Please enter a category name");
            }
        }

        public Task CreateCategory()
        {
            Validate();

            //make category directory using basedir and category name
            var categoryDir = Path.Combine(ConfigurationViewModel.Model.ProjectBaseDir, Model.CategoryName);

            if (Directory.Exists(categoryDir))
            {
                throw new CustomApplicationException("Category Already Exists.");
            }
            else
            {
                Directory.CreateDirectory(categoryDir);

                //Add new category to the list
                var newCategory = new CategoryModel
                {
                    DirectoryPath = categoryDir,
                    CategoryName = Path.GetFileName(categoryDir)
                };

                Close?.Invoke(newCategory);
            }

            return Task.Delay(1);
        }
    }
}
