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
using TTSToVideo.WPF.Helpers;
using TTSToVideo.WPF.Models;

namespace TTSToVideo.WPF.ViewModel.Implementations
{

    public partial class VMTTSToVideoPage : ObservableRecipient, IVMTTSToVideoPage
    {
        private readonly IImageGeneratorAI imageGeneratorAI;
        private readonly ITTS tts;
        public IVMConfiguration VMConf { get; }
        public IIOTerminal Terminal { get; }
        public IMapper Mapper { get; }
        public AsyncRelayCommand ProcessCommand { get; set; }
        public AsyncRelayCommand SaveCommand { get; set; }
        public AsyncRelayCommand CancelCommand { get; set; }
        public AsyncRelayCommand OpenExplorerCommand { get; set; }
        public AsyncRelayCommand OpenVideoCommand { get; set; }
        public AsyncRelayCommand UploadImageCommand { get; set; }
        public AsyncRelayCommand GeneratePortraitImageCommand { get; set; }
        public AsyncRelayCommand<string> ProjectNameSelectionChangedCommand { get; set; }

        [ObservableProperty]
        private TTSToVideoModel? model;
        private bool isGeneratingPortrait;

        public string? FinalProjectVideoPathWithVoice { get; private set; }
        public CancellationTokenSource? CancellationTokenSource { get; private set; }

