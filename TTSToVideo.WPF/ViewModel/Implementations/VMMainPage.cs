using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetXP.Exceptions;
using NetXP.ImageGeneratorAI;
using NetXP.TTS;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TTSToVideo.WPF.Helpers;
using static System.Net.Mime.MediaTypeNames;

namespace TTSToVideo.WPF.ViewModel.Implementations
{

    public class VMMainPage : ObservableRecipient, IVMMainPage
    {
        private readonly IImageGeneratorAI imageGeneratorAI;
        private readonly ITTS tts;
        public IVMConfiguration VMConfiguration { get; }

        public AsyncRelayCommand ProcessCommand { get; set; }
        public AsyncRelayCommand<string> ProjectNameSelectionChangedCommand { get; set; }

        public string _text;
        public string Text { get => _text; set => SetProperty(ref _text, value, true); }

        public string _projectName;
        public string ProjectName { get => _projectName; set => SetProperty(ref _projectName, value); }

        private string projectNameSelected;
        public string ProjectNameSelected { get => projectNameSelected; set => SetProperty(ref projectNameSelected, value); }

        public ObservableCollection<string> _projectsNames;
        private CancellationToken cancellationToken;

        public ObservableCollection<string> ProjectsNames { get => _projectsNames; set => SetProperty(ref _projectsNames, value, true); }

        public VMMainPage(IImageGeneratorAI imageGeneratorAI,
                          ITTS tts,
                          IVMConfiguration configuration
        )
        {
            ProcessCommand = new AsyncRelayCommand(ProcessCommandExecute);
            ProjectNameSelectionChangedCommand = new AsyncRelayCommand<string>(ProjectNameSelectionChangedCommandExecute);
            this.imageGeneratorAI = imageGeneratorAI;
            this.tts = tts;
            this.VMConfiguration = configuration;
            this.Text = "";

            this.VMConfiguration.Init();

            this.ProjectsNames = new ObservableCollection<string>();

            if (!Directory.Exists(this.VMConfiguration.ProjectBaseDir))
            {
                Directory.CreateDirectory(this.VMConfiguration.ProjectBaseDir);
            }
            else
            {
                var directories = Directory.GetDirectories(this.VMConfiguration.ProjectBaseDir, $"{this.VMConfiguration.ProjectBaseDirPrefix}*");
                directories.ToList().ForEach(o => this.ProjectsNames.Add(Path.GetFileName(o)));
            }
        }

        private async Task ProjectNameSelectionChangedCommandExecute(string p)
        {
            if (p == null) return;

            string projectFullPath = GetProjectPath(p);
            projectFullPath = Path.Combine(projectFullPath, "Text.txt");
            if (File.Exists(projectFullPath))
            {
                this.Text = File.ReadAllText(projectFullPath);
            }
        }

        private async Task ProcessCommandExecute()
        {
            if (string.IsNullOrEmpty(this.Text))
            {
                throw new CustomApplicationException("Text Empty");
            }

            if (string.IsNullOrEmpty(this.ProjectName))
            {
                throw new CustomApplicationException("Project Name Empty");
            }

            string projectFullPath = GetProjectPath(this.ProjectName);

            ProjectsNames.Add($"{this.VMConfiguration.ProjectBaseDirPrefix}{this.ProjectName}");
            Directory.CreateDirectory(projectFullPath);

            //Split text process text with dot and paragraph
            string[] paragraphs = Text.Split(new string[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            List<Statement> statements = new List<Statement>();

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

            var ttsVoices = await tts.GetTTSVoices("es");

            //Getting the audio for duration 
            int ca = 1;
            foreach (var statement in statements)
            {
                var audioFileName = $"{statement.Text[..Math.Min(statement.Text.Length, Constants.MAX_PATH)]}";
                audioFileName = Path.Combine(projectFullPath, $"{ca++}.-{audioFileName}.wav");

                if (File.Exists(audioFileName)) continue;

                var audio = await tts.Convert(new TTSConvertOption
                {
                    Text = statement.Text,
                    Voice = ttsVoices.FirstOrDefault(o => o.Id == "es_tux")
                });

                var buffer = audio.File.GetBuffer();

                File.WriteAllBytes(audioFileName, buffer);
            }

            File.WriteAllText(Path.Combine(projectFullPath, $"Text.txt"), Text);

            //Taking Picture
            int numImages = 1;
            foreach (var statement in statements)
            {
                int ci = 1;
                bool notExistsOneImage = false;
                for (int i = 0; i < numImages; i++)
                {
                    var imageFileName = $"{statement.Text.Substring(0, Math.Min(statement.Text.Length, Constants.MAX_PATH))}";
                    imageFileName = Path.Combine(projectFullPath, $"{ci++}.-{imageFileName}.jpg");
                    if (!File.Exists(imageFileName))
                    {
                        notExistsOneImage = true;
                        break;
                    }
                }

                if (notExistsOneImage)
                {
                    var imageId = await this.imageGeneratorAI.Generate(new OptionsImageGenerator
                    {
                        Width = 832,
                        Height = 1472,
                        ModelId = "e316348f-7773-490e-adcd-46757c738eb7",
                        NumImages = 1,
                        Prompt = statement.Text,
                    });
                    statement.ImageId = imageId.Id;

                    ResultImagesGenerated response;
                    do 
                    {
                        response = await this.imageGeneratorAI.GetImages(new ResultGenerate { Id = imageId.Id });

                        if (response == null)
                            await Task.Delay(3000);

                    } while (response == null || response.Images.Count == 0) ;

                    ci = 1;
                    foreach (var image in response.Images)
                    {
                        var imageFileName = $"{statement.Text.Substring(0, Math.Min(statement.Text.Length, Constants.MAX_PATH))}";
                        imageFileName = Path.Combine(projectFullPath, $"{ci++}.-{imageFileName}.jpg");
                        File.WriteAllBytes(imageFileName, image.Image);
                    }
                }
            }


            //Taking pictures
            //Saving pictures in the proyect folder

            //Making the video
            //Saving the video in the proyecto folder
        }

        private string GetProjectPath(string p)
        {
            if (p != null)
            {
                var prefix = p.StartsWith(this.VMConfiguration.ProjectBaseDirPrefix) ? "" : this.VMConfiguration.ProjectBaseDirPrefix;
                var projectFullPath = Path.Combine($"{this.VMConfiguration.ProjectBaseDir}", $"{prefix}{p}");
                return projectFullPath;
            }
            return "";
        }
    }
}
