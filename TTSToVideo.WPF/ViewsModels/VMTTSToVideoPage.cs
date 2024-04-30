using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NAudio.Wave;
using NetXP.Exceptions;
using NetXP.ImageGeneratorAI;
using NetXP.Processes;
using NetXP.Processes.Implementations;
using NetXP.TTS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TTSToVideo.Business;
using TTSToVideo.Business.Models;
using TTSToVideo.WPF.Helpers;
using TTSToVideo.WPF.Models;
using Message = TTSToVideo.WPF.Models.Message;

namespace TTSToVideo.WPF.ViewsModels
{

    public partial class VMTTSToVideoPage : ObservableRecipient
    {
        private readonly IImageGeneratorAI imageGeneratorAI;
        private readonly ITTS tts;
        private readonly ITTSToVideoBusiness ttsToVideoBusiness;

        public ObservableCollection<ProjectModel>? ProjectsNames { get; set; } = [];

        public VMConfiguration VMConf { get; }
        public IIOTerminal Terminal { get; }
        public IMapper Mapper { get; }
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
        private TTSToVideoModel? model;
        private bool isGeneratingPortrait;

        public string? FinalProjectVideoPathWithVoice { get; private set; }
        public CancellationTokenSource? CancellationTokenSource { get; private set; }

        public VMTTSToVideoPage(IImageGeneratorAI imageGeneratorAI,
                          ITTS tts,
                          VMConfiguration configuration,
                          IIOTerminal terminal,
                          IMapper mapper,
                          ITTSToVideoBusiness ttsToVideoBusiness
        )
        {
            ProcessCommand = new AsyncRelayCommand(ProcessCommandExecute);
            SaveCommand = new AsyncRelayCommand(SaveCommandExecute);
            CancelCommand = new AsyncRelayCommand(CancelCommandExecute);
            OpenExplorerCommand = new AsyncRelayCommand(OpenExplorer);
            OpenVideoCommand = new AsyncRelayCommand(OpenVideo);
            UploadImageCommand = new AsyncRelayCommand(UploadImageCommandExecute);
            GeneratePortraitImageCommand = new AsyncRelayCommand(GeneratePortraitImageCommandExecute);
            GeneratePortraitVideoCommand = new AsyncRelayCommand(GeneratePortraitVideoCommandExecute);
            ProjectNameSelectionChangedCommand = new AsyncRelayCommand<string>(ProjectNameSelectionChangedCommandExecute);


            this.imageGeneratorAI = imageGeneratorAI;
            this.tts = tts;
            this.VMConf = configuration;
            this.Terminal = terminal;
            Mapper = mapper;
            this.ttsToVideoBusiness = ttsToVideoBusiness;
            this.Model = new TTSToVideoModel
            {
                Prompt = ""
            };
        }

        private async Task SaveCommandExecute()
        {
            if (this.Model == null)
            {
                throw new CustomApplicationException("There is nothing to save (Model empty).");
            }

            var path = GetProjectPath(this.Model.ProjectName);

            Directory.CreateDirectory(path);

            await SaveModel(path);
        }
        private async Task GeneratePortraitVideoCommandExecute()
        {
            if (this.Model == null)
            {
                throw new CustomApplicationException("Model not defined.");
            }

            if (this.Model.PortraitImagePath == null)
            {
                throw new CustomApplicationException("PortraitImagePath empty.");
            }

            if (this.Model.ProjectName == null)
            {
                throw new CustomApplicationException("ProjectName empty.");
            }

            var path = GetProjectPath(this.Model.ProjectName);

            //Save video in path
            await this.ttsToVideoBusiness.GeneratePortraitVideoCommandExecute(
                  Path.Combine(path, this.Model.PortraitImagePath)
                , Path.Combine(path, this.Model.PortraitVideoPath));
        }
        private async Task GeneratePortraitImageCommandExecute()
        {
            try
            {
                isGeneratingPortrait = true;

                this.Validation();

                this.CancellationTokenSource = new CancellationTokenSource();
                var token = CancellationTokenSource.Token;

                string projectFullPath = GetProjectPath(Model.ProjectName);

                Directory.CreateDirectory(projectFullPath);

                var statement = new StatementModel
                {
                    Text = string.IsNullOrEmpty(this.Model.PortraitPrompt?.Trim()) ? this.Model.PortraitText : this.Model.PortraitPrompt
                };

                //Map StatementModel to Statement
                var statementForBusiness = new Statement
                {
                    Prompt = statement.Text,
                    NegativePrompt = Model.NegativePrompt,
                };

                await this.ttsToVideoBusiness.GeneratePortraitImageCommandExecute(statementForBusiness, projectFullPath, token);

                this.Model.PortraitImagePath = Path.GetFileName(statementForBusiness.Images.First().Path);

                var fullPath = this.GetProjectPath(this.Model.ProjectName);
                await this.SaveModel(fullPath);
            }
            finally
            {
                isGeneratingPortrait = false;
            }
        }
        private async Task UploadImageCommandExecute()
        {
            var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.png;*.gif;*.bmp;*.jpeg"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var fullProjectPath = this.GetProjectPath(this.Model.ProjectName);
                var imageName = Path.GetFileName(openFileDialog.FileName);
                if (!File.Exists(openFileDialog.FileName))
                {
                    File.Copy(openFileDialog.FileName, Path.Combine(fullProjectPath, imageName), true);
                }
                this.Model.PortraitImagePath = imageName;

                await this.SaveModel(fullProjectPath);
            }

            await Task.Delay(1);
        }

