using Microsoft.VisualBasic;
using NAudio.Wave;
using NetXP.Exceptions;
using NetXP.ImageGeneratorAI;
using NetXP.Tts;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TTSToVideo.Business.Models;
using TTSToVideo.Helpers;
using TTSToVideo.Helpers.Audios;
using TTSToVideo.Helpers.Implementations.Ffmpeg;
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

        public async Task<List<Statement>> ProcessCommandExecute(string projectPath
                                               , string projectName
                                               , string prompt
                                               , string negativePrompt
                                               , string globalPrompt
                                               , string selectedMusicFile
                                               , string[] imageModelIds
                                               , TtsVoice selectedVoice
                                               , bool portraitEnabled
                                               , TTSToVideoOptions options
                                               , CancellationToken token)
        {

            List<Statement>? statements = [];
            try
            {
                if (!options.ImageOptions.UseTextForPrompt && string.IsNullOrEmpty(globalPrompt))
                {
                    throw new CustomApplicationException("If option 'Use paragraph for prompt' is not selected you need to define a 'Additional prompt'");
                }

                Directory.CreateDirectory(projectPath);


                var pattern = string.Join("|", PromptPatternDictionary.Patterns.Values.Where(o => o.IsParagraphSeparator).Select(o => o.Pattern));
                string[] paragraphs = Regex.Split(prompt, pattern, RegexOptions.None);
                paragraphs = paragraphs.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();

                var firstParagraph = paragraphs.First();
                var tempStatements = new List<string>();

                foreach (var paragraph in paragraphs)
                {
                    Statement? statement = null;

                    //Search pattern and split
                    var patterns = PromptPatternDictionary.Patterns.Values.Where(o => !o.IsParagraphSeparator);
                    bool hasPatter = false;
                    foreach (var p in patterns)
                    {
                        if (PromptPatternsEnum.SilentVoice == p.TypeRegex && Regex.IsMatch(paragraph, p.Pattern))
                        {
                            hasPatter = true;
                            var matchesSplit = Regex.Split(paragraph, p.Pattern).Where(ms => !string.IsNullOrWhiteSpace(ms)).ToArray();
                            foreach (var ms in matchesSplit)
                            {
                                if (Regex.IsMatch(ms, p.Pattern))
                                {
                                    var msSplitResult = ms.Split(":", StringSplitOptions.TrimEntries).ToList();
                                    if (msSplitResult.Count != 2)
                                    {
                                        throw new CustomApplicationException($"Format of \"{ms}\" incorrect in prompt.");
                                    }

                                    var seconds = msSplitResult[1].Replace(">", "");
                                    //converting seconds to integer
                                    if (!int.TryParse(seconds, out int secondsInt))
                                    {
                                        throw new CustomApplicationException($"Format of \"{ms}\" incorrect in prompt, seconds part should be integer.");
                                    }

                                    //Validating max of seconds to 600
                                    if (secondsInt > 600)
                                    {
                                        throw new CustomApplicationException($"Format of \"{ms}\" incorrect in prompt, seconds part should be less than 600.");
                                    }

                                    statements.Add(new Statement
                                    {
                                        PropmtPatterType = PromptPatternsEnum.SilentVoice,
                                        AudioDuration = TimeSpan.FromSeconds(secondsInt)
                                    });
                                }
                                else
                                {
                                    statements.Add(new Statement { Prompt = ms });
                                }
                            }
                        }
                    }

                    if (!hasPatter)
                    {
                        statements.Add(new Statement { Prompt = paragraph });
                    }
                };

                //Adding options to statements
                foreach (var s in statements)
                {
                    var statementOption = options.StatementOptions.FirstOrDefault(o => o.Index == statements.IndexOf(s));
                    s.GlobalPrompt = globalPrompt;
                    s.NegativePrompt = negativePrompt;
                    if (statementOption != null)
                    {
                        s.FontStyle = statementOption.FontStyle;
                    }
                }

                //Assign the total of all iterations to progress bar Total
                progressBar.Total = statements.Count * 3; //3 because we have to generate image, voice and video

                if (portraitEnabled && statements.Count > 0)
                {
                    statements[0].IsProtrait = portraitEnabled;
                }

                //Getting voices
                #region Processing Voices

                //Getting statement voices
                int ca = 1;
                foreach (var statement in statements)
                {
                    progressBar.Increment();
                    progressBar.ShowMessage($"Getting voices {ca}");

                    if (statement.PropmtPatterType == PromptPatternsEnum.SilentVoice)
                    {
                        var silenceAudioPatternTemp = Path.Combine(projectPath, $"silencevoice_{statement.AudioDuration.TotalSeconds}.wav");
                        AudioHelper.CreateSilentWavAudio(silenceAudioPatternTemp, statement.AudioDuration, token);
                        statement.VoiceAudioPath = silenceAudioPatternTemp;
                        statement.VoiceAudioPathWave = statement.VoiceAudioPath + ".wav";
                        File.Copy(statement.VoiceAudioPath, statement.VoiceAudioPathWave, true);
                    }
                    else
                    {
                        var audioFileName = $"{statement.Prompt[..Math.Min(statement.Prompt.Length, Constants.MAX_PATH)]}";
                        audioFileName = Path.Combine(projectPath, $"v-{PathHelper.CleanFileName(audioFileName)}.wav");
                        statement.VoiceAudioPath = audioFileName;

                        await GetVoice(new TtsVoice
                        {
                            ModelId = "eleven_multilingual_v2",//selectedVoice.ModelId,
                            Id = selectedVoice.Id
                        }, statement, token);
                    }
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
                    AudioHelper.CreateSilentWavAudio(silenceAudioTemp, (options.DurationBetweenVideo ?? new TimeSpan()), token);
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
                RemoveTempFile(silenceAudioTemp);
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
                        string imageFileName;
                        if (statement.PropmtPatterType == PromptPatternsEnum.SilentVoice)
                        {
                            //Get previous statement 
                            var previousStatementIndex = statements.IndexOf(statement) - 1;
                            Statement previousStatement;
                            if (previousStatementIndex >= 0 && previousStatementIndex < statements.Count)
                            {
                                previousStatement = statements[previousStatementIndex];
                                imageFileName = previousStatement.Images.FirstOrDefault()?.Path ?? throw new CustomApplicationException("Previous statement image path is null.");
                            }
                            else
                            {
                                throw new CustomApplicationException("Previous statement index is out of range.");
                            }
                            imageFileName = previousStatement.Images.FirstOrDefault().Path;
                        }
                        else
                        {
                            imageFileName = $"{statement.Prompt[..Math.Min(statement.Prompt.Length, Constants.MAX_PATH)]}";
                            imageFileName = Path.Combine(projectPath, $"{PathHelper.CleanFileName(imageFileName)}.jpg");
                        }

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

                //Making Background Music 
                #region Processing Background Music
                progressBar.ShowMessage($"Making Background Music Audio.");

                var audioFilePath = selectedMusicFile; 

                TimeSpan desiredDuration = new();  // Adjust this value for desired audio duration
                foreach (var s in statements)
                {
                    desiredDuration += s.AudioDuration + (options.DurationBetweenVideo ?? new TimeSpan());

                    //if (s.IsProtrait)
                    //{
                    //    desiredDuration -= options.DurationBetweenVideo ?? new TimeSpan();
                    //}
                }

                desiredDuration += ((options.DurationEndVideo ?? new TimeSpan()) + (options.DurationBetweenVideo ?? new TimeSpan()));

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

                AudioHelper.DecreaseVolumeAtSpecificTime(tempAudioFileA
                                            , tempAudioFileB
                                            , TimeSpan.FromSeconds(0)
                                            , desiredDuration
                                            , (float)options.MusicaOptions.MusicVolume);

                File.Copy(tempAudioFileB, outputMusicFile, true);

                RemoveTempFile(tempAudioFileA);
                RemoveTempFile(tempAudioFileB);
                RemoveTempFile(tempAudioFileC);
                #endregion

                //Making the Video
                #region Making the Video

                //Adding last image


                var silenceVoice = $"{Path.GetTempFileName()}.wav";
                AudioHelper.CreateSilentWavAudio(silenceAudioTemp, (options.DurationBetweenVideo ?? new TimeSpan()), token);

                var lastStatement = statements.Last();
                var additionalLastStatement = new Statement
                {
                    AudioDuration = options.DurationEndVideo.Value,
                    Images = [new StatementImage { Path = $"{lastStatement.Images.First().Path}" }],
                    VideoPath = lastStatement.VideoPath,
                    VoiceAudioPath = $"{lastStatement.Images.First().Path}-last-video-part.mp4",
                };
                statements.Add(additionalLastStatement);

                progressBar.ShowMessage($"Making Video.");

                var finalProjectVideoPath = projectPath + "\\" + $"{projectName}.mp4";
                if (File.Exists(finalProjectVideoPath))
                {
                    File.Delete(finalProjectVideoPath);
                }

                string outputPath = "";
                foreach (var s in statements)
                {
                    progressBar.Increment();
                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion Cancelled by User");
                    }

                    // Set the output file path
                    outputPath = s.VoiceAudioPath + ".mp4";

                    await FFMPEGHelpers.CreateVideoWithSubtitle
                    (
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
                                FontSize = s?.FontStyle?.FontSize ?? 11,
                            },
                            MarginEndDuration = options.DurationBetweenVideo
                        }, token);
                }

                //Joining videos
                var videoPaths = statements.Select(o => o.VoiceAudioPath + ".mp4").ToList();
                await FFMPEGHelpers.JoiningVideos([.. videoPaths], finalProjectVideoPath, new()
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

                #endregion

                return statements;
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

        private static void RemoveTempFile(string pathToRemove)
        {
            File.Delete(pathToRemove);
            string path = pathToRemove.Replace(".mp4", "")
                          .Replace(".wav", "")
                          .Replace(".mp3", "");
            File.Delete(path);
        }


    }
}
