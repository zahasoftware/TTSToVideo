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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TTSToVideo.Business;
using TTSToVideo.Business.Models;
using TTSToVideo.Helpers;
using TTSToVideo.Helpers.Implementations.Ffmpeg;
using TTSToVideo.WPF.Models;

namespace TTSToVideo.WPF.ViewsModels
{
    public partial class TTSToVideoViewModel(
        IMessage message,
        IImageGeneratorAI imageGeneratorAI,
        ITts tts,
        ConfigurationViewModel configuration,
        IMapper mapper,
        ITTSToVideoBusiness ttsToVideoBusiness,
        FontStyleViewModel fontStyleViewModel,
        NewProjectViewModel newProjectViewModel,
        NewCategoryViewModel newCategoryViewModel,
        IAIChatService aIChatService,
        ITranslator translator) : ObservableRecipient
    {
        private static readonly Regex TagsRegex = new("<.*?>", RegexOptions.Compiled);

        // Collections
        public ObservableCollection<ProjectModel>? ProjectsNames { get; set; } = [];
        public ObservableCollection<ImageModel>? ImagesModels { get; set; } = [];
        public ObservableCollection<MusicModel>? MusicModels { get; set; } = [];
        public ObservableCollection<VoiceModel>? VoicesModels { get; set; } = [];
        public ObservableCollection<ChatAIModel>? ChatAIModels { get; set; } = [];

        // Commands (alphabetically ordered)
        public AsyncRelayCommand? CancelCommand { get; set; }
        public AsyncRelayCommand<string>? CategorySelectionChangedCommand { get; set; }
        public AsyncRelayCommand<string>? ChatSendCommand { get; set; }
        public AsyncRelayCommand? CopyDescriptionToClipboardCommand { get; set; }
        public AsyncRelayCommand? CopyFullPathVideoCommand { get; set; }
        public AsyncRelayCommand<object?>? DeletePictureCommand { get; set; }
        public RelayCommand<StatementModel?>? DeleteVideoCommand { get; set; }
        public AsyncRelayCommand<object?>? DeleteVoiceCommand { get; set; }
        public AsyncRelayCommand? GeneratePortraitImageCommand { get; set; }
        public AsyncRelayCommand? GeneratePortraitVideoCommand { get; set; }
        public AsyncRelayCommand<StatementModel?>? LoadVideoCommand { get; set; }
        public AsyncRelayCommand? OpenExplorerCommand { get; set; }
        public AsyncRelayCommand? OpenFinalVideoCommand { get; set; }
        public AsyncRelayCommand<StatementModel?>? OpenPictureCommand { get; set; }
        public AsyncRelayCommand<StatementModel?>? OpenVideoCommand { get; set; }
        public AsyncRelayCommand<object?>? OpenVoiceCommand { get; private set; }
        public AsyncRelayCommand? ProcessCommand { get; set; }
        public AsyncRelayCommand<ProjectModel?>? ProjectSelectionChangedCommand { get; set; }
        public AsyncRelayCommand<StatementModel?>? RegeneratePictureCommand { get; set; }
        public AsyncRelayCommand<StatementModel?>? RegenerateVideoCommand { get; set; }
        public AsyncRelayCommand? SaveCommand { get; set; }
        public AsyncRelayCommand<string>? TranslateToChangedCommand { get; set; }
        public AsyncRelayCommand? UploadImageCommand { get; set; }

        // Models & State
        public TtsToVideoModel? Model { get; set; }
        public ProjectModel? ProjectSelected { get; set; }
        public string? MessageRight { get; set; }
        public string? FinalProjectVideoPathWithVoice { get; private set; }
        public CancellationTokenSource? CancellationTokenSource { get; private set; }
        public bool IsInitialized { get; internal set; }
        public List<string>? Languages { get; set; }
        public string? SelectedLanguage { get; set; } = Constants.LANG_DEFAULT;
        public List<SocialPlatforms>? SocialPlatforms { get; set; }
        public SocialPlatforms? SelectedPlatform { get; set; } = WPF.SocialPlatforms.Tiktok;

        // ViewModels
        public FontStyleViewModel FontStyleViewModel { get; } = fontStyleViewModel;
        public NewProjectViewModel NewProjectViewModel { get; set; } = newProjectViewModel;
        public NewCategoryViewModel CategoryViewModel { get; set; } = newCategoryViewModel;

        #region Initialization

        public async Task Init()
        {
            await configuration.Init();
            InitializeCommands();
            InitializeEventHandlers();
            InitializeModel();
            await LoadResourcesAsync();
            InitializeLanguages();
            IsInitialized = true;
        }

        private void InitializeCommands()
        {
            CancelCommand = new AsyncRelayCommand(CancelCommandExecute);
            CategorySelectionChangedCommand = new AsyncRelayCommand<string>(CategorySelectionChangedCommandExecute);
            ChatSendCommand = new AsyncRelayCommand<string>(ChatSendCommandExecute);
            CopyDescriptionToClipboardCommand = new AsyncRelayCommand(CopyDescriptionToClipboardCommandExecute);
            CopyFullPathVideoCommand = new AsyncRelayCommand(CopyFullPathVideoCommandExecute);
            DeletePictureCommand = new AsyncRelayCommand<object?>(DeletePictureCommandExecute);
            DeleteVideoCommand = new RelayCommand<StatementModel?>(DeleteVideoCommandExecute);
            DeleteVoiceCommand = new AsyncRelayCommand<object?>(DeleteVoiceCommandExecute);
            LoadVideoCommand = new AsyncRelayCommand<StatementModel?>(LoadVideoCommandExecute);
            OpenExplorerCommand = new AsyncRelayCommand(OpenExplorerCommandExecute);
            OpenFinalVideoCommand = new AsyncRelayCommand(OpenFinalVideoCommandExecute);
            OpenPictureCommand = new AsyncRelayCommand<StatementModel?>(OpenPictureCommandExecute);
            OpenVideoCommand = new AsyncRelayCommand<StatementModel?>(OpenVideoCommandExecute);
            OpenVoiceCommand = new AsyncRelayCommand<object?>(OpenVoiceCommandExecute);
            ProcessCommand = new AsyncRelayCommand(ProcessCommandExecute);
            ProjectSelectionChangedCommand = new AsyncRelayCommand<ProjectModel?>(ProjectSelectionChangedCommandExecute);
            RegeneratePictureCommand = new AsyncRelayCommand<StatementModel?>(RegeneratePictureCommandExecute);
            RegenerateVideoCommand = new AsyncRelayCommand<StatementModel?>(RegenerateVideoCommandExecute);
            SaveCommand = new AsyncRelayCommand(SaveCommandExecute);
            TranslateToChangedCommand = new AsyncRelayCommand<string>(TranslateToChangedCommandExecute);
        }

        private void InitializeEventHandlers()
        {
            NewProjectViewModel.CloseNewProject += (_, p) =>
            {
                ArgumentNullException.ThrowIfNull(ProjectsNames);
                ProjectsNames.Add(p);
                ProjectSelected = p;
            };

            CategoryViewModel.LoadCategoryEvent += categoryModel =>
            {
                if (categoryModel == null || CategoryViewModel.Model == null || ChatAIModels == null) return;
                CategoryViewModel.Model.ChatAIModelSelected = ChatAIModels.FirstOrDefault(o => o.Id == categoryModel.ChatAIModelSelected?.Id);
            };

            NewProjectViewModel.Init();
        }

        private void InitializeModel()
        {
            Model = new TtsToVideoModel { Prompt = "" };
            configuration.Model.ProjectsNames = [];
        }

        private async Task LoadResourcesAsync()
        {
            LoadMusicFiles();
            await LoadImageModelsAsync();
            await LoadVoiceModelsAsync();
            await LoadChatModelsAsync();
            EnsureProjectDirectoryExists();
        }

        private void LoadMusicFiles()
        {
            if (!Directory.Exists(configuration.Model.MusicDir)) return;
            var musicFiles = Directory.GetFiles(configuration.Model.MusicDir, "*.wav");
            foreach (var file in musicFiles)
                MusicModels?.Add(new MusicModel { FilePath = file });
        }

        private async Task LoadImageModelsAsync()
        {
            try
            {
                var models = await imageGeneratorAI.GetModels();
                ImagesModels = new ObservableCollection<ImageModel>(models);
            }
            catch (Exception ex)
            {
                message.Error($"Failed to load image models: {ex.Message}");
            }
        }

        private async Task LoadVoiceModelsAsync()
        {
            try
            {
                var voices = await tts.GetTtsVoices();
                VoicesModels = new ObservableCollection<VoiceModel>(
                    voices.Select(o => new VoiceModel
                    {
                        Gender = o.Gender,
                        Id = o.Id,
                        Language = o.Language,
                        ModelId = o.ModelId,
                        Name = o.Name,
                        Tags = o.Tags
                    }));
            }
            catch (Exception ex)
            {
                message.Error($"Failed to load voice models: {ex.Message}");
            }
        }

        private async Task LoadChatModelsAsync()
        {
            try
            {
                var models = await aIChatService.GetAvailableModelsAsync();
                ChatAIModels = new ObservableCollection<ChatAIModel>(
                    models.Select(o => new ChatAIModel
                    {
                        Id = o.Name,
                        Name = o.Name,
                        Description = $"{o.Name} ({o.Size / (1024.0 * 1024.0 * 1024.0):F2} GB)"
                    }));
            }
            catch (Exception ex)
            {
                message.Error($"Failed to load chat models: {ex.Message}");
            }
        }

        private void EnsureProjectDirectoryExists()
        {
            if (!Directory.Exists(configuration.Model.ProjectBaseDir))
                Directory.CreateDirectory(configuration.Model.ProjectBaseDir);
        }

        private void InitializeLanguages()
        {
            Languages = $"{Constants.LANG_DEFAULT},en-US,fr-FR,de-DE,es-ES,it-IT,pt-BR,zh-CN,ja-JP,ko-KR"
                .Split(',').ToList();
        }

        #endregion

        #region Command Implementations

        private async Task SaveCommandExecute()
        {
            ArgumentNullException.ThrowIfNull(Model);
            if (ProjectSelected == null) throw new CustomApplicationException("Select a project");

            var fullPath = GetProjectPath();
            Directory.CreateDirectory(fullPath);
            await SaveModel(fullPath);
            message.Info("Project saved.");
        }

        private async Task ProcessCommandExecute()
        {
            try
            {
                Validation();
                EnsureRequiredFieldsNotNull();

                CancellationTokenSource = new CancellationTokenSource();
                var projectFullPath = GetProjectPath();
                Directory.CreateDirectory(projectFullPath);

                AddProjectIfNotExists(projectFullPath);
                await SaveModel(projectFullPath);

                var statements = await ExecuteBusinessProcess(projectFullPath, CancellationTokenSource.Token);
                UpdateModelStatements(statements);

                await SaveModel(projectFullPath);
                ShowVideoDetails(projectFullPath);
            }
            finally
            {
                CancellationTokenSource?.Dispose();
            }
        }

        private async Task<List<Statement>> ExecuteBusinessProcess(string projectPath, CancellationToken token)
        {
            return await ttsToVideoBusiness.ProcessCommandExecute(
                projectPath,
                ProjectSelected.ProjectName,
                Model.Prompt,
                $"{Model.NegativePrompt},{configuration.Model.NegativePrompt}",
                Model.AditionalPrompt ?? "",
                Model.MusicModelSelected.FilePath,
                [Model.ImageModelSelected.Id],
                new TtsVoice { Id = Model.VoiceModelSelected.Id, ModelId = Model.VoiceModelSelected.ModelId },
                Model.PortraitEnabled,
                CreateTTSToVideoOptions(),
                token);
        }

        private TTSToVideoOptions CreateTTSToVideoOptions()
        {
            return new TTSToVideoOptions
            {
                DurationBetweenVideo = TimeSpan.FromSeconds(2),
                DurationEndVideo = TimeSpan.FromSeconds(7),
                MusicaOptions = new TtsToVideoMusicOptions
                {
                    MusicDir = configuration.Model.MusicDir,
                    MusicVolume = Model.MusicVolume
                },
                ImageOptions = new TtsToVideoImageOptions
                {
                    UseOnlyFirstImage = Model.UseOnlyFirstImage,
                    UseTextForPrompt = Model.UseTextForPrompt,
                    CreateVideo = Model.CreateVideo
                },
                SubtitleOptions = new TtsTVideoSubtitleOptions
                {
                    SubtitleSize = Model.SubtitleSize,
                    MarginV = Model.SubtitleMarginV
                },
                StatementOptions = Model.Statements.Select((o, i) => new StatementOptions
                {
                    Index = i,
                    FontStyle = o.FontStyle,
                    Id = HashMD5.GenerateHash(o.Text ?? $"{i}"),
                    PromptDebug = o.Text
                }).ToList()
            };
        }

        private void UpdateModelStatements(List<Statement> statements)
        {
            Model.Statements = new ObservableCollection<StatementModel>(
                statements.Select(o => new StatementModel
                {
                    Text = o.Prompt,
                    Images = new ObservableCollection<StatementImageModel>(
                        o.Images.Select(i => new StatementImageModel { Path = i.Path, Id = i.Id })
                    ),
                    AudioDuration = o.AudioDuration,
                    FontStyle = o.FontStyle,
                    AudioPath = o.AudioPath,
                    ImageAnimatedPath = o.ImageAnimatedPath
                })
            );
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
                CancellationTokenSource = new CancellationTokenSource();
                var outputFolder = GetProjectPath();

                message.Info("Generating image.");

                var statement = arg.ToStatement();
                statement.GlobalPrompt = Model?.AditionalPrompt ?? "";
                statement.Prompt = Model?.Prompt ?? "";
                statement.Images.Clear();

                await ttsToVideoBusiness.GeneratePortraitImageCommandExecute(
                    statement,
                    [Model.ImageModelSelected.Id],
                    outputFolder,
                    new TTSToVideoOptions { ImageOptions = new TtsToVideoImageOptions { UseTextForPrompt = true } },
                    CancellationTokenSource.Token);

                arg.Images = new  ObservableCollection<StatementImageModel>( mapper.Map<List<StatementImageModel>>(statement.Images));
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
                CancellationTokenSource?.Dispose();
            }
        }

