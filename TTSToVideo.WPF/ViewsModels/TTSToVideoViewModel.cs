using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetXP;
using NetXP.Exceptions;
using NetXP.ImageGeneratorAI;
using NetXP.Processes;
using NetXP.Tts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TTSToVideo.Business;
using TTSToVideo.Business.Models;
using TTSToVideo.Helpers;
using TTSToVideo.WPF.Models;
using Message = TTSToVideo.WPF.Models.Message;

namespace TTSToVideo.WPF.ViewsModels
{

    public partial class TTSToVideoViewModel(IMessage message,
                          IImageGeneratorAI imageGeneratorAI,
                          ITts tts,
                          ConfigurationViewModel configuration,
                          IIOTerminal terminal,
                          IMapper mapper,
                          ITTSToVideoBusiness ttsToVideoBusiness,
                          FontStyleViewModel fontStyleViewModel
        ) : ObservableRecipient
    {

        /// <summary>
        /// Dont change this models to Model, because it will break the binding with the view, when json deserializes the model
        /// </summary>
        public ObservableCollection<ProjectModel>? ProjectsNames { get; set; } = [];
        public ObservableCollection<ImageModel>? ImagesModels { get; set; } = [];
        public ObservableCollection<MusicModel>? MusicModels { get; set; } = [];
        public ObservableCollection<VoiceModel>? VoicesModels { get; set; } = [];


        public AsyncRelayCommand<object?> DeletePictureCommand { get; set; }
        public AsyncRelayCommand<object?> OpenPictureCommand { get; set; }

        public AsyncRelayCommand ProcessCommand { get; set; }
        public AsyncRelayCommand SaveCommand { get; set; }
        public AsyncRelayCommand CancelCommand { get; set; }
        public AsyncRelayCommand OpenExplorerCommand { get; set; }
        public AsyncRelayCommand OpenVideoCommand { get; set; }
        public AsyncRelayCommand UploadImageCommand { get; set; }
        public AsyncRelayCommand GeneratePortraitImageCommand { get; set; }
        public AsyncRelayCommand GeneratePortraitVideoCommand { get; set; }
        public AsyncRelayCommand<string> ProjectNameSelectionChangedCommand { get; set; }

        [ObservableProperty]
        private TtsToVideoModel? model;
        private bool isGeneratingPortrait;

        public string? FinalProjectVideoPathWithVoice { get; private set; }
        public CancellationTokenSource? CancellationTokenSource { get; private set; }
        public FontStyleViewModel FontStyleViewModel { get; } = fontStyleViewModel;

        private async Task SaveCommandExecute()
        {
            ArgumentNullException.ThrowIfNull(this.Model);
            ArgumentNullException.ThrowIfNull(this.Model.ProjectName);

            var path = GetProjectPath(this.Model.ProjectName);

            Directory.CreateDirectory(path);

            await SaveModel(path);

            message.Info("Project saved.");

        }

        private async Task GeneratePortraitImageCommandExecute()
        {
            //try
            //{
            //    isGeneratingPortrait = true;
            //    this.PortraitValidation();

            //    this.CancellationTokenSource = new CancellationTokenSource();
            //    var token = CancellationTokenSource.Token;

            //    string projectFullPath = GetProjectPath(Model!.ProjectName!);
            //    Directory.CreateDirectory(projectFullPath);

            //    var statement = new StatementModel
            //    {
            //        Text = string.IsNullOrEmpty(this.Model.PortraitPrompt?.Trim()) ? this.Model.PortraitText! : this.Model.PortraitPrompt
            //    };

            //    //Map StatementModel to Statement
            //    var statementForBusiness =
            //        new Statement
            //        {
            //            Prompt = statement.Text + "," + Model.AditionalPrompt,
            //            NegativePrompt = Model.NegativePrompt + "," + configuration.Model.NegativePrompt,
            //        };

            //    await ttsToVideoBusiness.GeneratePortraitImageCommandExecute(
            //        statementForBusiness,
            //        [Model.ImageModelSelected!.Id],
            //        projectFullPath,
            //        token);

            //    this.Model.PortraitImagePath = Path.GetFileName(statementForBusiness.Images.First().Path);
            //    var fullPath = this.GetProjectPath(this.Model.ProjectName);
            //    await this.SaveModel(fullPath);
            //}
            //finally
            //{
            //    isGeneratingPortrait = false;
            //}
        }

        private async Task UploadImageCommandExecute()
        {
            //var openFileDialog = new System.Windows.Forms.OpenFileDialog
            //{
            //    Filter = "Image Files|*.jpg;*.png;*.gif;*.bmp;*.jpeg"
            //};

            //if (openFileDialog.ShowDialog() == DialogResult.OK)
            //{
            //    var fullProjectPath = this.GetProjectPath(this.Model.ProjectName);
            //    var imageName = Path.GetFileName(openFileDialog.FileName);
            //    if (!File.Exists(openFileDialog.FileName))
            //    {
            //        File.Copy(openFileDialog.FileName, Path.Combine(fullProjectPath, imageName), true);
            //    }
            //    this.Model.PortraitImagePath = imageName;

            //    await this.SaveModel(fullProjectPath);
            //}

            //await Task.Delay(1);
        }

        public async Task Init()
        {
            await configuration.Init();

            ProcessCommand = new AsyncRelayCommand(ProcessCommandExecute);
            SaveCommand = new AsyncRelayCommand(SaveCommandExecute);
            CancelCommand = new AsyncRelayCommand(CancelCommandExecute);
            OpenExplorerCommand = new AsyncRelayCommand(OpenExplorer);
            OpenVideoCommand = new AsyncRelayCommand(OpenVideo);
            UploadImageCommand = new AsyncRelayCommand(UploadImageCommandExecute);
            GeneratePortraitImageCommand = new AsyncRelayCommand(GeneratePortraitImageCommandExecute);
            ProjectNameSelectionChangedCommand = new AsyncRelayCommand<string>(ProjectNameSelectionChangedCommandExecute);
            DeletePictureCommand = new AsyncRelayCommand<object?>(DeletePictureCommandExecute);
            OpenPictureCommand = new AsyncRelayCommand<object?>(OpenPictureCommandExecute);

            this.Model = new TtsToVideoModel
            {
                Prompt = ""
            };

            configuration.Model.ProjectsNames = [];

            //Loading music directory
            var musicFiles = Directory.GetFiles(configuration.Model.MusicDir, "*.wav");

            foreach (var musicFile in musicFiles)
            {
                MusicModels.Add(new MusicModel { FilePath = musicFile });
            }

            //Loading models
            _ = imageGeneratorAI.GetModels().ContinueWith((task) =>
            {
                if (task.Exception != null)
                {
                    throw task.Exception;
                }

                var models = task.Result;
                ImagesModels = new ObservableCollection<ImageModel>(models);
            });

            //Loading models voices
            _ = tts.GetTtsVoices().ContinueWith((task) =>
            {
                if (task.Exception != null)
                {
                    throw task.Exception;
                }

                var models = task.Result.Select(o => new VoiceModel
                {
                    Gender = o.Gender,
                    Id = o.Id,
                    Language = o.Language,
                    ModelId = o.ModelId,
                    Name = o.Name,
                    Tags = o.Tags
                });

                VoicesModels = new ObservableCollection<VoiceModel>(models);
            });

            if (!Directory.Exists(configuration.Model.ProjectBaseDir))
            {
                Directory.CreateDirectory(configuration.Model.ProjectBaseDir);
            }
            else
            {
                var directories = Directory.GetDirectories(configuration.Model.ProjectBaseDir, $"*");
                this.ProjectsNames = new ObservableCollection<ProjectModel>(directories.Select(o => new ProjectModel
                {
                    FileName = Path.GetFileName(o),
                    FullPath = o,
                    ProjectName = Path.GetFileName(o),
                }));
            }
        }

        private async Task DeletePictureCommandExecute(object? statement)
        {
            if (await message.Confirm("Are you sure you want to delete this picture?") == false)
                return;

            if (statement is StatementModel statementModel)
            {
                if (statementModel.AudioPath != null && Path.Exists($"{statementModel.AudioPath}.mp4"))
                {
                    File.Delete($"{statementModel.AudioPath}.mp4");
                }

                var images = this.Model.Statements.FirstOrDefault(o => o == statementModel)?.Images;
                foreach (var image in images)
                {
                    var path = image.Path;
                    image.Path = "";
                    if (path != null && File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                var imagePath = this.Model.Statements.FirstOrDefault(o => o == statementModel)?.Images[0].Path;

                var fullPathProject = GetProjectPath(this.Model.ProjectName);

                var musicFinalPath = Path.Combine(fullPathProject, $"{Model.ProjectName}-Music-Final.mp4");
                if (File.Exists(musicFinalPath))
                {
                    File.Delete(musicFinalPath);
                }

                var projectPath = Path.Combine(fullPathProject, $"{Model.ProjectName}.mp4");
                if (File.Exists(projectPath))
                {
                    File.Delete(projectPath);
                }

                var finalPath = Path.Combine(fullPathProject, $"{Model.ProjectName}-Final.mp4");
                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }
            }
        }

        private async Task OpenPictureCommandExecute(object? statement)
        {
            //Open the picture in the default image viewer
            if (statement is StatementModel statementModel)
            {
                if (statementModel.Images.Count > 0)
                {
                    var path = statementModel.Images[0].Path;
                    if (path != null && Path.Exists(path))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = $"\"{path}\"",
                            UseShellExecute = true,
                            Verb = "open"
                        });
                    }
                }
            }
            await Task.Delay(0);
        }

        private async Task OpenVideo()
        {
            if (!File.Exists(this.FinalProjectVideoPathWithVoice))
            {
                throw new CustomApplicationException("Video not created.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = $"\"{this.FinalProjectVideoPathWithVoice}\"",
                UseShellExecute = true,
                Verb = "open"
            });
            await Task.Delay(0);
        }

        private async Task OpenExplorer()
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = $"\"{this.GetProjectPath(this.Model.ProjectName)}\"",
                    UseShellExecute = true,
                    Verb = "open"
                });
            await Task.Delay(0);
        }

        private async Task ProjectNameSelectionChangedCommandExecute(string? p)
        {
            this.Model = new TtsToVideoModel();

            if (p == null)
            {
                return;
            }

            string projectFullPath = GetProjectPath(p);

            await this.LoadModel(projectFullPath);

            this.FinalProjectVideoPathWithVoice = projectFullPath + "\\" + $"{p}-Final.mp4";
        }

        private async Task ProcessCommandExecute()
        {
            List<StatementModel> statementsModel;

            try
            {
                Validation();

                ArgumentNullException.ThrowIfNull(this.Model);
                ArgumentNullException.ThrowIfNull(this.Model.ProjectName);
                ArgumentNullException.ThrowIfNull(this.ProjectsNames);
                ArgumentNullException.ThrowIfNull(this.Model.Prompt);
                ArgumentNullException.ThrowIfNull(configuration.Model.MusicDir);
                ArgumentNullException.ThrowIfNull(this.Model.ImageModelSelected);
                ArgumentNullException.ThrowIfNull(this.Model.ImageModelSelected.Id);
                ArgumentNullException.ThrowIfNull(this.Model.VoiceModelSelected);

                this.CancellationTokenSource = new CancellationTokenSource();
                var token = CancellationTokenSource.Token;


                string projectFullPath = GetProjectPath(this.Model.ProjectName);

                Directory.CreateDirectory(projectFullPath);

                // Check if the project name already exists in the list
                if (!ProjectsNames.Any(p => p.ProjectName == this.Model.ProjectName))
                {
                    ProjectsNames.Add(new ProjectModel
                    {
                        ProjectName = this.Model.ProjectName,
                        FileName = this.Model.ProjectName,
                        FullPath = projectFullPath,
                    });
                }

                //Split text process text with dot and paragraph
                string[] paragraphs = Model.Prompt.Split("\n", StringSplitOptions.RemoveEmptyEntries);
                paragraphs = paragraphs.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();

                var firstParagraph = paragraphs.First();
                statementsModel = paragraphs.Select((o, i) =>
                {
                    StatementModel? statementModel = null;
                    if (Model.Statements != null && i < Model.Statements.Count)
                        statementModel = Model.Statements[i];

                    if (Model.UseOnlyFirstImage && i > 0)
                    {
                        return new StatementModel
                        {
                            Text = o,
                            Images = [new() { Path = Model.Statements[0].Images[0].Path }],
                            FontStyle = statementModel?.FontStyle ?? new FfmpegFontStyle()
                        };
                    }

                    var imageFileName = $"{o[..Math.Min(o.Length, Constants.MAX_PATH)]}";
                    imageFileName = Path.Combine(projectFullPath, $"{PathHelper.CleanFileName(imageFileName)}.jpg");

                    return new StatementModel
                    {
                        Text = o,
                        Images = [new() { Path = imageFileName }],
                        FontStyle = statementModel?.FontStyle ?? new FfmpegFontStyle()
                    };
                }).ToList();

                Model.Statements = new ObservableCollection<StatementModel>(statementsModel);

                await this.SaveModel(projectFullPath);

                var statements = statementsModel.Select(o => o.ToStatement()).ToList();

                await ttsToVideoBusiness.ProcessCommandExecute(
                      projectFullPath
                    , Model.ProjectName
                    , statements
                    , Model.NegativePrompt + "," + configuration.Model.NegativePrompt
                    , Model.AditionalPrompt ?? ""
                    , [Model.ImageModelSelected.Id]
                    , new TtsVoice
                    {
                        Id = Model.VoiceModelSelected.Id,
                        ModelId = Model.VoiceModelSelected.ModelId
                    },
                      Model.PortraitEnabled,
                      new TTSToVideoOptions
                      {
                          DurationBetweenVideo = TimeSpan.FromSeconds(2),
                          DurationEndVideo = TimeSpan.FromSeconds(7),
                          MusicDir = configuration.Model.MusicDir,
                          ImageOptions = new TtsToVideoImageOptions
                          {
                              UseOnlyFirstImage = Model.UseOnlyFirstImage,
                              UseTextForPrompt = Model.UseTextForPrompt,
                              CreateVideo = Model.CreateVideo
                          }
                      }, token);

                await LoadModel(projectFullPath);

            }
            finally
            {
                this.CancellationTokenSource?.Dispose();
            }
        }

        private string PortraitValidation()
        {
            CommonValidation();

            ArgumentNullException.ThrowIfNull(this.Model);
            ArgumentNullException.ThrowIfNull(this.Model.ProjectName);

            string projectFullPath = GetProjectPath(this.Model.ProjectName);

            //if (Model.PortraitEnabled)
            //{
            //    if (string.IsNullOrEmpty(this.Model.PortraitText))
            //    {
            //        throw new CustomApplicationException("Portrait text not written");
            //    }

            //    var projectPath = GetProjectPath(this.Model.ProjectName);
            //    if (string.IsNullOrEmpty(this.Model.PortraitImagePath)
            //        && !isGeneratingPortrait && !File.Exists(Path.Combine(projectPath, this.Model.PortraitVideoPath)))
            //    {
            //        throw new CustomApplicationException("Portrait image or video not uploaded or generated");
            //    }


            //}

            return projectFullPath;
        }

        private void CommonValidation()
        {
            ArgumentNullException.ThrowIfNull(this.Model);

            if (string.IsNullOrEmpty(this.Model.ProjectName))
            {
                throw new CustomApplicationException("Project Name Empty");
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var invalidCharsInPath = Model.ProjectName.Where(o => invalidChars.Any(a => a == o));
            if (invalidCharsInPath.Any())
            {
                throw new CustomApplicationException($"There are invalid chars in project name => \"{string.Join(",", invalidCharsInPath)}\"");
            }

            if (Model.ImageModelSelected == null)
            {
                throw new CustomApplicationException("Image Model not selected");
            }
        }

        private void Validation() 
        {
            ArgumentNullException.ThrowIfNull(this.Model);

            if (string.IsNullOrEmpty(this.Model.Prompt)) 
            {
                throw new CustomApplicationException("Text Empty");
            }

            if (string.IsNullOrEmpty(configuration.Model.MusicDir)) 
            {
                throw new CustomApplicationException("Music Directory Empty or does not exist");
            } 

            if (!Directory.Exists(configuration.Model.MusicDir)) 
            {
                throw new CustomApplicationException("Music Directory does not exist");
            }

            if (Model.ImageModelSelected == null)
            {
                throw new CustomApplicationException("Image Model not selected");
            }

            if (Model.VoiceModelSelected == null)
            { 
                throw new CustomApplicationException("Voice Model not selected");
            } 

        }

        private async Task SaveModel(string basePath)
        {
            var json = JsonConvert.SerializeObject(this.Model);
            await File.WriteAllTextAsync(Path.Combine(basePath, "TTSToVideo.json"), json);
        }

        private async Task LoadModel(string basePath)
        {
            ArgumentNullException.ThrowIfNull(this.ImagesModels);

            var configurationFile = Path.Combine(basePath, "TTSToVideo.json");
            string? json = null;
            if (File.Exists(configurationFile))
            {
                json = await File.ReadAllTextAsync(configurationFile); 
            }

            if (json == null) 
            {
                this.Model = new TtsToVideoModel();
            }
            else
            {
                var model = JsonConvert.DeserializeObject<TtsToVideoModel>(json)
                    ?? throw new Exception($"Error reading configuration of the project \"{Path.GetFileName(basePath)}\"");

                this.Model = new TtsToVideoModel();

                mapper.Map(model, this.Model);

                this.Model.ImageModelSelected = this.ImagesModels.FirstOrDefault(o => o.Id == model.ImageModelSelected?.Id);

                if (VoicesModels != null && model.VoiceModelSelected != null)
                {
                    this.Model.VoiceModelSelected = this.VoicesModels.FirstOrDefault(o => o.Id == model.VoiceModelSelected?.Id);
                }

                if (MusicModels != null && model.MusicModelSelected != null)
                {
                    this.Model.MusicModelSelected = this.MusicModels.FirstOrDefault(o => o.FilePath == model.MusicModelSelected?.FilePath);
                }

                var projectFullPath = GetProjectPath(this.Model.ProjectName);

                if (!string.IsNullOrEmpty(Model.Prompt))
                {
                    string[] paragraphs = Model.Prompt.Split("\n", StringSplitOptions.RemoveEmptyEntries);
                    paragraphs = paragraphs.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();

                    var statements = paragraphs.Select((o, i) =>
                    {
                        if (Model.UseOnlyFirstImage && i > 0)
                        {
                            return new StatementModel
                            {
                                Text = o,
                                Images = [new() { Path = Model.Statements[0].Images[0].Path }],
                                FontStyle = new FfmpegFontStyle()
                            };
                        }

                        var pathNoExtensionAndNumber = $"{o[..Math.Min(o.Length, Constants.MAX_PATH)]}";
                        var pathImage = Path.Combine(projectFullPath, $"{PathHelper.CleanFileName(pathNoExtensionAndNumber)}.jpg");
                        var pathAudio = Path.Combine(projectFullPath, $"{PathHelper.CleanFileName(pathNoExtensionAndNumber)}.wav");

                        return new StatementModel
                        {
                            Text = o,
                            Images = [new() { Path = pathImage }],
                            AudioPath = pathAudio
                        };

                    }).ToList();

                    Model.Statements = new ObservableCollection<StatementModel>(statements);
                }
            }
        }

        private async Task CancelCommandExecute()
        {
            this.CancellationTokenSource?.Cancel();
            await Task.Delay(0);
        }

        private string GetProjectPath(string p)
        {
            if (p != null)
            {
                var projectFullPath = Path.Combine($"{configuration.Model.ProjectBaseDir}", p);
                return projectFullPath;
            }
            return "";
        }

    }
}
