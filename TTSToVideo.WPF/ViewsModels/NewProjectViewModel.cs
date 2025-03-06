using CommunityToolkit.Mvvm.Input;
using NetXP;
using NetXP.Exceptions;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TTSToVideo.WPF.Models;

namespace TTSToVideo.WPF.ViewsModels
{

    [AddINotifyPropertyChangedInterface]
    public class NewProjectViewModel
    {
        public IMessage Message { get; }

        //Events
        public AsyncRelayCommand CreateProjectCommand { get; set; }
        public event EventHandler<ProjectModel> CloseNewProject;

        //ViewModels
        public ConfigurationViewModel ConfViewModel { get; }

        //Members
        public ProjectModel? Model { get; set; }
        public CategoryModel? SelectedCategory { get; set; }
        public ObservableCollection<CategoryModel> Categories { get; set; } = [];

        public NewProjectViewModel(ConfigurationViewModel confViewModel
            , IMessage message
            , NewCategoryViewModel newCategoryViewModel
            )
        {
            Model = new ProjectModel();

            ConfViewModel = confViewModel;
            Message = message;

            //Define relay command to create a new category
            CreateProjectCommand = new AsyncRelayCommand(CreateProject);

            newCategoryViewModel.Close += (category) =>
            {
                if (category != null)
                {
                    Categories.Add(category);
                    SelectedCategory = category;
                }
            };
        }

        public void Init()
        {
            //Define relay command to create a new category
            var baseDir = ConfViewModel.Model.ProjectBaseDir ?? throw new ApplicationException("Please set the project base directory in the configuration window");

            //validate if baseDir exists    
            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            //Load Categories
            var dirs = Directory.GetDirectories(baseDir);
            foreach (var dir in dirs)
            {
                Categories.Add(new CategoryModel
                {
                    DirectoryPath = dir,
                    CategoryName = Path.GetFileName(dir)
                });
            }
        }

        private async Task CreateProject()
        {
            //Validate Selected Category
            if (SelectedCategory == null)
            {
                throw new CustomApplicationException("Please select a category");
            }

            //Validate ProjectName
            if (string.IsNullOrEmpty(Model.ProjectName))
            {
                throw new CustomApplicationException("Please enter a project name");
            }

            //Validate folder name has no invalid characters
            if (Model.ProjectName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
            {
                throw new CustomApplicationException("Project name contains invalid characters");
            }

            //Validate if directory already exists
            var projectDir = Path.Combine(SelectedCategory.DirectoryPath, Model.ProjectName);
            if (Directory.Exists(projectDir))
            {
                throw new CustomApplicationException("Project directory already exists");
            }

            //Create Project Directory
            Directory.CreateDirectory(projectDir);

            //Closing form
            this.CloseNewProject?.Invoke(this, new ProjectModel
            {
                FullPath = projectDir,
                FileName = Path.GetFileName(projectDir),
                ProjectName = Model.ProjectName,
            });
        }


    }

}
