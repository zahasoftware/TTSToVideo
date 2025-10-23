using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetXP;
using NetXP.Exceptions;
using NetXP.IAs.Chat;
using NetXP.IAs.ImageGeneratorAI;
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
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TTSToVideo.Business;
using TTSToVideo.Business.Models;
using TTSToVideo.Helpers;
using TTSToVideo.Helpers.Implementations.Ffmpeg;
using TTSToVideo.WPF.Models;
using Message = TTSToVideo.WPF.Models.Message;

namespace TTSToVideo.WPF.ViewsModels
{

    public partial class TTSToVideoViewModel(IMessage message,
                          IImageGeneratorAI imageGeneratorAI,
                          ITts tts,
                          ConfigurationViewModel configuration,
                          IMapper mapper,
                          ITTSToVideoBusiness ttsToVideoBusiness,
                          FontStyleViewModel fontStyleViewModel,
                          NewProjectViewModel newProjectViewModel,
                          NewCategoryViewModel newCategoryViewModel,
                          NetXP.IAs.Chat.IAIChatService aIChatService,
                          ITranslator translator
        ) : ObservableRecipient
    {
        private static Regex RemoveTagsRegex() => new Regex("<.*?>", RegexOptions.Compiled); // Provide implementation for the partial method    

        /// <summary>
        /// Dont change this models to Model, because it will break the binding with the view, when json deserializes the model
        /// </summary>
        public ObservableCollection<ProjectModel>? ProjectsNames { get; set; } = [];
        public ObservableCollection<ImageModel>? ImagesModels { get; set; } = [];
        public ObservableCollection<MusicModel>? MusicModels { get; set; } = [];
        public ObservableCollection<VoiceModel>? VoicesModels { get; set; } = [];
        public ObservableCollection<ChatAIModel>? ChatAIModels { get; set; } = [];

        public AsyncRelayCommand<object?>? DeletePictureCommand { get; set; }
        public AsyncRelayCommand<StatementModel?>? OpenPictureCommand { get; set; }
        public AsyncRelayCommand<StatementModel?>? RegeneratePictureCommand { get; set; }

        public AsyncRelayCommand<StatementModel?>? OpenVideoCommand { get; set; }
        public AsyncRelayCommand<StatementModel?>? RegenerateVideoCommand { get; set; }
        public RelayCommand<StatementModel?>? DeleteVideoCommand { get; set; }

        public AsyncRelayCommand<object?>? OpenVoiceCommand { get; private set; }
        public AsyncRelayCommand<object?>? DeleteVoiceCommand { get; set; }
        public AsyncRelayCommand? ProcessCommand { get; set; }
        public AsyncRelayCommand? SaveCommand { get; set; }
        public AsyncRelayCommand? CancelCommand { get; set; }
        public AsyncRelayCommand? OpenExplorerCommand { get; set; }
        public AsyncRelayCommand? OpenFinalVideoCommand { get; set; }
        public AsyncRelayCommand? UploadImageCommand { get; set; }
        public AsyncRelayCommand? GeneratePortraitImageCommand { get; set; }
        public AsyncRelayCommand? GeneratePortraitVideoCommand { get; set; }
        public AsyncRelayCommand<string>? CategorySelectionChangedCommand { get; set; }
        public AsyncRelayCommand<ProjectModel?>? ProjectSelectionChangedCommand { get; set; }
        public AsyncRelayCommand<string>? TranslateToChangedCommand { get; set; }
        public AsyncRelayCommand<string>? ChatSendCommand { get; set; }
        public AsyncRelayCommand? CopyDescriptionToClipboardCommand { get; set; }
        public AsyncRelayCommand? CopyFullPathVideoCommand { get; set; }

        //Models
        public TtsToVideoModel? Model { get; set; }
        public ProjectModel? ProjectSelected { get; set; }
        public string? MessageRight { get; set; }

        public string? FinalProjectVideoPathWithVoice { get; private set; }
        public CancellationTokenSource? CancellationTokenSource { get; private set; }

        //ViewModels
        public FontStyleViewModel FontStyleViewModel { get; } = fontStyleViewModel;
        public NewProjectViewModel NewProjectViewModel { get; set; } = newProjectViewModel;
        public NewCategoryViewModel CategoryViewModel { get; set; } = newCategoryViewModel;

        public bool IsInitialized { get; internal set; }

        public List<string>? Languages { get; set; }
        public string? SelectedLanguage { get; set; } = Constants.LANG_DEFAULT;
        public List<SocialPlatforms>? SocialPlatforms { get; set; }
        public SocialPlatforms? SelectedPlatform { get; set; } = WPF.SocialPlatforms.Tiktok;


        private async Task SaveCommandExecute()
        {
            ArgumentNullException.ThrowIfNull(this.Model);

            if (ProjectSelected == null)
                throw new CustomApplicationException("Select a project");

            var fullPath = Path.Combine(ProjectSelected.FullPath, $"{SelectedPlatform}", SelectedLanguage);

            ArgumentNullException.ThrowIfNull(fullPath);

            Directory.CreateDirectory(fullPath);

            await SaveModel(fullPath);
            message.Info("Project saved.");
        }


        public async Task Init()
        {
            await configuration.Init();

            ProcessCommand = new AsyncRelayCommand(ProcessCommandExecute);
            SaveCommand = new AsyncRelayCommand(SaveCommandExecute);
            CancelCommand = new AsyncRelayCommand(CancelCommandExecute);
            OpenExplorerCommand = new AsyncRelayCommand(OpenExplorer);
            OpenFinalVideoCommand = new AsyncRelayCommand(OpenFinalVideoCommandExecute);

            OpenPictureCommand = new AsyncRelayCommand<StatementModel?>(OpenPictureCommandExecute);
            OpenVoiceCommand = new AsyncRelayCommand<object?>(OpenVoiceCommandExecute);
            OpenVideoCommand = new AsyncRelayCommand<StatementModel?>(OpenVideoCommandExecute);
            DeletePictureCommand = new AsyncRelayCommand<object?>(DeletePictureCommandExecute);
            DeleteVoiceCommand = new AsyncRelayCommand<object?>(DeleteVoiceCommandExecute);
            DeleteVideoCommand = new RelayCommand<StatementModel?>(DeleteVideoCommandExecute);
            RegeneratePictureCommand = new AsyncRelayCommand<StatementModel?>(RegeneratePictureCommandExecute);
            RegenerateVideoCommand = new AsyncRelayCommand<StatementModel?>(RegenerateVideoCommandExecute);

            CategorySelectionChangedCommand = new AsyncRelayCommand<string>(CategorySelectionChangedCommandExecute);
            ProjectSelectionChangedCommand = new AsyncRelayCommand<ProjectModel?>(ProjectSelectionChangedCommandExecute);
            TranslateToChangedCommand = new AsyncRelayCommand<string>(TranslateToChangedCommandExecute);
            ChatSendCommand = new AsyncRelayCommand<string>(ChatSendCommandExecute);
            CopyDescriptionToClipboardCommand = new AsyncRelayCommand(CopyDescriptionToClipboardCommandExcute);
            CopyFullPathVideoCommand = new AsyncRelayCommand(CopyFullPathVideoCommandExcute);

            NewProjectViewModel.CloseNewProject += (s, p) =>
            {
                ArgumentNullException.ThrowIfNull(this.ProjectsNames);
                this.ProjectsNames.Add(p);
                this.ProjectSelected = p;
            };

            NewProjectViewModel.Init();

            this.Model = new TtsToVideoModel
            {
                Prompt = ""
            };

            configuration.Model.ProjectsNames = [];

            //Loading music directory
            if (Directory.Exists(configuration.Model.MusicDir))
            {
                var musicFiles = Directory.GetFiles(configuration.Model.MusicDir, "*.wav");

                foreach (var musicFile in musicFiles)
                {
                    MusicModels?.Add(new MusicModel { FilePath = musicFile });
                }
            }

            //Loading Image models
            _ = imageGeneratorAI.GetModels().ContinueWith((task) =>
            {
                if (task.Exception != null)
                {
                    throw task.Exception;
                }

                var models = task.Result;
                ImagesModels = new ObservableCollection<ImageModel>(models);
            });

            //Loading voice models
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

            //Loading chat models
            _ = aIChatService.GetAvailableModelsAsync().ContinueWith((Task<List<ChatIAModelResponse>> task) =>
            {
                if (task.Exception != null)
                {
                    throw task.Exception;
                }

                var models = task.Result.Select(o => new ChatAIModel
                {
                    Id = o.Name,
                    Name = o.Name,
                    Description = $"{o.Name} ({o.Size / (1024.0 * 1024.0 * 1024.0):F2} GB)",
                });
                ChatAIModels = [.. models];
            });

            this.CategoryViewModel.LoadCategoryEvent += (categoryModel) =>
            {
                if (categoryModel == null || this.CategoryViewModel.Model == null || this.ChatAIModels == null) return;
                this.CategoryViewModel.Model.ChatAIModelSelected = this.ChatAIModels.FirstOrDefault(o => o.Id == categoryModel.ChatAIModelSelected?.Id);
            };

            if (!Directory.Exists(configuration.Model.ProjectBaseDir))
            {
                Directory.CreateDirectory(configuration.Model.ProjectBaseDir);
            }

            this.IsInitialized = true;
            var langs = $"{Constants.LANG_DEFAULT},en-US,fr-FR,de-DE,es-ES,it-IT,pt-BR,zh-CN,ja-JP,ko-KR".Split(',').ToList();
            this.Languages = langs;
        }

        private async Task TranslateToChangedCommandExecute(string? toLanguage)
        {
            if (toLanguage == null || this.Model == null || this.ProjectSelected == null)
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(this.Model);
            ArgumentNullException.ThrowIfNull(this.ProjectSelected);

            // Create subfolder for the translated project
            var translatedProjectName = $"{toLanguage}";
            var translatedProjectPath = Path.Combine(this.ProjectSelected.FullPath, $"{SelectedPlatform}", translatedProjectName);

            string translatedText = "";

            await this.LoadModel(this.ProjectSelected, SelectedPlatform.ToString(), SelectedLanguage);

            if (string.IsNullOrEmpty(this.Model.Prompt))
            {
                // Get default prompt if the current prompt is empty
                var defaultProjectPath = Path.Combine(this.ProjectSelected.FullPath, $"{SelectedPlatform}", Constants.LANG_DEFAULT);
                var defaultConfigPath = Path.Combine(defaultProjectPath, $"{Constants.CONFIG_FILE_PROJECT}");
                var defaultModel = JsonConvert.DeserializeObject<TtsToVideoModel>(await File.ReadAllTextAsync(defaultConfigPath));

                translatedText = await translator.TranslateTextAsync(defaultModel.Prompt, toLanguage);
                this.Model.Prompt = translatedText;
                message.Info($"Translated to {toLanguage}.");

                this.SaveModel(translatedProjectPath);
            }

            var projectDir = Path.Combine(this.ProjectSelected.FullPath, SelectedPlatform.ToString(), SelectedLanguage);
            this.ShowVideoDetails(projectDir);
        }

        private void DeleteVideoCommandExecute(StatementModel? arg)
        {
            var videoPath = arg?.ImageAnimatedPath;
            if (videoPath != null && File.Exists(videoPath))
            {
                File.Delete(videoPath);
                message.Info($"Video {videoPath} deleted successfully.");
            }
            else
            {
                message.Warn($"Video {videoPath} not found.");
            }
        }

        private async Task RegenerateVideoCommandExecute(StatementModel? model)
        {
            ArgumentNullException.ThrowIfNull(model);

            try
            {
                this.CancellationTokenSource = new CancellationTokenSource();
                var token = this.CancellationTokenSource.Token;

                var statementForBusiness = model.ToStatement();

                var imagePath= $"{statementForBusiness.Prompt[..Math.Min(statementForBusiness.Prompt.Length, Constants.MAX_PATH)]}";
                imagePath= Path.Combine(ProjectSelected.FullPath, SelectedPlatform.Value.ToString() , SelectedLanguage , $"{PathHelper.CleanFileName(imagePath)}.jpg");

                if (imagePath != null)
                {
                    //Generate video path form zero


                    model.ImageAnimatedPath = imagePath + ".mp4";

                    var request = new VideoGenerationRequest
                    {
                        Prompt = this.Model.UseOnlyFirstImage ? this.Model.AditionalPrompt : $"{this.Model.AditionalPrompt} {statementForBusiness.Prompt}",
                        SourceImagePath = imagePath,
                        Version = MotionVersion.Motion2,
                        MotionStrength = 5
                    };

                    await ttsToVideoBusiness.GeneratePortraitVideoCommandExecute(
                        request,
                        model.ImageAnimatedPath,
                        token);
                }
            }
            catch (Exception ex)
            {
                message.Error($"An error occurred while regenerating the video: {ex.Message}");
            }
            finally
            {
                this.CancellationTokenSource?.Dispose();
            }
        }

        private async Task RegeneratePictureCommandExecute(StatementModel? arg)
        {
            if (arg == null)
            {
                message.Warn("No statement provided to regenerate the picture.");
                return;
            }

            try
            {
                // Create cancellation token
                this.CancellationTokenSource = new CancellationTokenSource();
                var token = this.CancellationTokenSource.Token;

                // Define output folder
                var outputFolder =Path.Combine( this.ProjectSelected?.FullPath,$"{SelectedPlatform}", SelectedLanguage);
                if (string.IsNullOrEmpty(outputFolder))
                {
                    message.Warn("Project folder is not defined.");
                    return;
                }

                message.Info("Generating image.");

                // Map the statement to the business layer model
                var statementForBusiness = arg.ToStatement();
                statementForBusiness.GlobalPrompt = this.Model?.AditionalPrompt ?? "";

                statementForBusiness.Images.Clear();

                // Generate the image using the AI service
                await ttsToVideoBusiness.GeneratePortraitImageCommandExecute(
                    statementForBusiness,
                    new[] { Model.ImageModelSelected.Id },
                    outputFolder,
                    new TTSToVideoOptions
                    {
                        ImageOptions = new TtsToVideoImageOptions
                        {
                            UseTextForPrompt = true
                        }
                    },
                    token
                );

                // Update Model
                arg.Images = [.. mapper.Map<List<StatementImageModel>>(statementForBusiness.Images)];

                message.Info("Image regenerated successfully.");
            }
            catch (OperationCanceledException)
            {
                message.Warn("Image regeneration was canceled.");
            }
            catch (Exception ex)
            {
                message.Error($"An error occurred while regenerating the image: {ex.Message}");
            }
            finally
            {
                this.CancellationTokenSource?.Dispose();
            }
        }

        private async Task CopyFullPathVideoCommandExcute()
        {
            if (string.IsNullOrEmpty(this.FinalProjectVideoPathWithVoice) || !File.Exists(this.FinalProjectVideoPathWithVoice))
            {
                message.Warn("The final video path is either empty or the file does not exist.");
                return;
            }

            Clipboard.SetText(this.FinalProjectVideoPathWithVoice);
            message.Info("Final video path copied to clipboard.");

            await Task.CompletedTask;
        }




        private async Task CopyDescriptionToClipboardCommandExcute()
        {
            if (this.Model == null || string.IsNullOrEmpty(this.Model.Prompt))
            {
                message.Warn("No prompt available to copy.");
                return;
            }

            // Combine prompt and hashtags
            var description = this.Model.Prompt;
            if (!string.IsNullOrEmpty(this.CategoryViewModel.Model?.HashTags))
            {
                description += Environment.NewLine + this.CategoryViewModel.Model.HashTags;
            }

            // remove tags
            description = RemoveTagsRegex().Replace(description, string.Empty);

            // Copy to clipboard
            Clipboard.SetText(description);
            message.Info("Description and hashtags copied to clipboard.");

            await Task.CompletedTask;
        }

        private async Task ChatSendCommandExecute(string? prompt)
        {
            message.Info("Processing message...");
            ArgumentNullException.ThrowIfNull(this.CategoryViewModel.Model);
            ArgumentNullException.ThrowIfNull(this.ProjectsNames);

            // Check if the prompt is empty or null
            if (string.IsNullOrEmpty(prompt))
            {
                throw new CustomApplicationException("Prompt is empty or null.");
            }

            // Check if the AI model is selected
            if (this.CategoryViewModel.Model.ChatAIModelSelected == null || string.IsNullOrEmpty(this.CategoryViewModel.Model.ChatAIModelSelected.Name))
            {
                throw new CustomApplicationException("No AI Chat model selected.");
            }

            this.CancellationTokenSource = new CancellationTokenSource();
            var token = this.CancellationTokenSource.Token;

            var promptFull = prompt;

            promptFull += "\nHere the instruction:\n" + this.CategoryViewModel.Model.IAChatInstructions;

            // Add the messsage tha exclude the existing project names
            promptFull = promptFull + "\n\navoid using this following key words in the generation:\n" + string.Join("\n", this.ProjectsNames.Select(o => o.ProjectName).Distinct()) + "";

            await aIChatService.SetModelAsync(this.CategoryViewModel.Model.ChatAIModelSelected.Name, token);
            var response = await aIChatService.GenerateResponseAsync(promptFull, token);

            this.Model.Prompt = response;

            this.CategoryViewModel.SaveCategory();
        }

        private async Task DeleteVoiceCommandExecute(object? statement)
        {
            if (statement is not StatementModel statementModel || !await message.Confirm("Are you sure you want to delete this voice?"))
                return;

            var basePath = Path.Combine(ProjectSelected?.FullPath ?? string.Empty, SelectedPlatform.ToString(), SelectedLanguage);

            var pathsToDelete = new[]
            {
                statementModel.AudioPath,
                $"{statementModel.AudioPath}.wav",
                $"{statementModel.AudioPath}.mp4",
                $"{statementModel.AudioPath}.wav.mp4" ,
                Path.Combine(basePath,$"{ProjectSelected.ProjectName}-Music-Final.mp4"),
                Path.Combine(basePath,$"{ProjectSelected.ProjectName}.mp4"),
                Path.Combine(basePath,$"{ProjectSelected.ProjectName}-Final.mp4")
            }.Where(File.Exists);

            foreach (var file in pathsToDelete)
                File.Delete(file);
        }

        private async Task OpenVoiceCommandExecute(object? statement)
        {
            //Open the picture in the default image viewer
            if (statement is StatementModel statementModel)
            {
                var path = $"{statementModel.AudioPath}.wav";
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
            await Task.Delay(0);
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

                    if (path != null && File.Exists($"{path}.mp4"))
                    {
                        File.Delete($"{path}.mp4");
                    }

                    if (path != null && File.Exists($"{path}.wav.mp4"))
                    {
                        File.Delete($"{path}.wav.mp4");
                    }
                }

                var fullPathProject = Path.Combine(this.ProjectSelected.FullPath, SelectedPlatform.ToString(), SelectedLanguage);

                var musicFinalPath = Path.Combine(fullPathProject, $"{ProjectSelected.ProjectName}-Music-Final.mp4");
                if (File.Exists(musicFinalPath))
                {
                    File.Delete(musicFinalPath);
                }

                var projectPath = Path.Combine(fullPathProject, $"{ProjectSelected.ProjectName}.mp4");
                if (File.Exists(projectPath))
                {
                    File.Delete(projectPath);
                }

                var finalPath = Path.Combine(fullPathProject, $"{ProjectSelected.ProjectName}-Final.mp4");
                if (File.Exists(finalPath))
                {
                    File.Delete(finalPath);
                }
            }
        }

