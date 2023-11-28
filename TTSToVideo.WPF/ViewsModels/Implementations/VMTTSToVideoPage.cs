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

    public class VMTTSToVideoPage : ObservableRecipient, IVMTTSToVideoPage
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

        private MTTSToVideo model;
        private bool fileAlreadyExists;

        public MTTSToVideo Model { get => model; set => SetProperty(ref model, value); }

        public int WidthResolution { get; private set; } = 512;
        public int HeightResolution { get; private set; } = 904;
        public string FinalProjectVideoPathWithVoice { get; private set; }
        public CancellationTokenSource CancellationTokenSource { get; private set; }

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
            this.Model = new MTTSToVideo
            {
                Prompt = ""
            };
        }

        private Task SaveCommandExecute()
        {
            throw new NotImplementedException();
        }

        private async Task GeneratePortraitImageCommandExecute()
        {
            this.CancellationTokenSource = new CancellationTokenSource();
            var token = CancellationTokenSource.Token;

            var projectFullPath = this.GetProjectPath(this.Model.ProjectName);
            int countImageMain = 1;

            var statement = new Statement { Text = this.Model.PortraitText };
            await GenerateImage(projectFullPath, countImageMain, statement, token);

            this.Model.PortraitImagePath = Path.GetFileName(statement.Images.First().Path);

            var fullPath = this.GetProjectPath(this.Model.ProjectName);
            await this.SaveModel(fullPath);
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
                var directories = Directory.GetDirectories(this.VMConf.Model.ProjectBaseDir, $"{this.VMConf.Model.ProjectBaseDirPrefix}*");
                directories.ToList().ForEach(o => this.VMConf.Model.ProjectsNames.Add(Path.GetFileName(o)));
            }
        }

        private async Task OpenVideo()
        {
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
            this.Model = new MTTSToVideo();

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
                this.CancellationTokenSource = new CancellationTokenSource();
                var token = CancellationTokenSource.Token;

                WeakReferenceMessenger.Default.Send(new Message { Text = "" });

                string projectFullPath = Validation();

                Directory.CreateDirectory(projectFullPath);
                VMConf.Model.ProjectsNames.Add($"{this.VMConf.Model.ProjectBaseDirPrefix}{this.Model.ProjectName}");

                //Split text process text with dot and paragraph
                string[] paragraphs = Model.Prompt.Split(new string[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                statements = paragraphs.Select(o => new Statement { Text = o }).ToList();

                var statementPortraitVoice = new Statement()
                {
                    Images = new List<StatementImage> { new StatementImage { Path = Path.Combine(projectFullPath, this.Model.PortraitImagePath) } },
                    Text = this.Model.PortraitVoice,
                    AudioPath = Path.Combine(projectFullPath, "portrait-voice.wav")
                };

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
                anyNewFile = await GetVoice(ttsVoice, statementPortraitVoice, token);

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
                    previous = statementPortraitVoice.AudioPath;
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
                        throw new CustomApplicationException("Operantion Cancelled by User");
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
                desiredDuration += statementPortraitVoice.AudioDuration;
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

                outputPath = statementPortraitVoice.AudioPath + ".mp4";
                FfmpegOptions ffmpegOptions = new()
                {
                    FontStyle = new FfmpegFontStyle
                    {
                        Alignment = FfmpegAlignment.TopCenter,
                        FontSize = 11
                    }
                };
                await CreateVideoWithSubtitle(token, outputPath, statementPortraitVoice, ffmpegOptions);

                tempFile = $"{Path.GetTempFileName()}.mp4";
                File.Copy(outputPath, tempFile, true);
                previousVideo = tempFile;

                foreach (var s in statements)
                {
                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion Cancelled by User");
                    }


                    // Set the output file path
                    outputPath = s.AudioPath + ".mp4";
                    ffmpegOptions = new()
                    {
                        FontStyle = new()
                        {
                            Alignment = FfmpegAlignment.TopCenter,
                            FontSize = 11
                        }
                    };
                    await CreateVideoWithSubtitle(token, outputPath, s, ffmpegOptions);

                    await JoiningVideos(previousVideo, outputPath, finalProjectVideoPath, token);

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
                await GenerateVideoWithImage(outputPath, lastImage, token);

                //Adding last image
                await JoiningVideos(previousVideo, outputPath, finalProjectVideoPath, token);

                var finalProjectVideoPathWithAudio = this.GetProjectPath(Model.ProjectName) + "\\" + $"{Model.ProjectName}-Music-Final.mp4";
                await this.MixAudioWithVideo(finalProjectVideoPath
                                           , outputMusicFile
                                           , finalProjectVideoPathWithAudio
                                           , token);

                if (string.IsNullOrEmpty(FinalProjectVideoPathWithVoice))
                {
                    this.FinalProjectVideoPathWithVoice = this.GetProjectPath(Model.ProjectName) + "\\" + $"{Model.ProjectName}-Final.mp4";
                }

                await this.MixAudioWithVideo(finalProjectVideoPathWithAudio
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

        private static async Task CreateVideoWithSubtitle(CancellationToken token, string outputPath, Statement s, FfmpegOptions ffmpegOptions)
        {
            string subtitleFilePath = Path.GetTempFileName();

            File.WriteAllText(subtitleFilePath, $"1{Environment.NewLine}0:0:0.000 --> {s.AudioDuration:h\\:m\\:s\\.fff}{Environment.NewLine}{s.Text}");

            // Subtitles to ASS
            Process process = new Process();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = $" -i {subtitleFilePath} {subtitleFilePath}.ass";

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            process.Start();
            await process.WaitForExitAsync(token);

            var error = process.StandardError.ReadToEnd();

            Console.WriteLine("Error: " + error);

            var subtitleFilePathRare = subtitleFilePath
                        .Replace("\\", "\\\\\\\\")
                        .Replace(":", "\\:");


            if (!File.Exists(outputPath))
            {
                //Force_Style for ffmpeg
                var forceStyle = "";
                var options = new List<string>();
                if (ffmpegOptions.FontStyle.Alignment != null)
                {
                    options.Add($"Alignment={(byte)ffmpegOptions.FontStyle.Alignment.Value}");
                }

                if (ffmpegOptions.FontStyle.MarginV != null)
                {
                    options.Add($"MarginV={(byte)ffmpegOptions.FontStyle.MarginV.Value}");
                }

                if (ffmpegOptions.FontStyle.FontSize != null)
                {
                    options.Add($"Fontsize={(byte)ffmpegOptions.FontStyle.FontSize.Value}");
                }


                forceStyle = string.Join(",", options);

                if (!string.IsNullOrEmpty(forceStyle))
                {
                    forceStyle = $":force_style={forceStyle}";
                }

                string picturePath = s.Images.First().Path;

                // Run FFmpeg process
                process = new Process();
                process.StartInfo.FileName = "ffmpeg";
                process.StartInfo.Arguments = $"-loop 1 -y" +
                                              $" -i \"{picturePath}\" " +
                                              $" -f lavfi " +
                                              $" -t \"{s.AudioDuration.TotalSeconds}\" " +
                                              $" -i anullsrc=r=44100:cl=stereo " +
                                              $"-vf \"subtitles='{subtitleFilePathRare}.ass':force_style='{forceStyle}'\" " +
                                              $"-c:v libx264 " +
                                              $"-shortest \"{outputPath}\"";

                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();

                process.BeginOutputReadLine();
                error = process.StandardError.ReadToEnd();

                if (error.Contains("Error"))
                {
                    throw new Exception("Error when try to create video with image", new Exception(error));
                }

                await process.WaitForExitAsync(token);

                Console.WriteLine("Error: " + error);
                File.Delete(subtitleFilePath);
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

        private static async Task GenerateVideoWithImage(string outputPath, string inputImagePath, CancellationToken token)
        {
            var p = new Process();
            p.StartInfo.FileName = "ffmpeg";
            p.StartInfo.Arguments = $"-loop 1 -y" +
                                    $" -i \"{inputImagePath}\" " +
                                    $" -f lavfi " +
                                    $" -t 10 " +
                                    $" -i anullsrc=r=44100:cl=stereo " +
                                    $"-c:v libx264 " +
                                    $"-shortest \"{outputPath}\"";

            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.Start();

            p.BeginOutputReadLine();
            string tmpErrorOut = p.StandardError.ReadToEnd();

            await p.WaitForExitAsync(token);
        }

        private string Validation()
        {
            if (string.IsNullOrEmpty(this.Model.Prompt))
            {
                throw new CustomApplicationException("Text Empty");
            }

            if (string.IsNullOrEmpty(this.Model.ProjectName))
            {
                throw new CustomApplicationException("Project Name Empty");
            }

            string projectFullPath = GetProjectPath(this.Model.ProjectName);

            var invalidChars = Path.GetInvalidFileNameChars();
            var invalidCharsInPath = Model.ProjectName.Where(o => invalidChars.Any(a => a == o));
            if (invalidCharsInPath.Any())
            {
                throw new CustomApplicationException($"There are invalid chars in project name => \"{string.Join(",", invalidCharsInPath)}\"");
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
                Width = WidthResolution,//512, //832,
                Height = HeightResolution,//904, //1472,
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
                var imageFileName = $"{statement.Text.Substring(0, Math.Min(statement.Text.Length, Constants.MAX_PATH))}";
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
            var json = await File.ReadAllTextAsync(Path.Combine(basePath, "TTSToVideo.json"));
            if (json == null)
            {
                this.Model = new MTTSToVideo();
            }
            else
            {
                var model = JsonConvert.DeserializeObject<MTTSToVideo>(json);
                this.Mapper.Map<MTTSToVideo, MTTSToVideo>(model, this.Model);

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

        private async Task CancelCommandExecute()
        {
            this.CancellationTokenSource.Cancel();
            await Task.Delay(0);
        }

        private static void RemoveTempFile(string tempFileFinalVideo)
        {
            File.Delete(tempFileFinalVideo);
            string path = tempFileFinalVideo.Replace(".mp4", "");
            path = tempFileFinalVideo.Replace(".wav", "");
            path = tempFileFinalVideo.Replace(".mp3", "");
            File.Delete(path);
        }

        private async Task JoiningVideos(string previousVideo, string outputPath, string finalProjectVideoPath, CancellationToken token, string additionalArgs = "")
        {
            Process process;
            // Build the FFmpeg command to merge the videos
            string scale = $"{WidthResolution}:{HeightResolution}";
            string filter = $"[0:v]scale={scale},setsar=1[v0];[1:v]scale={scale},setsar=1[v1];[v0][0:a][v1][1:a]concat=n=2:v=1:a=1[vv][a];[vv]fps=30,format=yuv420p[v]";


            string ffmpegCmd = $" {additionalArgs} -i \"{previousVideo}\" -i \"{outputPath}\" " +
                               $" -filter_complex {filter}" +
                               $" -map \"[v]\" -map \"[a]\" -c:v libx264 -y" +
                               $" \"{finalProjectVideoPath}\"";

            // Run FFmpeg process
            process = new Process();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = ffmpegCmd;

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();

            process.BeginOutputReadLine();
            string tmpErrorOut = process.StandardError.ReadToEnd();

            await process.WaitForExitAsync(token);

        }

        private static bool IsFFmpegAvailable()
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "ffmpeg";
                    process.StartInfo.Arguments = "-version";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public string CleanFileName(string fileName)
        {
            string safeFileName = fileName;
            char[] invalidChars = Path.GetInvalidFileNameChars();

            foreach (char invalidChar in invalidChars)
            {
                safeFileName = safeFileName.Replace(invalidChar.ToString(), "_");
            }

            return safeFileName;
        }

        public string RemoveAccentuation(string input)
        {
            // Create a NormalizationForm that decomposes accented characters into multiple separate characters
            NormalizationForm normalizationForm = NormalizationForm.FormD;

            // Normalize the input string using the specified normalization form
            string normalizedString = input.Normalize(normalizationForm);

            // Remove any non-spacing combining characters (accentuation marks)
            StringBuilder result = new StringBuilder();
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
                var prefix = p.StartsWith(this.VMConf.Model.ProjectBaseDirPrefix) ? "" : this.VMConf.Model.ProjectBaseDirPrefix;
                var projectFullPath = Path.Combine($"{this.VMConf.Model.ProjectBaseDir}", $"{prefix}{p}");
                return projectFullPath;
            }
            return "";
        }

        public async Task MixAudioWithVideo(string videoFilePath, string audioFilePath, string outputFilePath, CancellationToken token)
        {
            // Check if ffmpeg executable exists in the system PATH
            if (!IsFFmpegAvailable())
            {
                throw new FileNotFoundException("ffmpeg executable not found. Make sure it's installed and added to the system PATH.");
            }

            // Execute ffmpeg command to mix audio with video
            string arguments = $"-i \"{videoFilePath}\" -i \"{audioFilePath}\" -c:v copy -c:a aac -y -filter_complex \"[0:a][1:a] amix=inputs=2:duration=longest [audio_out]\" -map 0:v -map \"[audio_out]\" \"{outputFilePath}\"";

            var process = new Process();
            process.StartInfo.FileName = "ffmpeg";
            process.StartInfo.Arguments = arguments;

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();

            process.BeginOutputReadLine();
            string tmpErrorOut = process.StandardError.ReadToEnd();

            await process.WaitForExitAsync(token);

            Console.WriteLine("Error: " + tmpErrorOut);
        }
    }
}