        public VMTTSToVideoPage(IImageGeneratorAI imageGeneratorAI,
                          ITTS tts,
                          IVMConfiguration configuration,
                          IIOTerminal terminal,
                          IMapper mapper
        )
        {
            ProcessCommand = new AsyncRelayCommand(ProcessCommandExecute);
            SaveCommand = new AsyncRelayCommand(SaveCommandExecute);
            CancelCommand = new AsyncRelayCommand(CancelCommandExecute);
            OpenExplorerCommand = new AsyncRelayCommand(OpenExplorer);
            OpenVideoCommand = new AsyncRelayCommand(OpenVideo);
            UploadImageCommand = new AsyncRelayCommand(UploadImageCommandExecute);
            GeneratePortraitImageCommand = new AsyncRelayCommand(GeneratePortraitImageCommandExecute);
            ProjectNameSelectionChangedCommand = new AsyncRelayCommand<string>(ProjectNameSelectionChangedCommandExecute);
            this.imageGeneratorAI = imageGeneratorAI;
            this.tts = tts;
            this.VMConf = configuration;
            this.Terminal = terminal;
            Mapper = mapper;
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
            await SaveModel(path);
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
                int countImageMain = 1;

                var statement = new Statement
                {
                    Text = string.IsNullOrEmpty(this.Model.PortraitPrompt?.Trim()) ? this.Model.PortraitText : this.Model.PortraitPrompt
                };
                await GenerateImage(projectFullPath, countImageMain, statement, token);

                this.Model.PortraitImagePath = Path.GetFileName(statement.Images.First().Path);

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
                this.Model.ProjectsNames = new ObservableCollection<ProjectModel>(directories.Select(o => new ProjectModel
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
            List<Statement> statements = null;

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
                Model.ProjectsNames.Add(new ProjectModel
                {
                    ProjectName = this.Model.ProjectName,
                    FileName = this.Model.ProjectName,
                    FullPath = projectFullPath,
                });

                //Split text process text with dot and paragraph
                string[] paragraphs = Model.Prompt.Split(new string[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                statements = paragraphs.Select(o => new Statement { Text = o }).ToList();

                Statement? statementPortraitVoice = null;
                if (Model.PortraitEnabled)
                {
                    if (this.Model.PortraitImagePath == null)
                    {
                        await this.GeneratePortraitImageCommandExecute();
                    }

                    statementPortraitVoice = new Statement()
                    {
                        Images = new List<StatementImage> {
                            new() { Path = Path.Combine(projectFullPath, this.Model.PortraitImagePath
                                                                        ?? throw new CustomApplicationException("PortraitImagePath empty") ) }
                        },
                        Text = (string.IsNullOrEmpty(this.Model.PortraitVoice) ? this.Model.PortraitText : this.Model.PortraitVoice) ??
                                  throw new CustomApplicationException("PortraitText or PortraitVoice empty"),

                        AudioPath = Path.Combine(projectFullPath, "portrait-voice.wav")
                    };
                }
                /*
                foreach (var paragraph in paragraphs)
                {
                    string[] sentences = Regex.Split(paragraph, @"(?<=[:\.!\?])\s+");
                    foreach (string sentence in sentences)
                    {
                        statements.Add(new Statement
                        {
                            Text = sentence,
                            IsFinalParagraph = paragraphs.Any(o => o.EndsWith(sentence))
                        });
                    }
                }
                */

                await this.SaveModel(projectFullPath);

                //Getting voices
                #region Processing Voices
                var ttsVoices = await tts.GetTTSVoices("", token);
                ttsVoices = ttsVoices.Where(o => o.Tags.Contains("meditation")).ToList();
                Random rv = new();
                int rvn = rv.Next(1, ttsVoices.Count + 1);
                var ttsVoice = ttsVoices[rvn - 1];

                bool anyNewFile = false;
                //Getting portrait voice
                if (Model.PortraitEnabled && statementPortraitVoice != null)
                {
                    anyNewFile = await GetVoice(ttsVoice, statementPortraitVoice, token);
                }

                //Getting statement voices
                int ca = 1;
                foreach (var statement in statements)
                {
                    WeakReferenceMessenger.Default.Send(new Message { Text = $"Getting voices {ca}" });

                    var audioFileName = $"{statement.Text[..Math.Min(statement.Text.Length, Constants.MAX_PATH)]}";
                    audioFileName = Path.Combine(projectFullPath, $"{ca++}.-{CleanFileName(audioFileName)}.wav");
                    statement.AudioPath = audioFileName;

                    anyNewFile = await GetVoice(ttsVoice, statement, token);
                }

                //Concating voices 
                WeakReferenceMessenger.Default.Send(new Message { Text = "Concatenating voices" });

                var tempVoiceFileA = $"{Path.GetTempFileName()}.mp4";
                var tempVoiceFileB = $"{Path.GetTempFileName()}.mp4";

                var previous = "";

                //Concatenating Portrait with the first audio
                if (this.Model.PortraitEnabled)
                {
                    previous = statementPortraitVoice?.AudioPath;
                    AudioHelper.ConcatenateAudioFiles(tempVoiceFileA, new string[] { previous, statements.First().AudioPath });
                    File.Copy(tempVoiceFileA, tempVoiceFileB, true);
                    previous = tempVoiceFileB;
                }
                else
                {
                    previous = statements.First().AudioPath;
                }

                foreach (var s in statements.Skip(1))
                {
                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion cancelled by User");
                    }

                    AudioHelper.ConcatenateAudioFiles(tempVoiceFileA, new string[] { previous, s.AudioPath });
                    File.Copy(tempVoiceFileA, tempVoiceFileB, true);
                    previous = tempVoiceFileB;
                }

                var bigAudioPath = Path.Combine(projectFullPath, $"Voices Concatenated.mp4");
                File.Copy(previous, bigAudioPath, true);

                RemoveTempFile(tempVoiceFileA);
                RemoveTempFile(tempVoiceFileB);
                #endregion

                //Taking Picture
                #region Processing Pictures
                int numImages = 1;
                int countImageMain = 0;
                foreach (var statement in statements)
                {
                    countImageMain++;

                    WeakReferenceMessenger.Default.Send(new Message { Text = $"Getting Pictures {countImageMain}" });

                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion Cancelled by User");
                    }

                    bool notExistsOneImage = false;
                    for (int i = 0; i < numImages; i++)
                    {
                        var imageFileName = $"{statement.Text[..Math.Min(statement.Text.Length, Constants.MAX_PATH)]}";
                        imageFileName = Path.Combine(projectFullPath, $"{countImageMain}.{i + 1}.-{CleanFileName(imageFileName)}.jpg");

                        statement.Images.Add(new StatementImage
                        {
                            Path = imageFileName
                        });

                        if (!File.Exists(imageFileName))
                        {
                            notExistsOneImage = true;
                        }
                    }


                    if (notExistsOneImage)
                    {
                        await GenerateImage(projectFullPath, countImageMain, statement, token);
                    }
                }
                #endregion

                //Making Audio
                #region Processing Audio
                WeakReferenceMessenger.Default.Send(new Message { Text = $"Making Audio." });

                Random random = new();
                var audioWavs = Directory.GetFiles(this.VMConf.Model.MusicDir, "*.wav");
                int randomNumber = random.Next(1, audioWavs.Length);
                var audioFilePath = audioWavs[randomNumber - 1];

                TimeSpan desiredDuration = new();  // Adjust this value for desired audio duration
                foreach (var s in statements)
                {
                    desiredDuration += s.AudioDuration;
                }

                if (Model.PortraitEnabled)
                {
                    desiredDuration += statementPortraitVoice.AudioDuration;
                }
                desiredDuration += new TimeSpan(0, 0, 10);//Final duracion
                using var audioFileReal = AudioHelper.OpenAudio(audioFilePath);

                double cut = audioFileReal.TotalTime.TotalSeconds - 10;

                var tempAudioFileA = $"{Path.GetTempFileName()}.mp4";
                //AudioHelper.CutAudio(audioFilePath, tempAudioFileA, cut);
                File.Copy(audioFilePath, tempAudioFileA, true);

                using var audioFile = AudioHelper.OpenAudio(tempAudioFileA);

                var tempAudioFileB = $"{Path.GetTempFileName()}.mp4";
                //AudioHelper.CutAudio(audioFilePath, tempAudioFileB, cut);
                File.Copy(audioFilePath, tempAudioFileB, true);

                var outputMusicFile = "output.wav";

                var tempAudioFileC = $"{Path.GetTempFileName()}.mp4";
                for (double s = 0; s < desiredDuration.TotalSeconds; s += audioFile.TotalTime.TotalSeconds)
                {
                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion Cancelled by User");
                    }

                    AudioHelper.ConcatenateAudioFiles(tempAudioFileC, new string[] { tempAudioFileA, tempAudioFileB });
                    File.Copy(tempAudioFileC, tempAudioFileA, true);
                }

                using var a = AudioHelper.OpenAudio(tempAudioFileC);
                cut = Math.Min(a.TotalTime.TotalSeconds, desiredDuration.TotalSeconds);

                AudioHelper.CutAudio(tempAudioFileC, tempAudioFileA, cut);
                AudioHelper.DecreaseVolumeAtSpecificTime(tempAudioFileA, tempAudioFileB, TimeSpan.FromSeconds(cut - 10), 0.5f);
                File.Copy(tempAudioFileB, outputMusicFile, true);

                RemoveTempFile(tempAudioFileA);
                RemoveTempFile(tempAudioFileB);
                RemoveTempFile(tempAudioFileC);
                #endregion

                //Making the Video
                #region Making the Video
                WeakReferenceMessenger.Default.Send(new Message { Text = $"Making Video." });

                var finalProjectVideoPath = GetProjectPath(Model.ProjectName) + "\\" + $"{Model.ProjectName}.mp4";

                var previousVideo = "";
                string outputPath = "";
                string tempFile = "";

                FfmpegOptions ffmpegOptions = new()
                {
                    FontStyle = new FfmpegFontStyle
                    {
                        Alignment = FfmpegAlignment.TopCenter,
                        FontSize = 11
                    }
                };

                List<Statement> statementsToIterate = statements;
                if (Model.PortraitEnabled && statementPortraitVoice != null)
                {
                    outputPath = statementPortraitVoice.AudioPath + ".mp4";
                    await FFMPEGHelpers.CreateVideoWithSubtitle(
                        outputPath,
                        statementPortraitVoice.Text,
                        statementPortraitVoice.Images.First().Path,
                        statementPortraitVoice.AudioDuration,
                        ffmpegOptions,
                        token);

                    tempFile = $"{Path.GetTempFileName()}.mp4";
                    File.Copy(outputPath, tempFile, true);
                    previousVideo = tempFile;
                }
                else
                {
                    var firstStatement = statements.First();
                    outputPath = firstStatement.AudioPath + ".mp4";
                    await FFMPEGHelpers.CreateVideoWithSubtitle(
                        outputPath,
                        firstStatement.Text,
                        firstStatement.Images.First().Path,
                        firstStatement.AudioDuration,
                        ffmpegOptions,
                        token);

                    tempFile = $"{Path.GetTempFileName()}.mp4";
                    File.Copy(outputPath, tempFile, true);
                    previousVideo = tempFile;

                    statementsToIterate = statements.Skip(1).ToList();
                }

                foreach (var s in statementsToIterate)
                {
                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion Cancelled by User");
                    }

                    // Set the output file path
                    outputPath = s.AudioPath + ".mp4";
                    ffmpegOptions = new()
                    {
                        HeightResolution = FFMPEGDefinitions.HeightResolution,
                        WidthResolution = FFMPEGDefinitions.WidthResolution,
                        FontStyle = new()
                        {
                            Alignment = FfmpegAlignment.TopCenter,
                            FontSize = 11,
                        }
                    };
                    await FFMPEGHelpers.CreateVideoWithSubtitle(outputPath, s.Text, s.Images.First().Path, s.AudioDuration, ffmpegOptions, token);

                    await FFMPEGHelpers.JoiningVideos(previousVideo, outputPath, finalProjectVideoPath, ffmpegOptions, token);

                    if (File.Exists(previousVideo))
                    {
                        RemoveTempFile(previousVideo);
                    }

                    tempFile = $"{Path.GetTempFileName()}.mp4";
                    File.Copy(finalProjectVideoPath, tempFile, true);
                    previousVideo = tempFile;
                }

                tempFile = $"{Path.GetTempFileName()}.mp4";
                File.Copy(finalProjectVideoPath, tempFile, true);
                previousVideo = tempFile;

                var lastStatement = statements.Last();
                var lastImage = lastStatement.Images[0].Path;
                outputPath = $"{lastStatement.AudioPath}-no-text.mp4";

                //Run ffmpeg process
                await FFMPEGHelpers.GenerateVideoWithImage(outputPath, lastImage, token);

                //Adding last image
                await FFMPEGHelpers.JoiningVideos(previousVideo, outputPath, finalProjectVideoPath, new FfmpegOptions
                {
                    HeightResolution = FFMPEGDefinitions.HeightResolution,
                    WidthResolution = FFMPEGDefinitions.WidthResolution,
                }, token);

                var finalProjectVideoPathWithAudio = this.GetProjectPath(Model.ProjectName) + "\\" + $"{Model.ProjectName}-Music-Final.mp4";
                await FFMPEGHelpers.MixAudioWithVideo(finalProjectVideoPath
                                           , outputMusicFile
                                           , finalProjectVideoPathWithAudio
                                           , token);

                if (string.IsNullOrEmpty(FinalProjectVideoPathWithVoice))
                {
                    this.FinalProjectVideoPathWithVoice = this.GetProjectPath(Model.ProjectName) + "\\" + $"{Model.ProjectName}-Final.mp4";
                }

                await FFMPEGHelpers.MixAudioWithVideo(finalProjectVideoPathWithAudio
                                           , bigAudioPath
                                           , FinalProjectVideoPathWithVoice
                                           , token);

                RemoveTempFile(previousVideo);

                #endregion
            }
            finally
            {
                this.CancellationTokenSource.Dispose();

                if (statements != null)
                {
                    //Cleaning
                    foreach (var s in statements)
                    {
                        if (s.ImageId != null)
                        {
                            await this.imageGeneratorAI.Remove(new ResultGenerate
                            {
                                Id = s.ImageId
                            });
                        }
                    }
                }
                WeakReferenceMessenger.Default.Send(new Message { Text = "Process Finished." });
            }
        }