        private async Task OpenPictureCommandExecute(StatementModel? statement)
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

        private async Task OpenVideoCommandExecute(StatementModel? statementModel = null)
        {
            var path = statementModel?.ImageAnimatedPath;
            path ??= statementModel?.Images.FirstOrDefault()?.Path + ".mp4";
            await OpenVideo(path);
        }

        private async Task OpenFinalVideoCommandExecute()
        {
            this.FinalProjectVideoPathWithVoice = Path.Combine(ProjectSelected.FullPath, SelectedPlatform.ToString(), SelectedLanguage, $"{ProjectSelected.FileName}-Final.mp4");
            await OpenVideo(this.FinalProjectVideoPathWithVoice);
        }

        private static async Task OpenVideo(string path)
        {
            if (!File.Exists(path))
            {
                throw new CustomApplicationException("Video not created.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = $"\"{path}\"",
                UseShellExecute = true,
                Verb = "open"
            });
            

        }

        private async Task OpenExplorer()
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = Path.Combine(this.ProjectSelected.FullPath, SelectedPlatform.ToString(), SelectedLanguage),
                    UseShellExecute = true,
                    Verb = "open"
                });
            await Task.Delay(0);
        }

        private async Task CategorySelectionChangedCommandExecute(string? path)
        {
            //Load all directories from path to the list of projects
            if (path != null)
            {
                var directories = Directory.GetDirectories(path, $"*");
                this.ProjectsNames = [.. directories.Select(o => new ProjectModel
                {
                    FileName = Path.GetFileName(o),
                    FullPath = o,
                    ProjectName = Path.GetFileName(o),
                    CreatedAt = Directory.GetCreationTime(o),
                    Category =  new CategoryModel{
                         CategoryName = Path.GetFileName(path),
                         DirectoryPath = path
                    }
                })];

                ProjectsNames = [.. ProjectsNames.OrderByDescending(o => o.CreatedAt)];

                this.CategoryViewModel.LoadCategory(path);

            }
        }

        private async Task ProjectSelectionChangedCommandExecute(ProjectModel? pm)
        {
            if (pm == null || pm != this.ProjectSelected)
            {
                return;
            }

            await this.LoadModel(pm, SelectedPlatform.ToString(), SelectedLanguage);

            var projectDir = Path.Combine(pm.FullPath, $"{SelectedPlatform}", SelectedLanguage);
            this.FinalProjectVideoPathWithVoice = Path.Combine(projectDir, "{pm.FileName}-Final.mp4");

            this.ShowVideoDetails(projectDir);
        }

        private async Task ProcessCommandExecute()
        {
            List<StatementModel> statementsModel;

            try
            {
                Validation();

                ArgumentNullException.ThrowIfNull(this.Model);
                ArgumentNullException.ThrowIfNull(this.ProjectSelected.ProjectName);
                ArgumentNullException.ThrowIfNull(this.ProjectsNames);
                ArgumentNullException.ThrowIfNull(this.Model.Prompt);
                ArgumentNullException.ThrowIfNull(configuration.Model.MusicDir);
                ArgumentNullException.ThrowIfNull(this.Model.ImageModelSelected);
                ArgumentNullException.ThrowIfNull(this.Model.ImageModelSelected.Id);
                ArgumentNullException.ThrowIfNull(this.Model.VoiceModelSelected);
                ArgumentNullException.ThrowIfNull(this.Model.MusicModelSelected);

                this.CancellationTokenSource = new CancellationTokenSource();
                var token = CancellationTokenSource.Token;

                string projectFullPath = Path.Combine(this.ProjectSelected.FullPath, SelectedPlatform.ToString(), SelectedLanguage);

                Directory.CreateDirectory(projectFullPath);

                // Check if the project name already exists in the list
                if (!ProjectsNames.Any(p => p.ProjectName == this.ProjectSelected.ProjectName))
                {
                    ProjectsNames.Add(new ProjectModel
                    {
                        ProjectName = this.ProjectSelected.ProjectName,
                        FileName = this.ProjectSelected.ProjectName,
                        FullPath = projectFullPath,
                    });
                }

                /*
                if (this.Model.Prompt != this.Model.PromptOriginal)
                {
                    this.Model.Statements = [];
                    this.Model.PromptOriginal = this.Model.Prompt;
                }
                */

                await this.SaveModel(projectFullPath);

                var statements = await ttsToVideoBusiness.ProcessCommandExecute(
                      projectFullPath
                    , ProjectSelected.ProjectName
                    , Model.Prompt
                    , Model.NegativePrompt + "," + configuration.Model.NegativePrompt
                    , Model.AditionalPrompt ?? ""
                    , Model.MusicModelSelected.FilePath
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
                        MusicaOptions = new TtsToVideoMusicOptions
                        {
                            MusicDir = configuration.Model.MusicDir,
                            MusicVolume = Model.MusicVolume,
                        },
                        ImageOptions = new TtsToVideoImageOptions
                        {
                            UseOnlyFirstImage = Model.UseOnlyFirstImage,
                            UseTextForPrompt = Model.UseTextForPrompt,
                            CreateVideo = Model.CreateVideo
                        },
                        SubtitleOptions = new TtsTVideoSubtitleOptions
                        {
                            SubtitleSize = Model.SubtitleSize
                        },
                        StatementOptions = [.. Model.Statements.Select((o, i) => {
                            return new StatementOptions
                        {
                            Index = i,
                            FontStyle = o.FontStyle,
                            Id = HashMD5.GenerateHash(o.Text ?? $"{i}"),
                            PromptDebug = o.Text,
                        }; } )]
                    }, token);

                this.Model.Statements = [.. statements.Select(o => new StatementModel
                        {
                            Text = o.Prompt,
                            Images = [.. o.Images.Select(i => new StatementImageModel { Path = i.Path })],
                            AudioDuration = o.AudioDuration,
                            FontStyle = o.FontStyle,
                            AudioPath = o.AudioPath,
                            ImageAnimatedPath = o.ImageAnimatedPath,
                        })];

                await this.SaveModel(projectFullPath);

                ShowVideoDetails(projectFullPath);
            }
            finally
            {
                this.CancellationTokenSource?.Dispose();
            }
        }

        private void ShowVideoDetails(string projectFullPath)
        {

            var finalVideoPath = Path.Combine(projectFullPath, $"{ProjectSelected?.FileName}-Final.mp4");
            if (File.Exists(finalVideoPath))
            {
                var fileInfo = new FileInfo(finalVideoPath);
                TimeSpan? videoDuration;
                try
                {
                    // Use MediaInfo or FFmpeg to get duration if available, fallback to null
                    videoDuration = GetVideoDuration(finalVideoPath);
                }
                catch
                {
                    videoDuration = null;
                }

                if (videoDuration != null)
                {
                    var videoSizeMB = fileInfo.Length / (1024 * 1024);
                    this.MessageRight = $"Video duration: {(videoDuration?.ToString(@"hh\:mm\:ss") ?? "Unknown")}, Size: {videoSizeMB:F2} MB";
                }
            }
        }

        // Helper method to get video duration using FFmpeg (requires ffprobe in PATH)
        private static TimeSpan? GetVideoDuration(string videoPath)
        {
            var ffprobePath = "ffprobe"; // Assumes ffprobe is in PATH
            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            string? output = process?.StandardOutput.ReadToEnd();
            process?.WaitForExit();
            if (double.TryParse(output, out var seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }
            return null;
        }

        private void CommonValidation()
        {
            ArgumentNullException.ThrowIfNull(this.Model);

            if (string.IsNullOrEmpty(this.ProjectSelected.ProjectName))
            {
                throw new CustomApplicationException("Project Name Empty");
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var invalidCharsInPath = ProjectSelected.ProjectName.Where(o => invalidChars.Any(a => a == o));
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

            if (Model.MusicModelSelected == null)
            {
                throw new CustomApplicationException("Music Model not selected");
            }

        }

        private async Task SaveModel(string basePath)
        {
            var json = JsonConvert.SerializeObject(this.Model);
            await File.WriteAllTextAsync(Path.Combine(basePath, Constants.CONFIG_FILE_PROJECT), json);
        }

        private async Task LoadModel(ProjectModel? pm, string selectedPlatform, string selectedLanguage)
        {
            ArgumentNullException.ThrowIfNull(this.ImagesModels);

            var configurationFile = Path.Combine(pm.FullPath, selectedPlatform, selectedLanguage, Constants.CONFIG_FILE_PROJECT);

            Directory.CreateDirectory(Path.GetDirectoryName(configurationFile));

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
                    ?? throw new Exception($"Error reading configuration of the project \"{pm.ProjectName}\", {selectedPlatform}, {selectedLanguage}");

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

            }
        }

        private async Task CancelCommandExecute()
        {
            this.CancellationTokenSource?.Cancel();
            await Task.Delay(0);
        }

        public void CleanProject()
        {
            if (Model != null)
            {
                //Model.Prompt = "";
                Model.AditionalPrompt = "";
                Model.MusicVolume = 100;
                Model.NegativePrompt = "";
                Model.Statements = [];
            }
            if (NewProjectViewModel.Model != null)
            {
                NewProjectViewModel.Model.ProjectName = "";
            }
        }


    }
}