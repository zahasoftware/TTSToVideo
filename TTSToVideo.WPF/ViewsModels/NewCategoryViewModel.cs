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
using System.Windows.Input;
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

        public ObservableCollection<ChatAIModel>? ChatAIModels { get; set; } = [];
        public CategoryModel? Model { get; set; } = new CategoryModel();
        public event Action<CategoryModel>? Close;

        public AsyncRelayCommand CreateCategoryCommand { get; set; }
        public ConfigurationViewModel ConfigurationViewModel { get; }

        public event Action<CategoryModel>? LoadCategoryEvent;

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

        public void SaveCategory()
        {
            //Save the category model as json file in the category directory
            var categoryDir = Path.Combine(ConfigurationViewModel.Model.ProjectBaseDir, Model.CategoryName);
            if (Directory.Exists(categoryDir))
            {
                var categoryFilePath = Path.Combine(categoryDir, "category.json");
                File.WriteAllText(categoryFilePath, System.Text.Json.JsonSerializer.Serialize(Model));
            }
            else
            {
                throw new CustomApplicationException("Category Directory does not exist.");
            }
        }

        internal void LoadCategory(string directoryPath)
        {
            //Load the category model from the json file in the category directory

            if (Directory.Exists(directoryPath))
            {
                var categoryFilePath = Path.Combine(directoryPath, "category.json");
                if (File.Exists(categoryFilePath))
                {
                    var categoryModel = System.Text.Json.JsonSerializer.Deserialize<CategoryModel>(File.ReadAllText(categoryFilePath));
                    if (categoryModel != null)
                    {
                        Model = categoryModel;
                        //Send an event to the TTSToVideoViewModel to load the category
                        LoadCategoryEvent?.Invoke(Model);
                    }
                }
            }
            else
            {
                throw new CustomApplicationException("Category Directory does not exist.");
            }

            this.Model.DirectoryPath = directoryPath;
            this.Model.CategoryName = Path.GetFileName(directoryPath);
        }

        private RelayCommand windowClosedCommand;
        public ICommand WindowClosedCommand => windowClosedCommand ??= new RelayCommand(WindowClosed);

        private void WindowClosed()
        {
            this.SaveCategory();
        }
    }
}
