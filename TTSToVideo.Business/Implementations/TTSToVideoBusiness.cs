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
    public class TTSToVideoBusiness(IImageGeneratorAI imageGeneratorAI
                                      , ITts tts
                                      , IProgressBar progressBar
            ) : ITTSToVideoBusiness
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

        public async Task GeneratePortraitImageCommandExecute(Statement statement, string[] imageModelIds, string outputFolder, CancellationToken token)
        {
            int countImageMain = 1;

            await GenerateImage(outputFolder, imageModelIds, countImageMain, statement, token);
        }


        public async Task ProcessCommandExecute(string projectPath
                                               , string projectName
                                               , string text
                                               , string negativePrompt
                                               , string globalPrompt
                                               , string[] imageModelIds
                                               , TtsVoice selectedVoice
                                               , TTSToVideoPortraitParams portraitParams
                                               , TTSToVideoOptions options
                                               , CancellationToken token)
        {
            List<Statement> statements = null;
            //Assign the total of all iterations to progress bar Total

            try
            {

                Directory.CreateDirectory(projectPath);

                //Split text process text with dot and paragraph
                string[] paragraphs = text.Split(new string[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                statements = paragraphs.Select(o => new Statement
                {
                    Prompt = o,
                    GlobalPrompt = globalPrompt,
                    NegativePrompt = negativePrompt
                }).ToList();

                progressBar.Total = statements.Count * 3; //3 because we have to generate image, voice and video

                Statement? statementPortraitVoice = null;
                if (portraitParams.Enable)
                {
                    if (portraitParams.ImagePath == null)
                    {
                        //Map StatementModel to Statement
                        statementPortraitVoice = new Statement
                        {
                            Prompt = portraitParams.Text,
                            NegativePrompt = negativePrompt,
                        };

                        await this.GeneratePortraitImageCommandExecute(statementPortraitVoice, imageModelIds, projectPath, token);
                    }

                    statementPortraitVoice = new Statement()
                    {
                        Images = [
                            new() {
                                    Path = Path.Combine(projectPath, portraitParams.ImagePath
                                                                     ?? throw new CustomApplicationException("PortraitImagePath empty"))
                                }
                        ],
                        Prompt = (string.IsNullOrEmpty(portraitParams.Voice) ? portraitParams.Text : portraitParams.Voice) ??
                                  throw new CustomApplicationException("PortraitText or PortraitVoice empty"),

                        VoiceAudioPath = Path.Combine(projectPath, "portrait-voice.wav")
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

                //Getting voices
                #region Processing Voices

                //Getting portrait voice
                if (portraitParams.Enable && statementPortraitVoice != null)
                {
                    await GetVoice(new TtsVoice
                    {
                        ModelId = selectedVoice.ModelId,
                        Id = selectedVoice.Id
                    }, statementPortraitVoice, token);
                }

                //Getting statement voices
                int ca = 1;
                foreach (var statement in statements) 
                {
                    progressBar.Increment();
                    progressBar.ShowMessage($"Getting voices {ca}");

                    var audioFileName = $"{statement.Prompt[..Math.Min(statement.Prompt.Length, Constants.MAX_PATH)]}";
                    audioFileName = Path.Combine(projectPath, $"{ca++}.-{PathHelper.CleanFileName(audioFileName)}.wav");
                    statement.VoiceAudioPath = audioFileName;

                    await GetVoice(new TtsVoice
                    {
                        ModelId = selectedVoice.ModelId,
                        Id = selectedVoice.Id
                    }, statement, token);
                }

                //Concatenating Voices
                progressBar.ShowMessage("Concatenating voices");

                var tempVoiceFileA = $"{Path.GetTempFileName()}.wav";
                var tempVoiceFileB = $"{Path.GetTempFileName()}.wav";

                var previosVoiceAudioPath = "";

                //Concatenating Portrait with the first audio
                if (portraitParams.Enable)
                {
                    previosVoiceAudioPath = statementPortraitVoice?.VoiceAudioPathWave;
                }
                else
                {
                    previosVoiceAudioPath = statements.First().VoiceAudioPathWave;
                }

                var isFirstIteration = true;
                var silenceAudioTemp = $"{Path.GetTempFileName()}.wav";

                if (options.DurationBetweenVideo != null && options.DurationBetweenVideo.Value.TotalSeconds != 0)
                {
                    AudioHelper.CreateSilentWavAudio(silenceAudioTemp, (options.DurationBetweenVideo ?? new TimeSpan()), token);//, AudioHelper.AudioFormat.WAV, token);
                }

                foreach (var s in portraitParams.Enable ? statements : statements.Skip(1))
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
                        imageFileName = Path.Combine(projectPath, $"{countImageMain}.{i + 1}.-{PathHelper.CleanFileName(imageFileName)}.jpg");

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
                        await GenerateImage(projectPath, imageModelIds, countImageMain, statement, token);
                    }
                }
                #endregion

                //Making Audio
                #region Processing Audio
                progressBar.ShowMessage($"Making Audio.");

                Random random = new();
                var audioWavs = Directory.GetFiles(options.MusicDir, "*.wav");
                int randomNumber = random.Next(1, audioWavs.Length);
                var audioFilePath = audioWavs[randomNumber - 1];

                TimeSpan desiredDuration = new();  // Adjust this value for desired audio duration
                foreach (var s in statements)
                {
                    desiredDuration += s.AudioDuration + (options.DurationBetweenVideo ?? new TimeSpan());
                }

                if (portraitParams.Enable)
                {
                    desiredDuration += statementPortraitVoice.AudioDuration;
                }

                desiredDuration += (options.DurationEndVideo ?? new TimeSpan());

                var audioFileReal = AudioHelper.OpenAudio(audioFilePath);
                double cut = audioFileReal.TotalTime.TotalSeconds - (options.DurationEndVideo?.TotalSeconds ?? 0);

                var tempAudioFileA = $"{Path.GetTempFileName()}.wav";
                File.Copy(audioFilePath, tempAudioFileA, true);

                var tempAudioFileB = $"{Path.GetTempFileName()}.wav";
                File.Copy(audioFilePath, tempAudioFileB, true);

                var outputMusicFile = "output.wav";

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
                //AudioHelper.DecreaseVolumeAtSpecificTime(tempAudioFileA, tempAudioFileB, TimeSpan.FromSeconds(cut - 10), 0.5f);
                File.Copy(tempAudioFileA, outputMusicFile, true);

                RemoveTempFile(tempAudioFileA);
                RemoveTempFile(tempAudioFileB);
                RemoveTempFile(tempAudioFileC);
                #endregion

                //Making the Video
                #region Making the Video
                progressBar.ShowMessage($"Making Video.");

                var finalProjectVideoPath = projectPath + "\\" + $"{projectName}.mp4";

                var previousVideo = "";
                string outputPath = "";
                string tempFile = "";

                FfmpegOptions ffmpegOptions = new()
                {
                    FontStyle = new FfmpegFontStyle
                    {
                        Alignment = FfmpegAlignment.TopCenter,
                        FontSize = 11
                    },
                    MarginEndDuration = options.DurationBetweenVideo
                };

                FfmpegOptions ffmpegOptionPortrait = new()
                {
                    FontStyle = new FfmpegFontStyle
                    {
                        Alignment = FfmpegAlignment.TopCenter,
                        FontSize = 11
                    },
                    MarginEndDuration = options.DurationBetweenVideo
                };

                List<Statement> statementsToIterate = statements;
                if (portraitParams.Enable && statementPortraitVoice != null)
                {
                    if (statementPortraitVoice.Images.Count == 0)
                    {
                        throw new CustomApplicationException("Portrait image not generated.");
                    }

                    outputPath = statementPortraitVoice.VoiceAudioPath + ".mp4";
                    if (File.Exists(portraitParams.VideoPath))
                    {
                        await FFMPEGHelpers.CreateVideoWithSubtitle(
                            outputPath,
                            statementPortraitVoice.Prompt,
                            portraitParams.VideoPath,
                            statementPortraitVoice.AudioDuration,
                            ffmpegOptionPortrait,
                            token);
                    }
                    else
                    {
                        await FFMPEGHelpers.CreateVideoWithSubtitle(
                            outputPath,
                            statementPortraitVoice.Prompt,
                            statementPortraitVoice.Images.First().Path,
                            statementPortraitVoice.AudioDuration,
                            ffmpegOptionPortrait,
                            token);
                    }

                    tempFile = $"{Path.GetTempFileName()}.mp4";
                    File.Copy(outputPath, tempFile, true);
                    previousVideo = tempFile;
                }
                else
                {
                    var firstStatement = statements.First();
                    outputPath = firstStatement.VoiceAudioPath + ".mp4";
                    await FFMPEGHelpers.CreateVideoWithSubtitle(
                        outputPath,
                        firstStatement.Prompt,
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
                    progressBar.Increment();
                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion Cancelled by User");
                    }

                    // Set the output file path
                    outputPath = s.VoiceAudioPath + ".mp4";
                    ffmpegOptions = new()
                    {
                        HeightResolution = FFMPEGDefinitions.HeightResolution,
                        WidthResolution = FFMPEGDefinitions.WidthResolution,
                        FontStyle = new()
                        {
                            Alignment = FfmpegAlignment.TopCenter,
                            FontSize = 11,
                        },
                        MarginEndDuration = options.DurationBetweenVideo
                    };

                    await FFMPEGHelpers.CreateVideoWithSubtitle(
                        outputPath,
                        s.Prompt,
                        s.Images.First().Path,
                        s.AudioDuration, ffmpegOptions, token);

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
                Prompt = statement.GlobalPrompt + (string.IsNullOrEmpty(statement.GlobalPrompt) ? "" : ",") + statement.Prompt,
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

            var ci = 1;
            foreach (var image in response.Images)
            {
                var imageFileName = $"{statement.Prompt[..Math.Min(statement.Prompt.Length, Helpers.Constants.MAX_PATH)]}";
                imageFileName = Path.Combine(projectPath, $"{countImageMain}.{ci++}.-{PathHelper.CleanFileName(imageFileName)}.jpg");

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
