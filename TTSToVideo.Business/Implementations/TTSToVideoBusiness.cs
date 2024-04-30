using Microsoft.VisualBasic;
using NAudio.Wave;
using NetXP.Exceptions;
using NetXP.ImageGeneratorAI;
using NetXP.TTS;
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
                                  , ITTS tts
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

        public async Task GeneratePortraitImageCommandExecute(Statement statement, string outputFolder, CancellationToken token)
        {
            int countImageMain = 1;

            await GenerateImage(outputFolder, countImageMain, statement, token);
        }


        public async Task ProcessCommandExecute(string projectPath
                                               , string projectName
                                               , string text
                                               , string negativePrompt
                                               , bool portraitEnable
                                               , string portraitText
                                               , string portraitVoice
                                               , string portraitImagePath
                                               , string portraitVideoPath
                                               , string musicDir
                                               , CancellationToken token)
        {
            List<Statement> statements = null;

            try
            {
                Directory.CreateDirectory(projectPath);

                //Split text process text with dot and paragraph
                string[] paragraphs = text.Split(new string[] { Environment.NewLine + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                statements = paragraphs.Select(o => new Statement { Prompt = o }).ToList();

                Statement? statementPortraitVoice = null;
                if (portraitEnable)
                {
                    if (portraitImagePath == null)
                    {

                        //Map StatementModel to Statement
                        statementPortraitVoice = new Statement
                        {
                            Prompt = portraitText,
                            NegativePrompt = negativePrompt,
                        };

                        await this.GeneratePortraitImageCommandExecute(statementPortraitVoice, projectPath, token);
                    }

                    statementPortraitVoice = new Statement()
                    {
                        Images = [
                            new()
                            {
                                Path = Path.Combine(projectPath, portraitImagePath
                                                                     ?? throw new CustomApplicationException("PortraitImagePath empty"))
                            }
                        ],
                        Prompt = (string.IsNullOrEmpty(portraitVoice) ? portraitText : portraitVoice) ??
                                  throw new CustomApplicationException("PortraitText or PortraitVoice empty"),

                        AudioPath = Path.Combine(projectPath, "portrait-voice.wav")
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
                var ttsVoices = await tts.GetTTSVoices("", token);
                ttsVoices = ttsVoices.Where(o => o.Tags.Contains("meditation")).ToList();
                Random rv = new();
                int rvn = rv.Next(1, ttsVoices.Count + 1);
                var ttsVoice = ttsVoices[rvn - 1];

                bool anyNewFile = false;
                //Getting portrait voice
                if (portraitEnable && statementPortraitVoice != null)
                {
                    anyNewFile = await GetVoice(ttsVoice, statementPortraitVoice, token);
                }

                //Getting statement voices
                int ca = 1;
                foreach (var statement in statements)
                {
                    progressBar.ShowMessage($"Getting voices {ca}");

                    var audioFileName = $"{statement.Prompt[..Math.Min(statement.Prompt.Length, Constants.MAX_PATH)]}";
                    audioFileName = Path.Combine(projectPath, $"{ca++}.-{CleanFileName(audioFileName)}.wav");
                    statement.AudioPath = audioFileName;

                    anyNewFile = await GetVoice(ttsVoice, statement, token);
                }

                //Concating voices 
                progressBar.ShowMessage("Concatenating voices");

                var tempVoiceFileA = $"{Path.GetTempFileName()}.mp4";
                var tempVoiceFileB = $"{Path.GetTempFileName()}.mp4";

                var previous = "";

                //Concatenating Portrait with the first audio
                if (portraitEnable)
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

                var bigAudioPath = Path.Combine(projectPath, $"Voices Concatenated.mp4");
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

                    progressBar.ShowMessage($"Getting Pictures {countImageMain}");

                    if (token.IsCancellationRequested)
                    {
                        throw new CustomApplicationException("Operantion Cancelled by User");
                    }

                    bool notExistsOneImage = false;
                    for (int i = 0; i < numImages; i++)
                    {
                        var imageFileName = $"{statement.Prompt[..Math.Min(statement.Prompt.Length, Constants.MAX_PATH)]}";
                        imageFileName = Path.Combine(projectPath, $"{countImageMain}.{i + 1}.-{CleanFileName(imageFileName)}.jpg");

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
                        await GenerateImage(projectPath, countImageMain, statement, token);
                    }
                }
                #endregion

                //Making Audio
                #region Processing Audio
                progressBar.ShowMessage($"Making Audio.");

                Random random = new();
                var audioWavs = Directory.GetFiles(musicDir, "*.wav");
                int randomNumber = random.Next(1, audioWavs.Length);
                var audioFilePath = audioWavs[randomNumber - 1];

                TimeSpan desiredDuration = new();  // Adjust this value for desired audio duration
                foreach (var s in statements)
                {
                    desiredDuration += s.AudioDuration;
                }

                if (portraitEnable)
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
                    }
                };

                List<Statement> statementsToIterate = statements;
                if (portraitEnable && statementPortraitVoice != null)
                {
                    if (statementPortraitVoice.Images.Count == 0)
                    {
                        throw new CustomApplicationException("Portrait image not generated.");
                    }

                    outputPath = statementPortraitVoice.AudioPath + ".mp4";
                    if (File.Exists(portraitVideoPath))
                    {
                        await FFMPEGHelpers.CreateVideoWithSubtitle(
                            outputPath,
                            statementPortraitVoice.Prompt,
                            portraitVideoPath,
                            statementPortraitVoice.AudioDuration,
                            ffmpegOptions,
                            token);
                    }
                    else
                    {
                        await FFMPEGHelpers.CreateVideoWithSubtitle(
                            outputPath,
                            statementPortraitVoice.Prompt,
                            statementPortraitVoice.Images.First().Path,
                            statementPortraitVoice.AudioDuration,
                            ffmpegOptions,
                            token);

                    }

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
                    await FFMPEGHelpers.CreateVideoWithSubtitle(outputPath, s.Prompt, s.Images.First().Path, s.AudioDuration, ffmpegOptions, token);

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

                var finalProjectVideoPathWithAudio = projectPath + "\\" + $"{projectName}-Music-Final.mp4";
                await FFMPEGHelpers.MixAudioWithVideo(finalProjectVideoPath
                                           , outputMusicFile
                                           , finalProjectVideoPathWithAudio
                , token);

                var FinalProjectVideoPathWithVoice = projectPath + "\\" + $"{projectName}-Final.mp4";

                await FFMPEGHelpers.MixAudioWithVideo(finalProjectVideoPathWithAudio
                                           , bigAudioPath
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
                    Text = statement.Prompt,
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


        private async Task GenerateImage(string projectPath,
                                          int countImageMain,
                                          Statement statement,
                                          CancellationToken token)
        {
            string[] modelsIds = new string[] {
                     //"e316348f-7773-490e-adcd-46757c738eb7", //Abosulte Reality v1.6 
                       "ac614f96-1082-45bf-be9d-757f2d31c174" //DreamShaper v7
                };
            Random r = new();
            int rn = r.Next(1, modelsIds.Length + 1);

            var imageId = await imageGeneratorAI.Generate(new OptionsImageGenerator
            {
                Width = FFMPEGDefinitions.WidthResolution,//512, //832,
                Height = FFMPEGDefinitions.HeightResolution,//904, //1472,
                ModelId = modelsIds[rn - 1],
                NumImages = 1,
                Prompt = statement.Prompt,
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
                imageFileName = Path.Combine(projectPath, $"{countImageMain}.{ci++}.-{CleanFileName(imageFileName)}.jpg");

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

    }
}
