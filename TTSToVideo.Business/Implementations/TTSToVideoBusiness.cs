using Microsoft.VisualBasic;
using NAudio.Wave;
using NetXP.Exceptions;
using NetXP.ImageGeneratorAI;
using NetXP.Tts;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using TTSToVideo.Business.Models;
using TTSToVideo.Helpers;
using TTSToVideo.WPF.Helpers;
using Constants = TTSToVideo.Helpers.Constants;

namespace TTSToVideo.Business.Implementations
{
    public class TTSToVideoBusiness(IImageGeneratorAI imageGeneratorAI, ITts tts, IProgressBar progressBar) : ITTSToVideoBusiness
    {

        public async Task GeneratePortraitVideoCommandExecute(string imagePath, string outputPath)
        {
            var video = await imageGeneratorAI.GenerateVideoFromImage(
                new ParameterVideoGenerator
                {
                    ImageUrlOrPath = imagePath
                });
            File.WriteAllBytes(outputPath, video.Video);
        }

        public async Task GeneratePortraitImageCommandExecute(Statement statement, string[] imageModelIds, string outputFolder, TTSToVideoOptions options, CancellationToken token)
        {
            int countImageMain = 1;
            await GenerateImage(outputFolder, imageModelIds, countImageMain, statement, options, token);
        }

