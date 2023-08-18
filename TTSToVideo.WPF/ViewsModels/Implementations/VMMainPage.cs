using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.VisualBasic.Devices;
using NAudio.Wave;
using NetXP.Exceptions;
using NetXP.ImageGeneratorAI;
using NetXP.Processes;
using NetXP.Processes.Implementations;
using NetXP.TTS;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
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
using TTSToVideo.WPF.Helpers;
using Xabe.FFmpeg;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TTSToVideo.WPF.ViewModel.Implementations
{

    public class VMMainPage : ObservableRecipient, IVMMainPage
    {
        private readonly IImageGeneratorAI imageGeneratorAI;
        private readonly ITTS tts;
        public IVMConfiguration VMConf { get; }
        public IIOTerminal Terminal { get; }
        public AsyncRelayCommand ProcessCommand { get; set; }
        public AsyncRelayCommand OpenExplorerCommand { get; set; }
        public AsyncRelayCommand OpenVideoCommand { get; set; }
        public AsyncRelayCommand<string> ProjectNameSelectionChangedCommand { get; set; }

        public string _prompt;
        public string Prompt { get => _prompt; set => SetProperty(ref _prompt, value, true); }

        public string _negativePrompt;
        public string NegativePrompt { get => _negativePrompt; set => SetProperty(ref _negativePrompt, value, true); }

        public string _projectName;
        public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value); }

        private string projectNameSelected;
        public string ProjectNameSelected { get => projectNameSelected; set => SetProperty(ref projectNameSelected, value); }

        public ObservableCollection<string> _projectsNames;
        public ObservableCollection<string> ProjectsNames { get => _projectsNames; set => SetProperty(ref _projectsNames, value, true); }
        public int WidthResolution { get; private set; } = 512;
        public int HeightResolution { get; private set; } = 904;
        public string FinalProjectVideoPathWithVoice { get; private set; }

        public VMMainPage(IImageGeneratorAI imageGeneratorAI,
                          ITTS tts,
                          IVMConfiguration configuration,
                          IIOTerminal terminal
        )
        {
            ProcessCommand = new AsyncRelayCommand(ProcessCommandExecute);
            OpenExplorerCommand = new AsyncRelayCommand(OpenExplorer);
            OpenVideoCommand = new AsyncRelayCommand(OpenVideo);
            ProjectNameSelectionChangedCommand = new AsyncRelayCommand<string>(ProjectNameSelectionChangedCommandExecute);
            this.imageGeneratorAI = imageGeneratorAI;
            this.tts = tts;
            this.VMConf = configuration;
            this.Terminal = terminal;
            this.Prompt = "";


        }

        public async Task Init()
        {
            await this.VMConf.Init();

            this.ProjectsNames = new ObservableCollection<string>();

            if (!Directory.Exists(this.VMConf.Model.ProjectBaseDir))
            {
                Directory.CreateDirectory(this.VMConf.Model.ProjectBaseDir);
            }
            else
            {
                var directories = Directory.GetDirectories(this.VMConf.Model.ProjectBaseDir, $"{this.VMConf.Model.ProjectBaseDirPrefix}*");
                directories.ToList().ForEach(o => this.ProjectsNames.Add(Path.GetFileName(o)));
            }
        }

        private async Task OpenVideo()
        {
            Process.Start(
                new ProcessStartInfo {
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
                    FileName = $"\"{this.GetProjectPath(this.ProjectName)}\"",
                    UseShellExecute = true,
                    Verb = "open"
                }
                );
            await Task.Delay(0);
        }

        private async Task ProjectNameSelectionChangedCommandExecute(string? p)
        {
            if (p == null) return;

            string projectFullPath = GetProjectPath(p);
            var prompt = Path.Combine(projectFullPath, "Prompt.txt");
            if (File.Exists(prompt))
            {
                this.Prompt = await File.ReadAllTextAsync(prompt);
            }

            var nprompt = Path.Combine(projectFullPath, "NegativePrompt.txt");
            if (File.Exists(nprompt))
            {
                this.NegativePrompt = await File.ReadAllTextAsync(nprompt);
            }

            this.FinalProjectVideoPathWithVoice = this.GetProjectPath(ProjectName) + "\\" + $"{ProjectName}-Final.mp4";
        }

        private async Task ProcessCommandExecute()
        {
            WeakReferenceMessenger.Default.Send(new Message { Text = "" });

            if (string.IsNullOrEmpty(this.Prompt))
            {
                throw new CustomApplicationException("Text Empty");
            }

            if (string.IsNullOrEmpty(this.ProjectName))
            {
                throw new CustomApplicationException("Project Name Empty");
            }

            string projectFullPath = GetProjectPath(this.ProjectName);

            ProjectsNames.Add($"{this.VMConf.Model.ProjectBaseDirPrefix}{this.ProjectName}");
            Directory.CreateDirectory(projectFullPath);

            //Split text process text with dot and paragraph
            string[] paragraphs = Prompt.Split(new string[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            List<Statement> statements = new();

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

            File.WriteAllText(Path.Combine(projectFullPath, $"Prompt.txt"), Prompt);
            File.WriteAllText(Path.Combine(projectFullPath, $"NegativePrompt.txt"), NegativePrompt);

            var ttsVoices = await tts.GetTTSVoices("es");

            //Getting the getting voices
            int ca = 1;
            bool anyNewFile = false;
            foreach (var statement in statements)
            {
                var audioFileName = $"{statement.Text[..Math.Min(statement.Text.Length, Constants.MAX_PATH)]}";
                audioFileName = Path.Combine(projectFullPath, $"{ca++}.-{CleanFileName(audioFileName)}.wav");

                statement.AudioPath = audioFileName;

                if (File.Exists(audioFileName))
                {
                    using var audioFile_ = new AudioFileReader(statement.AudioPath);
                    statement.AudioDuration = audioFile_.TotalTime;
                }
                else
                {
                    anyNewFile = true;

                    var audio = await tts.Convert(new TTSConvertOption
                    {
                        Text = statement.Text,
                        Voice = ttsVoices.FirstOrDefault(o => o.Id == "es_tux")
                    });

                    var buffer = audio.File.GetBuffer();

                    File.WriteAllBytes(audioFileName, buffer);

                    using var audioFile1 = new AudioFileReader(statement.AudioPath);
                    statement.AudioDuration = audioFile1.TotalTime;
                }
            }

            //Concating voices 
            var tempVoiceFileA = $"{Path.GetTempFileName()}.mp4";
            var tempVoiceFileB = $"{Path.GetTempFileName()}.mp4";

            var previous = statements.First().AudioPath;
            foreach (var s in statements.Skip(1))
            {
                AudioHelper.ConcatenateAudioFiles(tempVoiceFileA, new string[] { previous, s.AudioPath });
                File.Copy(tempVoiceFileA, tempVoiceFileB, true);
                previous = tempVoiceFileB;
            }

            var bigAudioPath = Path.Combine(projectFullPath, $"Voices Concatenated.mp4");
            File.Copy(previous, bigAudioPath, true);

            RemoveTempFile(tempVoiceFileA);
            RemoveTempFile(tempVoiceFileB);

            //Taking Picture
            int numImages = 1;
            foreach (var statement in statements)
            {
                int ci = 1;
                bool notExistsOneImage = false;
                for (int i = 0; i < numImages; i++)
                {
                    var imageFileName = $"{statement.Text.Substring(0, Math.Min(statement.Text.Length, Constants.MAX_PATH))}";
                    imageFileName = Path.Combine(projectFullPath, $"{ci++}.-{CleanFileName(imageFileName)}.jpg");


                    statement.Images.Add(new StatementImage
                    {
                        Path = imageFileName
                    });

                    if (!File.Exists(imageFileName))
                    {
                        notExistsOneImage = true;
                    }
                }

                string[] modelsIds = new string[] {
                      "e316348f-7773-490e-adcd-46757c738eb7" //Abosulte Reality v1.6 
                    , "ac614f96-1082-45bf-be9d-757f2d31c174" //DreamShaper v7
                };
                Random r = new();
                int rn = r.Next(1, 3);



                if (notExistsOneImage)
                {
                    var imageId = await this.imageGeneratorAI.Generate(new OptionsImageGenerator
                    {
                        Width = WidthResolution,//512, //832,
                        Height = HeightResolution,//904, //1472,
                        ModelId = modelsIds[rn - 1],
                        NumImages = 1,
                        Prompt = statement.Text,
                        NegativePrompt = NegativePrompt
                    });
                    statement.ImageId = imageId.Id;

                    ResultImagesGenerated response;
                    do
                    {
                        response = await this.imageGeneratorAI.GetImages(new ResultGenerate { Id = imageId.Id });

                        if (response == null)
                            await Task.Delay(3000);

                    } while (response == null || response.Images.Count == 0);

                    ci = 1;
                    foreach (var image in response.Images)
                    {
                        var imageFileName = $"{statement.Text.Substring(0, Math.Min(statement.Text.Length, Constants.MAX_PATH))}";
                        imageFileName = Path.Combine(projectFullPath, $"{ci++}.-{CleanFileName(imageFileName)}.jpg");

                        File.WriteAllBytes(imageFileName, image.Image);
                    }
                }
            }

            //Making Audio
            Random random = new();
            int randomNumber = random.Next(1, 3);
            var audioFilePath = Directory.GetFiles(this.VMConf.Model.MusicDir, "*.wav")[randomNumber - 1];

            TimeSpan desiredDuration = new();  // Adjust this value for desired audio duration
            foreach (var s in statements)
            {
                desiredDuration += s.AudioDuration;
            }
            desiredDuration += new TimeSpan(0, 0, 10);
            using var audioFileReal = new AudioFileReader(audioFilePath);

            double cut = audioFileReal.TotalTime.TotalSeconds - 10;

            var tempAudioFileA = $"{Path.GetTempFileName()}.mp4";
            //AudioHelper.CutAudio(audioFilePath, tempAudioFileA, cut);
            File.Copy(audioFilePath, tempAudioFileA, true);

            using var audioFile = new AudioFileReader(tempAudioFileA);

            var tempAudioFileB = $"{Path.GetTempFileName()}.mp4";
            //AudioHelper.CutAudio(audioFilePath, tempAudioFileB, cut);
            File.Copy(audioFilePath, tempAudioFileB, true);

            var outputMusicFile = "output.wav";

            var tempAudioFileC = $"{Path.GetTempFileName()}.mp4";
            for (double s = 0; s < desiredDuration.TotalSeconds; s += audioFile.TotalTime.TotalSeconds)
            {
                AudioHelper.ConcatenateAudioFiles(tempAudioFileC, new string[] { tempAudioFileA, tempAudioFileB });
                File.Copy(tempAudioFileC, tempAudioFileA, true);
            }

            using var a = new AudioFileReader(tempAudioFileC);
            cut = Math.Min(a.TotalTime.TotalSeconds, desiredDuration.TotalSeconds);

            AudioHelper.CutAudio(tempAudioFileC, tempAudioFileA, cut);
            AudioHelper.DecreaseVolumeAtSpecificTime(tempAudioFileA, tempAudioFileB, TimeSpan.FromSeconds(cut - 10), 0.5f);
            File.Copy(tempAudioFileB, outputMusicFile, true);

            RemoveTempFile(tempAudioFileA);
            RemoveTempFile(tempAudioFileB);
            RemoveTempFile(tempAudioFileC);

            //Making the Video
            #region Making the Video
            var finalProjectVideoPath = GetProjectPath(ProjectName) + "\\" + $"{ProjectName}.mp4";

            var previousVideo = "";

            string output = "";
            string error = "";
            string outputPath = "";
            string tempFile = "";

            foreach (var s in statements)
            {
                // Set the paths to the input files
                string audioPath = s.AudioPath;
                string picturePath = s.Images.First().Path;
                //string subtitlePath = "path_to_subtitle_file";

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
                await process.WaitForExitAsync();

                error = process.StandardError.ReadToEnd();

                Console.WriteLine("Output: " + output);
                Console.WriteLine("Error: " + error);

                var subtitleFilePathRare = subtitleFilePath
                            .Replace("\\", "\\\\\\\\")
                            .Replace(":", "\\:");

                // Set the output file path
                outputPath = s.AudioPath + ".mp4";

                if (!File.Exists(outputPath))
                {
                    //subtitlePosition = The first in the top and continue with midle position of subtitle
                    var subtitlePosition = (statements[0] == s) ? ",Alignment=10" : ",MarginV=30";

                    // Run FFmpeg process
                    process = new Process();
                    process.StartInfo.FileName = "ffmpeg";
                    process.StartInfo.Arguments = $"-loop 1 -y" +
                                                  $" -i \"{picturePath}\" " +
                                                  $" -f lavfi " +
                                                  $" -t \"{s.AudioDuration.TotalSeconds}\" " +
                                                  $" -i anullsrc=r=44100:cl=stereo " +
                                                  $"-vf \"subtitles='{subtitleFilePathRare}.ass':force_style='Fontsize=12{subtitlePosition}'\" " +
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
                        throw new CustomApplicationException("Error when try to create video with image", new Exception(error));
                    }

                    await process.WaitForExitAsync();

                    Console.WriteLine("Error: " + error);
                    File.Delete(subtitleFilePath);
                }

                //Mergins videos to make final Video
                if (statements[0] == s)
                {
                    tempFile = $"{Path.GetTempFileName()}.mp4";
                    File.Copy($"{s.AudioPath}.mp4", tempFile, true);
                    previousVideo = tempFile;
                }
                else
                {
                    await JoiningVideos(previousVideo, outputPath, finalProjectVideoPath);

                    if (File.Exists(previousVideo))
                    {
                        RemoveTempFile(previousVideo);
                    }

                    tempFile = $"{Path.GetTempFileName()}.mp4";
                    File.Copy(finalProjectVideoPath, tempFile, true);
                    previousVideo = tempFile;
                }
            }

            tempFile = $"{Path.GetTempFileName()}.mp4";
            File.Copy(finalProjectVideoPath, tempFile, true);
            previousVideo = tempFile;

            var lastStatement = statements.Last();
            var lastImage = lastStatement.Images[0].Path;
            outputPath = $"{lastStatement.AudioPath}-no-text.mp4";

            //Run ffmpeg process
            var p = new Process();
            p.StartInfo.FileName = "ffmpeg";
            p.StartInfo.Arguments = $"-loop 1 -y" +
                                    $" -i \"{lastImage}\" " +
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

            await p.WaitForExitAsync();

            //Adding last image
            await JoiningVideos(previousVideo, outputPath, finalProjectVideoPath);

            var finalProjectVideoPathWithAudio = this.GetProjectPath(ProjectName) + "\\" + $"{ProjectName}-Music-Final.mp4";
            await this.MixAudioWithVideo(finalProjectVideoPath
                                       , outputMusicFile
                                       , finalProjectVideoPathWithAudio
                                       );

            if (string.IsNullOrEmpty(FinalProjectVideoPathWithVoice))
            {
                this.FinalProjectVideoPathWithVoice = this.GetProjectPath(ProjectName) + "\\" + $"{ProjectName}-Final.mp4";
            }

            await this.MixAudioWithVideo(finalProjectVideoPathWithAudio
                                       , bigAudioPath
                                       , FinalProjectVideoPathWithVoice
                                       );

            RemoveTempFile(previousVideo);

            #endregion

            WeakReferenceMessenger.Default.Send(new Message { Text = "Process Finished." });
        }

        private static void RemoveTempFile(string tempFileFinalVideo)
        {
            File.Delete(tempFileFinalVideo);
            string path = tempFileFinalVideo.Replace(".mp4", "");
            path = tempFileFinalVideo.Replace(".wav", "");
            path = tempFileFinalVideo.Replace(".mp3", "");
            File.Delete(path);
        }

        private async Task JoiningVideos(string previousVideo, string outputPath, string finalProjectVideoPath, string additionalArgs = "")
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

            await process.WaitForExitAsync();

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

        public async Task MixAudioWithVideo(string videoFilePath, string audioFilePath, string outputFilePath)
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

            await process.WaitForExitAsync();

            Console.WriteLine("Error: " + tmpErrorOut);
        }
    }
}