        public async Task Init()
        {
            await this.VMConf.Init();

            this.VMConf.Model.ProjectsNames = new ObservableCollection<string>();

            if (!Directory.Exists(this.VMConf.Model.ProjectBaseDir))
            {
                Directory.CreateDirectory(this.VMConf.Model.ProjectBaseDir);
            }
            else
            {
                var directories = Directory.GetDirectories(this.VMConf.Model.ProjectBaseDir, $"*");
                this.ProjectsNames = new ObservableCollection<ProjectModel>(directories.Select(o => new ProjectModel
                {
                    FileName = Path.GetFileName(o),
                    FullPath = o,
                    ProjectName = Path.GetFileName(o),
                }));
            }
        }
        private async Task OpenVideo()
        {
            if (!File.Exists(this.FinalProjectVideoPathWithVoice))
            {
                throw new CustomApplicationException("Video not created.");
            }

            Process.Start(
                new ProcessStartInfo
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
            this.Model = new TTSToVideoModel();

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
            List<StatementModel> statements = null;

            try
            {
                if (this.Model == null)
                {
                    throw new Exception("Model not defined.");
                }

                this.CancellationTokenSource = new CancellationTokenSource();
                var token = CancellationTokenSource.Token;

                WeakReferenceMessenger.Default.Send(new Message { Text = "" });

                string projectFullPath = Validation();

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
                string[] paragraphs = Model.Prompt.Split(new string[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                statements = paragraphs.Select(o => new StatementModel { Text = o }).ToList();

                StatementModel? statementPortraitVoice = null;
                if (Model.PortraitEnabled)
                {
                    if (this.Model.PortraitImagePath == null)
                    {
                        await this.GeneratePortraitImageCommandExecute();
                    }

                    statementPortraitVoice = new StatementModel()
                    {
                        Images = [
                            new()
                            {
                                Path = Path.Combine(projectFullPath, this.Model.PortraitImagePath
                                                                     ?? throw new CustomApplicationException("PortraitImagePath empty"))
                            }
                        ],
                        Text = (string.IsNullOrEmpty(this.Model.PortraitVoice) ? this.Model.PortraitText : this.Model.PortraitVoice) ??
                                  throw new CustomApplicationException("PortraitText or PortraitVoice empty"),

                        AudioPath = Path.Combine(projectFullPath, "portrait-voice.wav")
                    };
                }

                await this.SaveModel(projectFullPath);


                await ttsToVideoBusiness.ProcessCommandExecute(
                      projectFullPath
                    , Model.ProjectName
                    , Model.Prompt
                    , Model.NegativePrompt
                    , Model.PortraitEnabled
                    , Model.PortraitText
                    , Model.PortraitVoice,
                      Model.PortraitImagePath,
                      Model.PortraitVideoPath,
                      VMConf.Model.MusicDir, token);

            }
            finally
            {
                this.CancellationTokenSource?.Dispose();

                WeakReferenceMessenger.Default.Send(new Message { Text = "Process Finished." });
            }
        }

        private string Validation()
        {
            if (string.IsNullOrEmpty(this.Model.ProjectName))
            {
                throw new CustomApplicationException("Project Name Empty");
            }

            if (string.IsNullOrEmpty(this.Model.Prompt))
            {
                throw new CustomApplicationException("Text Empty");
            }

            string projectFullPath = GetProjectPath(this.Model.ProjectName);

            var invalidChars = Path.GetInvalidFileNameChars();
            var invalidCharsInPath = Model.ProjectName.Where(o => invalidChars.Any(a => a == o));
            if (invalidCharsInPath.Any())
            {
                throw new CustomApplicationException($"There are invalid chars in project name => \"{string.Join(",", invalidCharsInPath)}\"");
            }

            if (Model.PortraitEnabled)
            {
                if (string.IsNullOrEmpty(this.Model.PortraitText))
                {
                    throw new CustomApplicationException("Portrait text not written");
                }

                var projectPath = GetProjectPath(this.Model.ProjectName);
                if (string.IsNullOrEmpty(this.Model.PortraitImagePath)
                    && !isGeneratingPortrait && !File.Exists(Path.Combine(projectPath, this.Model.PortraitVideoPath)))
                {
                    throw new CustomApplicationException("Portrait image or video not uploaded or generated");
                }
            }

            return projectFullPath;
        }


        private async Task SaveModel(string basePath)
        {
            var json = JsonConvert.SerializeObject(this.Model);
            await File.WriteAllTextAsync(Path.Combine(basePath, "TTSToVideo.json"), json);
        }

        private async Task LoadModel(string basePath)
        {
            var configurationFile = Path.Combine(basePath, "TTSToVideo.json");
            string? json = null;
            if (File.Exists(configurationFile))
            {
                json = await File.ReadAllTextAsync(configurationFile);
            }

            if (json == null)
            {
                this.Model = new TTSToVideoModel();
            }
            else
            {
                var model = JsonConvert.DeserializeObject<TTSToVideoModel>(json)
                    ?? throw new Exception($"Error reading configuration of the project \"{Path.GetFileName(basePath)}\"");

                this.Model = new TTSToVideoModel();

                this.Mapper.Map(model, this.Model);

                var portraitImagePath = "";
                if (this.Model.PortraitImagePath != null)
                {
                    portraitImagePath = Path.Combine(basePath, this.Model.PortraitImagePath);
                }

                if (this.Model.PortraitImagePath != null && File.Exists(portraitImagePath))
                {
                    BitmapImage bitmapImage = new();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(portraitImagePath);
                    bitmapImage.EndInit();

                    await Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                    {
                        this.Model.PortraitImage = bitmapImage;
                    });
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
                var projectFullPath = Path.Combine($"{this.VMConf.Model.ProjectBaseDir}", p);
                return projectFullPath;
            }
            return "";
        }

    }
}