        public async Task ProcessCommandExecute(string projectPath
                                               , string projectName
                                               , List<Statement> statements
                                               , string negativePrompt
                                               , string globalPrompt
                                               , string[] imageModelIds
                                               , TtsVoice selectedVoice
                                               , bool portraitEnabled
                                               , TTSToVideoOptions options
                                               , CancellationToken token)
        {

            try
            {
                Directory.CreateDirectory(projectPath);

                foreach (var s in statements)
                {
                    s.GlobalPrompt = globalPrompt;
                    s.NegativePrompt = negativePrompt;
                }

                //Assign the total of all iterations to progress bar Total
                progressBar.Total = statements.Count * 3; //3 because we have to generate image, voice and video

                if (portraitEnabled && statements.Count > 0)
                {
                    statements[0].IsProtrait = portraitEnabled;
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

                //Getting voices
                #region Processing Voices

                //Getting statement voices
                int ca = 1;
                foreach (var statement in statements)
                {
                    progressBar.Increment();
                    progressBar.ShowMessage($"Getting voices {ca}");

                    var audioFileName = $"{statement.Prompt[..Math.Min(statement.Prompt.Length, Constants.MAX_PATH)]}";
                    audioFileName = Path.Combine(projectPath, $"v-{PathHelper.CleanFileName(audioFileName)}.wav");
                    statement.VoiceAudioPath = audioFileName;

                    await GetVoice(new TtsVoice
                    {
                        ModelId = "eleven_multilingual_v2",//selectedVoice.ModelId,
                        Id = selectedVoice.Id
                    }, statement, token);
                }

                //Concatenating Voices
                progressBar.ShowMessage("Concatenating voices");

                var tempVoiceFileA = $"{Path.GetTempFileName()}.wav";
                var tempVoiceFileB = $"{Path.GetTempFileName()}.wav";

                var previosVoiceAudioPath = "";

                //First audio
                previosVoiceAudioPath = statements.First().VoiceAudioPathWave;
                var silenceAudioTemp = $"{Path.GetTempFileName()}.wav";

                if (options.DurationBetweenVideo != null && options.DurationBetweenVideo.Value.TotalSeconds != 0)
                {
                    await AudioHelper.CreateSilentWavAudio(silenceAudioTemp, (options.DurationBetweenVideo ?? new TimeSpan()), token);//AudioHelper.AudioFormat.WAV, token);
                }

                //The rest of the other audios
                foreach (var s in statements.Skip(1))
                {
                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion cancelled by User");
                    }

                    if (silenceAudioTemp != null && File.Exists(silenceAudioTemp))
                    {
                        AudioHelper.ConcatenateAudioFiles(tempVoiceFileA, [previosVoiceAudioPath, silenceAudioTemp, s.VoiceAudioPathWave]);
                    }
                    else
                    {
                        AudioHelper.ConcatenateAudioFiles(tempVoiceFileA, [previosVoiceAudioPath, s.VoiceAudioPathWave]);
                    }

                    File.Copy(tempVoiceFileA, tempVoiceFileB, true);
                    previosVoiceAudioPath = tempVoiceFileB;
                }

                var concatenatedVoicesPath = Path.Combine(projectPath, $"voices-concatenated.wav");
                File.Copy(previosVoiceAudioPath, concatenatedVoicesPath, true);

                RemoveTempFile(tempVoiceFileA);
                RemoveTempFile(tempVoiceFileB);

                if (silenceAudioTemp != null)
                {
                    RemoveTempFile(silenceAudioTemp);
                }
                #endregion

                //Taking Picture
                #region Processing Pictures
                int numImages = 1;
                int countImageMain = 0;
                var firstStatement = statements.First();
                foreach (var statement in statements)
                {
                    progressBar.Increment();
                    progressBar.ShowMessage($"Getting Picture {countImageMain}");

                    countImageMain++;

                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion Cancelled by User");
                    }

                    bool notExistsOneImage = false;
                    for (int i = 0; i < numImages; i++)
                    {
                        var imageFileName = $"{statement.Prompt[..Math.Min(statement.Prompt.Length, Constants.MAX_PATH)]}";
                        imageFileName = Path.Combine(projectPath, $"{PathHelper.CleanFileName(imageFileName)}.jpg");

                        if (!File.Exists(imageFileName))
                        {
                            notExistsOneImage = true;
                        }

                        statement.Images.Clear();
                        statement.Images.Add(new StatementImage
                        {
                            Path = imageFileName
                        });
                    }

                    if (options.ImageOptions.UseOnlyFirstImage && statement != firstStatement)
                    {
                        statement.Images.Clear();
                        statement.Images.Add(new StatementImage
                        {
                            Path = statements.First().Images[0].Path
                        });
                    }
                    else if (notExistsOneImage)
                    {
                        await GenerateImage(projectPath, imageModelIds, countImageMain, statement, options, token);
                    }
                }
                #endregion

                //Creating Video (If It is enabled)
                #region Generating Video
                if (options.ImageOptions.CreateVideo)
                {
                    foreach (var statement in statements)
                    {
                        progressBar.Increment();
                        progressBar.ShowMessage($"Generating Video {countImageMain}");
                        if (token.IsCancellationRequested)
                        {
                            throw new CustomApplicationException("Operantion Cancelled by User");
                        }

                        var videoPath = $"{statement.Images[0].Path}.mp4";
                        if (options.ImageOptions.UseOnlyFirstImage && statement != firstStatement)
                        {
                            statement.VideoPath = firstStatement.VideoPath;
                        }
                        else if (statement == firstStatement)
                        {
                            if (!File.Exists(videoPath))
                            {
                                await this.GeneratePortraitVideoCommandExecute(statement.Images[0].Path, videoPath);
                            }
                            statement.VideoPath = videoPath;
                        }
                    }
                }
                #endregion

                //Making Music 
                #region Processing Audio
                progressBar.ShowMessage($"Making Music Audio.");

                Random random = new();
                var audioWavs = Directory.GetFiles(options.MusicDir, "*.wav");
                int randomNumber = random.Next(1, audioWavs.Length);
                var audioFilePath = audioWavs[randomNumber - 1];

                TimeSpan desiredDuration = new();  // Adjust this value for desired audio duration
                foreach (var s in statements)
                {
                    desiredDuration += s.AudioDuration + (options.DurationBetweenVideo ?? new TimeSpan());

                    if (s.IsProtrait)
                    {
                        desiredDuration -= options.DurationBetweenVideo ?? new TimeSpan();
                    }
                }

                desiredDuration += (options.DurationEndVideo ?? new TimeSpan());

                var audioFileReal = AudioHelper.OpenAudio(audioFilePath);
                double cut = audioFileReal.TotalTime.TotalSeconds - (options.DurationEndVideo?.TotalSeconds ?? 0);

                var tempAudioFileA = $"{Path.GetTempFileName()}.wav";
                File.Copy(audioFilePath, tempAudioFileA, true);

                var tempAudioFileB = $"{Path.GetTempFileName()}.wav";
                File.Copy(audioFilePath, tempAudioFileB, true);

                var outputMusicFile = Path.Combine(projectPath, "output-music.wav");

                var tempAudioFileC = $"{Path.GetTempFileName()}.wav";
                for (double s = 0; s < desiredDuration.TotalSeconds; s += audioFileReal.TotalTime.TotalSeconds)
                {
                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion Cancelled by User");
                    }

                    AudioHelper.ConcatenateAudioFiles(tempAudioFileC, [tempAudioFileA, tempAudioFileB]);
                    File.Copy(tempAudioFileC, tempAudioFileA, true);
                }