        private async Task RegenerateVideoCommandExecute(StatementModel? model)
        {
            ArgumentNullException.ThrowIfNull(model);

            try
            {
                CancellationTokenSource = new CancellationTokenSource();
                var statement = model.ToStatement();
                var projectPath = GetProjectPath();
                var imagePath = PathHelper.GenerateImagePath(projectPath, statement.Prompt);

                if (imagePath != null)
                {
                    model.ImageAnimatedPath = $"{imagePath}.mp4";
                    var request = new VideoGenerationRequest
                    {
                        Prompt = Model.UseOnlyFirstImage
                            ? Model.AditionalPrompt
                            : $"{Model.AditionalPrompt} {statement.Prompt}",
                        SourceImageId = model.Images.FirstOrDefault()?.Id,
                        SourceImagePath = imagePath,
                        Version = MotionVersion.Motion2,
                        MotionStrength = 5
                    };

                    await ttsToVideoBusiness.GeneratePortraitVideoCommandExecute(
                        request,
                        model.ImageAnimatedPath,
                        CancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                message.Error($"An error occurred while regenerating the video: {ex.Message}");
            }
            finally
            {
                CancellationTokenSource?.Dispose();
            }
        }

        private async Task LoadVideoCommandExecute(StatementModel? model)
        {
            if (model == null) return;

            if (string.IsNullOrWhiteSpace(model.PlaybackVideoPath))
            {
                var candidate = model.ImageAnimatedPath;
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate)) { 
                    //model.PlaybackVideoPath = candidate;
                }else

                    return;
            }

            model.IsVideoLoaded = true;
            await Task.CompletedTask;
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

        private async Task DeletePictureCommandExecute(object? statement)
        {
            if (!await message.Confirm("Are you sure you want to delete this picture?")) return;

            if (statement is not StatementModel statementModel) return;

            DeleteRelatedFiles(statementModel);
            ClearStatementImages(statementModel);
            DeleteProjectFiles();
        }

        private void DeleteRelatedFiles(StatementModel statementModel)
        {
            var filesToDelete = new[] { $"{statementModel.AudioPath}.mp4" };
            foreach (var file in filesToDelete.Where(File.Exists))
                File.Delete(file);
        }

        private void ClearStatementImages(StatementModel statementModel)
        {
            var images = Model.Statements.FirstOrDefault(o => o == statementModel)?.Images;
            if (images == null) return;

            foreach (var image in images)
            {
                var path = image.Path;
                image.Path = "";

                var pathsToDelete = new[] { path, $"{path}.mp4", $"{path}.wav.mp4" };
                foreach (var p in pathsToDelete.Where(f => f != null && File.Exists(f)))
                    File.Delete(p);
            }
        }

        private void DeleteProjectFiles()
        {
            var fullPathProject = GetProjectPath();
            var filesToDelete = new[]
            {
                Path.Combine(fullPathProject, $"{ProjectSelected.ProjectName}-Music-Final.mp4"),
                Path.Combine(fullPathProject, $"{ProjectSelected.ProjectName}.mp4"),
                Path.Combine(fullPathProject, $"{ProjectSelected.ProjectName}-Final.mp4")
            };

            foreach (var file in filesToDelete.Where(File.Exists))
                File.Delete(file);
        }

        private async Task DeleteVoiceCommandExecute(object? statement)
        {
            if (statement is not StatementModel statementModel ||
                !await message.Confirm("Are you sure you want to delete this voice?"))
                return;

            var basePath = GetProjectPath();
            var pathsToDelete = new[]
            {
                statementModel.AudioPath,
                $"{statementModel.AudioPath}.wav",
                $"{statementModel.AudioPath}.mp4",
                $"{statementModel.AudioPath}.wav.mp4",
                Path.Combine(basePath, $"{ProjectSelected.ProjectName}-Music-Final.mp4"),
                Path.Combine(basePath, $"{ProjectSelected.ProjectName}.mp4"),
                Path.Combine(basePath, $"{ProjectSelected.ProjectName}-Final.mp4")
            }.Where(File.Exists);

            foreach (var file in pathsToDelete)
                File.Delete(file);
        }

        private async Task OpenPictureCommandExecute(StatementModel? statement)
        {
            if (statement?.Images.Count > 0)
            {
                var path = statement.Images[0].Path;
                if (path != null && File.Exists(path))
                    await OpenFileAsync(path);
            }
        }

        private async Task OpenVideoCommandExecute(StatementModel? statementModel = null)
        {
            var path = statementModel?.ImageAnimatedPath ?? $"{statementModel?.Images.FirstOrDefault()?.Path}.mp4";
            await OpenVideoAsync(path);
        }

        private async Task OpenVoiceCommandExecute(object? statement)
        {
            if (statement is StatementModel statementModel)
            {
                var path = $"{statementModel.AudioPath}.wav";
                if (path != null && File.Exists(path))
                    await OpenFileAsync(path);
            }
        }

        private async Task OpenFinalVideoCommandExecute()
        {
            FinalProjectVideoPathWithVoice = Path.Combine(
                GetProjectPath(),
                $"{ProjectSelected.FileName}-Final.mp4");
            await OpenVideoAsync(FinalProjectVideoPathWithVoice);
        }

        private async Task OpenExplorerCommandExecute()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GetProjectPath(),
                UseShellExecute = true,
                Verb = "open"
            });
            await Task.CompletedTask;
        }

        private async Task TranslateToChangedCommandExecute(string? toLanguage)
        {
            if (toLanguage == null || Model == null || ProjectSelected == null) return;

            var translatedProjectPath = Path.Combine(ProjectSelected.FullPath, $"{SelectedPlatform}", toLanguage);

            await LoadModel(ProjectSelected, SelectedPlatform.ToString(), SelectedLanguage);

            if (string.IsNullOrEmpty(Model.Prompt))
            {
                var defaultProjectPath = Path.Combine(ProjectSelected.FullPath, $"{SelectedPlatform}", Constants.LANG_DEFAULT);
                var defaultConfigPath = Path.Combine(defaultProjectPath, Constants.CONFIG_FILE_PROJECT);
                var defaultModel = JsonConvert.DeserializeObject<TtsToVideoModel>(
                    await File.ReadAllTextAsync(defaultConfigPath));

                var translatedText = await translator.TranslateTextAsync(defaultModel.Prompt, toLanguage);
                Model.Prompt = translatedText;
                message.Info($"Translated to {toLanguage}.");
                await SaveModel(translatedProjectPath);
            }

            ShowVideoDetails(GetProjectPath());
        }

        private async Task ChatSendCommandExecute(string? prompt)
        {
            message.Info("Processing message...");
            ArgumentNullException.ThrowIfNull(CategoryViewModel.Model);
            ArgumentNullException.ThrowIfNull(ProjectsNames);

            if (string.IsNullOrEmpty(prompt))
                throw new CustomApplicationException("Prompt is empty or null.");

            if (CategoryViewModel.Model.ChatAIModelSelected == null ||
                string.IsNullOrEmpty(CategoryViewModel.Model.ChatAIModelSelected.Name))
                throw new CustomApplicationException("No AI Chat model selected.");

            CancellationTokenSource = new CancellationTokenSource();
            var promptFull = BuildChatPrompt(prompt);

            await aIChatService.SetModelAsync(CategoryViewModel.Model.ChatAIModelSelected.Name, CancellationTokenSource.Token);
            var response = await aIChatService.GenerateResponseAsync(promptFull, CancellationTokenSource.Token);

            Model.Prompt = response;
            CategoryViewModel.SaveCategory();
        }

        private string BuildChatPrompt(string prompt)
        {
            var fullPrompt = $"{prompt}\nHere the instruction:\n{CategoryViewModel.Model.IAChatInstructions}";
            fullPrompt += $"\n\navoid using this following key words in the generation:\n{string.Join("\n", ProjectsNames.Select(o => o.ProjectName).Distinct())}";
            return fullPrompt;
        }

        private async Task CopyDescriptionToClipboardCommandExecute()
        {
            if (Model == null || string.IsNullOrEmpty(Model.Prompt))
            {
                message.Warn("No prompt available to copy.");
                return;
            }

            var description = Model.Prompt;
            if (!string.IsNullOrEmpty(CategoryViewModel.Model?.HashTags))
                description += $"{Environment.NewLine}{CategoryViewModel.Model.HashTags}";

            description = TagsRegex.Replace(description, string.Empty);
            Clipboard.SetText(description);
            message.Info("Description and hashtags copied to clipboard.");
            await Task.CompletedTask;
        }

        private async Task CopyFullPathVideoCommandExecute()
        {
            if (string.IsNullOrEmpty(FinalProjectVideoPathWithVoice) || !File.Exists(FinalProjectVideoPathWithVoice))
            {
                message.Warn("The final video path is either empty or the file does not exist.");
                return;
            }

            Clipboard.SetText(FinalProjectVideoPathWithVoice);
            message.Info("Final video path copied to clipboard.");
            await Task.CompletedTask;
        }

        private async Task CategorySelectionChangedCommandExecute(string? path)
        {
            if (path == null) return;

            var directories = Directory.GetDirectories(path);
            ProjectsNames = new ObservableCollection<ProjectModel>(
                directories.Select(o => new ProjectModel
                {
                    FileName = Path.GetFileName(o),
                    FullPath = o,
                    ProjectName = Path.GetFileName(o),
                    CreatedAt = Directory.GetCreationTime(o),
                    Category = new CategoryModel
                    {
                        CategoryName = Path.GetFileName(path),
                        DirectoryPath = path
                    }
                }).OrderByDescending(o => o.CreatedAt));

            CategoryViewModel.LoadCategory(path);
        }

        private async Task ProjectSelectionChangedCommandExecute(ProjectModel? pm)
        {
            if (pm == null || pm != ProjectSelected) return;

            await LoadModel(pm, SelectedPlatform.ToString(), SelectedLanguage);

            var projectDir = GetProjectPath();
            FinalProjectVideoPathWithVoice = Path.Combine(projectDir, $"{pm.FileName}-Final.mp4");
            ShowVideoDetails(projectDir);
        }

        private async Task CancelCommandExecute()
        {
            CancellationTokenSource?.Cancel();
            await Task.CompletedTask;
        }

        #endregion

        #region Helper Methods

        private string GetProjectPath()
        {
            return Path.Combine(ProjectSelected.FullPath, SelectedPlatform.ToString(), SelectedLanguage);
        }

        private void AddProjectIfNotExists(string fullPath)
        {
            if (!ProjectsNames.Any(p => p.ProjectName == ProjectSelected.ProjectName))
            {
                ProjectsNames.Add(new ProjectModel
                {
                    ProjectName = ProjectSelected.ProjectName,
                    FileName = ProjectSelected.ProjectName,
                    FullPath = fullPath
                });
            }
        }

        private void EnsureRequiredFieldsNotNull()
        {
            ArgumentNullException.ThrowIfNull(Model);
            ArgumentNullException.ThrowIfNull(ProjectSelected.ProjectName);
            ArgumentNullException.ThrowIfNull(ProjectsNames);
            ArgumentNullException.ThrowIfNull(Model.Prompt);
            ArgumentNullException.ThrowIfNull(configuration.Model.MusicDir);
            ArgumentNullException.ThrowIfNull(Model.ImageModelSelected);
            ArgumentNullException.ThrowIfNull(Model.ImageModelSelected.Id);
            ArgumentNullException.ThrowIfNull(Model.VoiceModelSelected);
            ArgumentNullException.ThrowIfNull(Model.MusicModelSelected);
        }

        private static async Task OpenFileAsync(string path)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"\"{path}\"",
                UseShellExecute = true,
                Verb = "open"
            });
            await Task.CompletedTask;
        }

        private static async Task OpenVideoAsync(string path)
        {
            if (!File.Exists(path))
                throw new CustomApplicationException("Video not created.");

            await OpenFileAsync(path);
        }

        private void ShowVideoDetails(string projectFullPath)
        {
            var finalVideoPath = Path.Combine(projectFullPath, $"{ProjectSelected?.FileName}-Final.mp4");
            if (!File.Exists(finalVideoPath)) return;

            var fileInfo = new FileInfo(finalVideoPath);
            var videoDuration = GetVideoDuration(finalVideoPath);

            if (videoDuration != null)
            {
                var videoSizeMB = fileInfo.Length / (1024.0 * 1024.0);
                MessageRight = $"Video duration: {videoDuration?.ToString(@"hh\:mm\:ss") ?? "Unknown"}, Size: {videoSizeMB:F2} MB";
            }
        }

        private static TimeSpan? GetVideoDuration(string videoPath)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                var output = process?.StandardOutput.ReadToEnd();
                process?.WaitForExit();

                return double.TryParse(output, out var seconds) ? TimeSpan.FromSeconds(seconds) : null;
            }
            catch
            {
                return null;
            }
        }

        private void Validation()
        {
            ArgumentNullException.ThrowIfNull(Model);

            if (string.IsNullOrEmpty(Model.Prompt))
                throw new CustomApplicationException("Text Empty");
            if (string.IsNullOrEmpty(configuration.Model.MusicDir))
                throw new CustomApplicationException("Music Directory Empty or does not exist");
            if (!Directory.Exists(configuration.Model.MusicDir))
                throw new CustomApplicationException("Music Directory does not exist");
            if (Model.ImageModelSelected == null)
                throw new CustomApplicationException("Image Model not selected");
            if (Model.VoiceModelSelected == null)
                throw new CustomApplicationException("Voice Model not selected");
            if (Model.MusicModelSelected == null)
                throw new CustomApplicationException("Music Model not selected");
        }

        private async Task SaveModel(string basePath)
        {
            var json = JsonConvert.SerializeObject(Model);
            await File.WriteAllTextAsync(Path.Combine(basePath, Constants.CONFIG_FILE_PROJECT), json);
        }

        private async Task LoadModel(ProjectModel? pm, string selectedPlatform, string selectedLanguage)
        {
            ArgumentNullException.ThrowIfNull(ImagesModels);

            var configurationFile = Path.Combine(pm.FullPath, selectedPlatform, selectedLanguage, Constants.CONFIG_FILE_PROJECT);
            Directory.CreateDirectory(Path.GetDirectoryName(configurationFile));

            if (!File.Exists(configurationFile))
            {
                Model = new TtsToVideoModel();
                return;
            }

            var json = await File.ReadAllTextAsync(configurationFile);
            var model = JsonConvert.DeserializeObject<TtsToVideoModel>(json)
                ?? throw new Exception($"Error reading configuration of the project \"{pm.ProjectName}\", {selectedPlatform}, {selectedLanguage}");

            Model = new TtsToVideoModel();
            mapper.Map(model, Model);

            Model.ImageModelSelected = ImagesModels.FirstOrDefault(o => o.Id == model.ImageModelSelected?.Id);
            if (VoicesModels != null && model.VoiceModelSelected != null)
                Model.VoiceModelSelected = VoicesModels.FirstOrDefault(o => o.Id == model.VoiceModelSelected?.Id);
            if (MusicModels != null && model.MusicModelSelected != null)
                Model.MusicModelSelected = MusicModels.FirstOrDefault(o => o.FilePath == model.MusicModelSelected?.FilePath);
        }

        public void CleanProject()
        {
            if (Model != null)
            {
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

        #endregion
    }
}