        private async Task<bool> GetVoice(TTSVoice ttsVoice, Statement statement, CancellationToken token)
        {
            bool isNewFile = false;
            if (File.Exists(statement.AudioPath))
            {
                WaveStream file = AudioHelper.OpenAudio(statement.AudioPath);
                statement.AudioDuration = file.TotalTime;
                isNewFile = true;
            }
            else
            {
                var audio = await tts.Convert(new TTSConvertOption
                {
                    Text = statement.Text,
                    Voice = ttsVoice
                }, token);

                var buffer = audio.File.GetBuffer();

                File.WriteAllBytes(statement.AudioPath, buffer);

                using var audioFile1 = AudioHelper.OpenAudio(statement.AudioPath);
                statement.AudioDuration = audioFile1.TotalTime;
                isNewFile = true;
            }

            return isNewFile;
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

                if (string.IsNullOrEmpty(this.Model.PortraitImagePath) && !isGeneratingPortrait)
                {
                    throw new CustomApplicationException("Portrait image not uploaded or generated");
                }
            }

            return projectFullPath;
        }

        private async Task GenerateImage(string projectFullPath, int countImageMain, Statement statement, CancellationToken token)
        {
            string[] modelsIds = new string[] {
                     //"e316348f-7773-490e-adcd-46757c738eb7", //Abosulte Reality v1.6 
                       "ac614f96-1082-45bf-be9d-757f2d31c174" //DreamShaper v7
                };
            Random r = new();
            int rn = r.Next(1, modelsIds.Length + 1);

            var imageId = await this.imageGeneratorAI.Generate(new OptionsImageGenerator
            {
                Width = FFMPEGDefinitions.WidthResolution,//512, //832,
                Height = FFMPEGDefinitions.HeightResolution,//904, //1472,
                ModelId = modelsIds[rn - 1],
                NumImages = 1,
                Prompt = statement.Text,
                NegativePrompt = Model.NegativePrompt
            });
            statement.ImageId = imageId.Id;

            ResultImagesGenerated response;
            do
            {
                response = await this.imageGeneratorAI.GetImages(new ResultGenerate { Id = imageId.Id });

                if (token.IsCancellationRequested)
                {
                    throw new CustomApplicationException("Operantion Cancelled by User");
                }

                if (response == null)
                {
                    await Task.Delay(3000, token);
                }

            } while (response == null || response.Images.Count == 0);

            var ci = 1;
            foreach (var image in response.Images)
            {
                var imageFileName = $"{statement.Text[..Math.Min(statement.Text.Length, Constants.MAX_PATH)]}";
                imageFileName = Path.Combine(projectFullPath, $"{countImageMain}.{ci++}.-{CleanFileName(imageFileName)}.jpg");

                statement.Images.Add(new StatementImage
                {
                    Path = imageFileName,
                });

                File.WriteAllBytes(imageFileName, image.Image);
            }
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

                if (this.Model.PortraitImagePath != null)
                {
                    BitmapImage bitmapImage = new();
                    bitmapImage.BeginInit();
                    bitmapImage.UriSource = new Uri(Path.Combine(basePath, this.Model.PortraitImagePath));
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

        private static void RemoveTempFile(string tempFileFinalVideo)
        {
            File.Delete(tempFileFinalVideo);
            string path = tempFileFinalVideo.Replace(".mp4", "")
                          .Replace(".wav", "")
                          .Replace(".mp3", "");
            File.Delete(path);
        }

        public static string CleanFileName(string fileName)
        {
            string safeFileName = fileName;
            char[] invalidChars = Path.GetInvalidFileNameChars();

            foreach (char invalidChar in invalidChars)
            {
                safeFileName = safeFileName.Replace(invalidChar.ToString(), "_");
            }

            return safeFileName;
        }

        public static string RemoveAccentuation(string input)
        {
            // Create a NormalizationForm that decomposes accented characters into multiple separate characters
            NormalizationForm normalizationForm = NormalizationForm.FormD;

            // Normalize the input string using the specified normalization form
            string normalizedString = input.Normalize(normalizationForm);

            // Remove any non-spacing combining characters (accentuation marks)
            StringBuilder result = new();
            foreach (char c in normalizedString)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                    result.Append(c);
            }

            // Return the final result as a normalized string without accentuation characters
            return result.ToString().Normalize(NormalizationForm.FormC);
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