                using var a = AudioHelper.OpenAudio(tempAudioFileC);
                cut = Math.Min(a.TotalTime.TotalSeconds, desiredDuration.TotalSeconds);
                await a.DisposeAsync();

                AudioHelper.CutAudio(tempAudioFileC, tempAudioFileA, cut);

                AudioHelper.DecreaseVolumeAtSpecificTime( tempAudioFileA
                                            , tempAudioFileB
                                            , TimeSpan.FromSeconds(0)//(cut - 10)
                                            , AudioHelper.GetAudioDuration(concatenatedVoicesPath)
                                            , -20.0f);

                File.Copy(tempAudioFileB, outputMusicFile, true);

                RemoveTempFile(tempAudioFileA);
                RemoveTempFile(tempAudioFileB);
                RemoveTempFile(tempAudioFileC);
                #endregion

                //Making the Video
                #region Making the Video
                progressBar.ShowMessage($"Making Video.");

                var finalProjectVideoPath = projectPath + "\\" + $"{projectName}.mp4";
                if (File.Exists(finalProjectVideoPath))
                {
                    File.Delete(finalProjectVideoPath);
                }

                var previousVideo = "";
                string outputPath = "";
                string tempFile = "";

                List<Statement> statementsToIterate = statements;

                await FFMPEGHelpers.CreateVideoWithSubtitle(
                    finalProjectVideoPath,
                    firstStatement.Prompt,
                    firstStatement.VideoPath != null && File.Exists(firstStatement.VideoPath) ? firstStatement.VideoPath : firstStatement?.Images.FirstOrDefault()?.Path,
                    firstStatement.AudioDuration,
                     new()
                     {
                         FontStyle = new FfmpegFontStyle
                         {
                             Alignment = firstStatement?.FontStyle?.Alignment ?? FfmpegAlignment.TopCenter,
                             FontSize = 11
                         },
                         MarginEndDuration = options.DurationBetweenVideo
                     },
                     token);

                tempFile = $"{Path.GetTempFileName()}.mp4";
                File.Copy(finalProjectVideoPath, tempFile, true);
                previousVideo = tempFile;

                statementsToIterate = statements.Skip(1).ToList();

                foreach (var s in statementsToIterate)
                {
                    progressBar.Increment();
                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion Cancelled by User");
                    }

                    // Set the output file path
                    outputPath = s.VoiceAudioPath + ".mp4";

                    await FFMPEGHelpers.CreateVideoWithSubtitle(
                        outputPath,
                        s.Prompt,
                        File.Exists(s.VideoPath) ? s.VideoPath : s.Images.First().Path,
                        s.AudioDuration,
                        new()
                        {
                            HeightResolution = FFMPEGDefinitions.HeightResolution,
                            WidthResolution = FFMPEGDefinitions.WidthResolution,
                            FontStyle = new()
                            {
                                Alignment = s?.FontStyle?.Alignment ?? FfmpegAlignment.TopCenter,
                                FontSize = 11,
                            },
                            MarginEndDuration = options.DurationBetweenVideo
                        }, token);

                    await FFMPEGHelpers.JoiningVideos(previousVideo, outputPath, finalProjectVideoPath, new()
                    {
                        HeightResolution = FFMPEGDefinitions.HeightResolution,
                        WidthResolution = FFMPEGDefinitions.WidthResolution,
                    }
                        , token);

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

                var lastImage = File.Exists(lastStatement.VideoPath) 
                                ? lastStatement.VideoPath 
                                : lastStatement.Images.First().Path;
                outputPath = $"{lastStatement.VoiceAudioPath}-no-text.mp4";

                //Run ffmpeg process
                await FFMPEGHelpers.GenerateVideoWithImage(outputPath, lastImage, options.DurationEndVideo, token);

                //Adding last image
                await FFMPEGHelpers.JoiningVideos(previousVideo, outputPath, finalProjectVideoPath, new FfmpegOptions
                {
                    HeightResolution = FFMPEGDefinitions.HeightResolution,
                    WidthResolution = FFMPEGDefinitions.WidthResolution,
                }, token);

                //Addin music sound 
                var finalProjectVideoPathWithAudio = projectPath + "\\" + $"{projectName}-Music-Final.mp4";
                await FFMPEGHelpers.MixAudioWithVideo(finalProjectVideoPath
                                           , outputMusicFile
                                           , finalProjectVideoPathWithAudio
                                           , token);

                var FinalProjectVideoPathWithVoice = projectPath + "\\" + $"{projectName}-Final.mp4";

                await FFMPEGHelpers.MixAudioWithVideo(finalProjectVideoPathWithAudio
                                           , concatenatedVoicesPath
                                           , FinalProjectVideoPathWithVoice
                                           , token);

                RemoveTempFile(previousVideo);

                #endregion
            }
            finally
            {
                if (statements != null)
                {
                    //Cleaning
                    foreach (var s in statements)
                    {
                        if (s.ImageId != null)
                        {
                            await imageGeneratorAI.Remove(new ResultGenerate
                            {
                                Id = s.ImageId
                            });
                        }
                    }
                }
                progressBar.ShowMessage("Process Finished.");
            }
        }

        private async Task GetVoice(TtsVoice ttsVoice, Statement statement, CancellationToken token)
        {
            if (File.Exists(statement.VoiceAudioPath))
            {
                WaveStream file = AudioHelper.OpenAudio(statement.VoiceAudioPath);
                statement.AudioDuration = file.TotalTime;
                statement.IsNewAudio = false;
            }
            else
            {
                var audio = await tts.Convert(new TtsConvertOption
                {
                    Text = statement.Prompt,
                    Voice = ttsVoice
                }, token);

                var buffer = audio.File.GetBuffer();

                File.WriteAllBytes(statement.VoiceAudioPath, buffer);

                using var audioFile1 = AudioHelper.OpenAudio(statement.VoiceAudioPath);
                statement.AudioDuration = audioFile1.TotalTime;
                statement.IsNewAudio = true;
            }

            statement.VoiceAudioPathWave = statement.VoiceAudioPath + ".wav";
            if (!File.Exists(statement.VoiceAudioPathWave) || statement.IsNewAudio)
            {
                AudioHelper.ConvertMp3ToWav(statement.VoiceAudioPath, statement.VoiceAudioPathWave);
            }
        }


        private async Task GenerateImage(string projectPath,
                                         string[] imageModelId,
                                          int countImageMain,
                                          Statement statement,
                                          TTSToVideoOptions options,
                                          CancellationToken token)
        {

            Random r = new();
            int rn = r.Next(1, imageModelId.Length + 1);

            var imageId = await imageGeneratorAI.Generate(new OptionsImageGenerator
            {
                Width = FFMPEGDefinitions.WidthResolution,//512, //832,
                Height = FFMPEGDefinitions.HeightResolution,//904, //1472,
                ModelId = imageModelId[rn - 1],
                NumImages = 1,
                Prompt = statement.GlobalPrompt + (string.IsNullOrEmpty(statement.GlobalPrompt) || options.ImageOptions.UseTextForPrompt ? "" : ",")
                        + (options.ImageOptions.UseTextForPrompt ? statement.Prompt : ""),
                NegativePrompt = statement.NegativePrompt
            });
            statement.ImageId = imageId.Id;

            ResultImagesGenerated response;
            do
            {
                response = await imageGeneratorAI.GetImages(new ResultGenerate { Id = imageId.Id });

                if (token.IsCancellationRequested)
                {
                    throw new CustomApplicationException("Operantion Cancelled by User");
                }

                if (response == null)
                {
                    await Task.Delay(3000, token);
                }

            } while (response == null || response.Images.Count == 0);

            foreach (var image in response.Images)
            {
                var imageFileName = $"{statement.Prompt[..Math.Min(statement.Prompt.Length, Helpers.Constants.MAX_PATH)]}";
                imageFileName = Path.Combine(projectPath, $"{PathHelper.CleanFileName(imageFileName)}.jpg");

                statement.Images.Add(new StatementImage
                {
                    Path = imageFileName,
                });

                File.WriteAllBytes(imageFileName, image.Image);
            }
        }

        private static void RemoveTempFile(string tempFileFinalVideo)
        {
            File.Delete(tempFileFinalVideo);
            string path = tempFileFinalVideo.Replace(".mp4", "")
                          .Replace(".wav", "")
                          .Replace(".mp3", "");
            File.Delete(path);
        }


    }
}
