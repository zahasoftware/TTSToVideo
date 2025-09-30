using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;
using NAudio.Wave;
using NetXP.Exceptions;
using NetXP.IAs.ImageGeneratorAI;
using NetXP.Tts;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TTSToVideo.Business.Models;
using TTSToVideo.Helpers;
using TTSToVideo.Helpers.Audios;
using TTSToVideo.Helpers.Implementations.Ffmpeg;
using static System.Net.Mime.MediaTypeNames;
using Constants = TTSToVideo.Helpers.Constants;

namespace TTSToVideo.Business.Implementations
{
    public class TTSToVideoBusiness : ITTSToVideoBusiness
    {
        private readonly IImageGeneratorAI imageGeneratorAI;
        private readonly IVideoGeneratorFactory videoFactory;
        private readonly ITts tts;
        private readonly IProgressBar progressBar;

        public TTSToVideoBusiness(
            IImageGeneratorAI imageGeneratorAI,
            IVideoGeneratorFactory videoFactory,
            ITts tts,
            IProgressBar progressBar)
        {
            this.imageGeneratorAI = imageGeneratorAI;
            this.videoFactory = videoFactory;
            this.tts = tts;
            this.progressBar = progressBar;
        }

        public async Task GeneratePortraitVideoCommandExecute(
            VideoGenerationRequest request,
            string outputPath,
            CancellationToken token = default)
        {
            progressBar.ShowMessage($"Generating video ({request.Version}) from \"{Path.GetFileName(request.SourceImagePath)}\"");

            var generator = videoFactory.Resolve(request);
            var video = await generator.GenerateVideoAsync(request, token);

            File.WriteAllBytes(outputPath, video.Video);
            progressBar.ShowMessage($"Video \"{Path.GetFileName(outputPath)}\" created");
        }

        public async Task GeneratePortraitImageCommandExecute(Statement statement, string[] imageModelIds, string projectPath, TTSToVideoOptions options, CancellationToken token)
        {
            int countImageMain = 1;
            await GenerateImage(projectPath, imageModelIds, countImageMain, statement, options, token);
        }

        public async Task<List<Statement>> ProcessCommandExecute(
                                                 string projectPath
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
                paragraphs = [.. paragraphs.Where(o => !string.IsNullOrWhiteSpace(o))];

                var firstParagraph = paragraphs.First();
                var tempStatements = new List<string>();

                foreach (var paragraph in paragraphs)
                {
                    //Search pattern and split
                    var patterns = PromptPatternDictionary.Patterns.Values.Where(o => !o.IsParagraphSeparator);
                    bool hasPatter = false;
                    foreach (var p in patterns)
                    {
                        //Check silenece voice pattern is in the paragraph
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
                                        AudioDuration = TimeSpan.FromSeconds(secondsInt),
                                        GlobalPrompt = globalPrompt,
                                    });
                                }
                                else
                                {
                                    statements.Add(new Statement { Prompt = ms, GlobalPrompt = globalPrompt });
                                }
                            }
                        }
                    }

                    if (!hasPatter)
                    {
                        statements.Add(new Statement { Prompt = paragraph, GlobalPrompt = globalPrompt });
                    }
                }


                //Assign the total of all iterations to progress bar Total
                progressBar.Total = statements.Count * 3; //3 because we have to generate image, voice and video

                if (portraitEnabled && statements.Count > 0)
                {
                    statements[0].IsProtrait = portraitEnabled;
                }

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
                            Statement previousStatementImage;
                            if (previousStatementIndex >= 0 && previousStatementIndex < statements.Count)
                            {
                                previousStatementImage = statements[previousStatementIndex];
                                imageFileName = previousStatementImage.Images.FirstOrDefault()?.Path ?? throw new CustomApplicationException("Previous statement image path is null.");
                            }
                            else
                            {
                                throw new CustomApplicationException("Previous statement index is out of range.");
                            }
                            imageFileName = previousStatementImage.Images.FirstOrDefault().Path;
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
                        else
                        {
                            statement.Images.Clear();
                            statement.Images.Add(new StatementImage
                            {
                                Path = imageFileName
                            });
                        }
                    }

                    if (options.ImageOptions.UseOnlyFirstImage && statement != firstStatement && notExistsOneImage)
                    {
                        statement.Images.Clear();
                        statement.Images.Add(new StatementImage
                        {
                            Path = statements[statements.IndexOf(statement) - 1].Images[0].Path
                        });
                    }
                    else if (notExistsOneImage)
                    {
                        await GenerateImage(projectPath, imageModelIds, countImageMain, statement, options, token);
                    }
                }

                //Creating Video (If It is enabled)
                countImageMain = 1;
                firstStatement = statements.FirstOrDefault();
                foreach (var statement in statements)
                {
                    if (statement.PropmtPatterType == PromptPatternsEnum.SilentVoice)
                    {
                        statement.ImageAnimatedPath = statements[statements.IndexOf(statement) - 1].ImageAnimatedPath;
                        continue;
                    }

                    var imageFileName = $"{statement.Prompt[..Math.Min(statement.Prompt.Length, Constants.MAX_PATH)]}";
                    imageFileName = Path.Combine(projectPath, $"{PathHelper.CleanFileName(imageFileName)}.jpg");

                    var notExistsOneVideo = !File.Exists($"{imageFileName}.mp4");
                    var videoPath = $"{imageFileName}.mp4";
                    if (Path.Exists(videoPath))
                    {
                        statement.ImageAnimatedPath = videoPath;
                    }

                    if (options.ImageOptions.UseOnlyFirstImage && statement != firstStatement && notExistsOneVideo
                        && !string.IsNullOrEmpty(statements[statements.IndexOf(statement) - 1].ImageAnimatedPath))
                    {
                        statement.Images.Clear();
                        statement.ImageAnimatedPath = statements[statements.IndexOf(statement) - 1].ImageAnimatedPath;
                    }
                    else if (notExistsOneVideo)
                    {
                        //statement.Images.Add(new StatementImage
                        //{
                        //    Path =  firstStatement?.Images.First().Path
                        //});
                    }

                }
                /*
                                                    if (options.ImageOptions.CreateVideo)
                                                    {

                                                        progressBar.Increment();
                                                        progressBar.ShowMessage($"Generating Video {countImageMain}");
                                                        if (token.IsCancellationRequested)
                                                        {
                                                            throw new CustomApplicationException("Operantion Cancelled by User");
                                                        }


                                                        if (options.ImageOptions.UseOnlyFirstImage && statement != firstStatement)
                                                        {
                                                            statement.ImageAnimatedPath = firstStatement.ImageAnimatedPath;
                                                        }
                                                        else
                                                        {
                                                            if (!File.Exists(videoPath))
                                                            {
                                                                await this.GeneratePortraitVideoCommandExecute(statement.Images[0].Path, videoPath);
                                                            }
                                                            statement.ImageAnimatedPath = videoPath;
                                                        }
                                                    }
                                                }
                                */

                #endregion


                //Making subtitles if it can
                foreach (Statement s in statements)
                {
                    var fontSize = s?.FontStyle?.FontSize ?? options.SubtitleOptions.SubtitleSize;
                    int maxChars = SubtitleHelper.CalculateMaxChars(FFMPEGDefinitions.WidthResolution
                                                                   , fontSize
                                                                   , s?.FontStyle?.MarginL ?? 0
                                                                   , s?.FontStyle?.MarginR ?? 0);
                    var chunks = SubtitleHelper.SplitByMaxChars(s.Prompt, maxChars);

                    if (s.PropmtPatterType != PromptPatternsEnum.SilentVoice)
                        s.Id = HashMD5.GenerateHash(s.Prompt);

                    //Adding style
                    var statementOption = options.StatementOptions.FirstOrDefault(o => o.Id == s.Id);
                    s.GlobalPrompt = globalPrompt;
                    s.NegativePrompt = negativePrompt;
                    if (statementOption != null)
                    {
                        s.FontStyle = statementOption.FontStyle;
                        s.FontStyle.FontSize = statementOption.FontStyle.FontSize == null ? Helpers.Constants.SUBTITTLE_SIZE_DEFAULT : statementOption.FontStyle.FontSize;
                        s.FontStyle.Alignment ??= FfmpegAlignment.TopCenter;
                    }

                    if (chunks.Count > 1)
                    {
                        s.SubStatements = [];
                        foreach (var c in chunks)
                        {
                            if (!string.IsNullOrEmpty(c))
                            {
                                s.SubStatements.Add(
                                new Statement
                                {
                                    Id = HashMD5.GenerateHash(s.Id + "-" + c),
                                    Prompt = c,
                                    NegativePrompt = s.NegativePrompt,
                                    GlobalPrompt = s.GlobalPrompt,
                                    FontStyle = s.FontStyle,
                                    PropmtPatterType = s.PropmtPatterType,
                                    IsProtrait = s.IsProtrait,
                                    Images = s.Images,
                                    ImageAnimatedPath = s.ImageAnimatedPath,
                                    ParentId = s.Id,
                                    IsSubtitle = true,
                                    IsTheLastSubtitle = false
                                });
                                s.HasSubstatements = true;
                            }
                        }

                        if (options.DurationBetweenVideo != null && options.DurationBetweenVideo.Value.TotalSeconds > 0)
                        {
                            //add a new silent statement
                            s.SubStatements.Add(new Statement
                            {
                                PropmtPatterType = PromptPatternsEnum.SilentVoice,
                                AudioDuration = options.DurationBetweenVideo ?? TimeSpan.FromSeconds(0),
                                Id = HashMD5.GenerateHash(s.Id + "-silent"),
                                ParentId = s.Id,
                                IsSubtitle = true,
                                IsTheLastSubtitle = true,
                                Images = s.Images,
                                ImageAnimatedPath = s.ImageAnimatedPath,
                            });
                            s.HasSubstatements = true;
                        }
                    }
                    else
                    {
                        s.IsSubtitle = false;
                        s.HasSubstatements = false;
                        if (s.Images == null && string.IsNullOrEmpty(s.ImageAnimatedPath))
                        { 
                        }
                    }

                }

                // Insert a silence statement after sessions that don't have s.SubStatements
                for (int i = 0; i < statements.Count; i++)
                {
                    var s = statements[i];
                    // Only insert if there are no substatements or substatements is empty
                    if (!s.HasSubstatements)
                    {
                        var silenceStatement = new Statement
                        {
                            PropmtPatterType = PromptPatternsEnum.SilentVoice,
                            AudioDuration = options.DurationBetweenVideo ?? TimeSpan.FromSeconds(0),
                            Id = HashMD5.GenerateHash(s.Id + "-silent"),
                            ParentId = s.Id,
                            IsSubtitle = true,
                            IsTheLastSubtitle = true,
                            Images = s.Images,
                            ImageAnimatedPath = s.ImageAnimatedPath
                        };
                        statements.Insert(i + 1, silenceStatement);
                        i++; // Skip the inserted silence statement
                    }
                }

                //add the last silent statement
                var lastStatement = statements.Last(o => o.PropmtPatterType != PromptPatternsEnum.SilentVoice);
                var lastImage = lastStatement?.Images?.FirstOrDefault()?.Path;
                statements.Add(new Statement
                {
                    PropmtPatterType = PromptPatternsEnum.SilentVoice,
                    AudioDuration = options.DurationEndVideo ?? TimeSpan.FromSeconds(0),
                    Id = HashMD5.GenerateHash("last-silent"),
                    Images = lastStatement.Images,
                    ImageAnimatedPath = lastStatement.ImageAnimatedPath,
                    OutputVideoPath = lastImage == null ? lastStatement.ImageAnimatedPath : $"{lastImage}-last-video-part.mp4"
                });

                var statementsUnion = statements.SelectMany(s => s.SubStatements?.Count > 0 ? s.SubStatements : [s]).ToList();


                //Getting voices (It also get the duration of each video)
                var concatenatedVoicesPath = await this.ProcessVoices(statementsUnion, projectPath, selectedVoice, options, token);

                //Mergin videos
                #region Merging the Video

                var finalProjectVideoPath = projectPath + "\\" + $"final-{projectName}.mp4";
                if (File.Exists(finalProjectVideoPath))
                {
                    File.Delete(finalProjectVideoPath);
                }

                progressBar.ShowMessage($"Creating Videos.");

                var groupedByImage = statementsUnion
                    .GroupBy(o => new
                    {
                        ImagePath = o.Images.FirstOrDefault()?.Path,
                        AnimatedPath = o.ImageAnimatedPath ?? string.Empty
                    })
                    .ToList();

                foreach (var sg in groupedByImage)
                {
                    var first = sg.First();
                    var totalNanoseconds = sg.Sum(o => o.AudioDuration.TotalMilliseconds);
                    var duration = TimeSpan.FromMilliseconds(totalNanoseconds);

                    // Set the output file path (SilenceVoice enum is set because not all silence voice have the same seconds
                    var promptPath = (string.IsNullOrEmpty(first.Prompt) ? $"video.{statementsUnion.IndexOf(first)}" : first.Prompt);
                    var outputVideoPath = $"{promptPath[..Math.Min(promptPath.Length, Constants.MAX_PATH)]}";
                    outputVideoPath = Path.Combine(projectPath, $"{PathHelper.CleanFileName(outputVideoPath)}");

                    first.OutputVideoPath = outputVideoPath + (first.PropmtPatterType == PromptPatternsEnum.SilentVoice ? $".{statementsUnion.IndexOf(first)}" : "") + ".mp4";

                    await FFMPEGHelpers.CreateVideo
                    (
                        first.OutputVideoPath,
                        File.Exists(first.ImageAnimatedPath) ? first.ImageAnimatedPath : first.Images.First().Path,
                        duration,
                        token
                    );
                }

                //Joining Videos
                progressBar.ShowMessage($"Merging Videos.");
                var videoPaths = groupedByImage.Select(o => o.First().OutputVideoPath);

                await FFMPEGHelpers.JoiningVideos([.. videoPaths], finalProjectVideoPath, new FfmpegOptions()
                {
                    HeightResolution = FFMPEGDefinitions.HeightResolution,
                    WidthResolution = FFMPEGDefinitions.WidthResolution,
                }, token);

                //Get subtitle file
                var subtitleSegments = statementsUnion.Select(o => new FFMPEGHelpers.AssSubtitleSegment(
                    o.AudioDuration,
                    o.Prompt,
                    o.FontStyle)
                ).ToList();

                var subtitleFile = FFMPEGHelpers.CreateAssSubtitleFile(subtitleSegments);
                //, new FfmpegOptions()
                //{
                //    HeightResolution = FFMPEGDefinitions.HeightResolution,
                //    WidthResolution = FFMPEGDefinitions.WidthResolution
                //});

                var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                File.Copy(finalProjectVideoPath, tempFile);

                var optionStyle = statementsUnion.LastOrDefault(o => o.FontStyle != null);
                var ffmpegOptions = new FfmpegOptions()
                {
                    HeightResolution = FFMPEGDefinitions.HeightResolution,
                    WidthResolution = FFMPEGDefinitions.WidthResolution,
                    FontStyle = new()
                    {
                        Alignment = optionStyle?.FontStyle?.Alignment ?? FfmpegAlignment.TopCenter,
                        FontSize = optionStyle?.FontStyle?.FontSize ?? options.SubtitleOptions.SubtitleSize,
                        SubtitleVisible = optionStyle?.FontStyle?.SubtitleVisible ?? false
                    },
                };

                await FFMPEGHelpers.InjectSubtitlesAsync(tempFile, subtitleFile, finalProjectVideoPath, false);

                #endregion

                progressBar.ShowMessage($"Procesing Music.");
                //Get total duration of finalProjectVideoPath
                var audioFile = AudioHelper.OpenAudio(finalProjectVideoPath);
                var outputMusicFile = await ProcessBackgroundMusic(selectedMusicFile, statements, audioFile.TotalTime, options, projectPath, token);

                //Adding music sound 
                progressBar.ShowMessage($"Merging Music to the Video.");
                var finalProjectVideoPathWithAudio = projectPath + "\\" + $"{projectName}-Music-Final.mp4";
                await FFMPEGHelpers.MixAudioWithVideo(finalProjectVideoPath
                                           , outputMusicFile
                                           , finalProjectVideoPathWithAudio
                                           , token);

                var FinalProjectVideoPathWithVoice = projectPath + "\\" + $"{projectName}-Final.mp4";

                progressBar.ShowMessage($"Merging Voice to the Video.");
                await FFMPEGHelpers.MixAudioWithVideo(finalProjectVideoPathWithAudio
                                           , concatenatedVoicesPath
                                           , FinalProjectVideoPathWithVoice, token);


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
            ArgumentException.ThrowIfNullOrEmpty(statement.AudioPath);

            if (File.Exists(statement.AudioPath))
            {
                WaveStream file = AudioHelper.OpenAudio(statement.AudioPath);
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

                File.WriteAllBytes(statement.AudioPath, buffer);

                using var audioFile1 = AudioHelper.OpenAudio(statement.AudioPath);
                statement.AudioDuration = audioFile1.TotalTime;
                statement.IsNewAudio = true;
            }

            statement.AudioPathWave = statement.AudioPath + ".wav";
            if (!File.Exists(statement.AudioPathWave) || statement.IsNewAudio)
            {
                AudioHelper.ConvertMp3ToWav(statement.AudioPath, statement.AudioPathWave);
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

        private async Task<string> ProcessVoices(List<Statement> statements, string projectPath, TtsVoice selectedVoice, TTSToVideoOptions options, CancellationToken token)
        {
            // Getting statement voices
            int ca = 1;
            foreach (var statement in statements)
            {
                progressBar.Increment();
                progressBar.ShowMessage($"Getting voices {ca}");

                if (statement.PropmtPatterType == PromptPatternsEnum.SilentVoice)
                {
                    var silenceAudioPatternTemp = Path.Combine(projectPath, $"silencevoice_{statement.AudioDuration.TotalSeconds}.wav");
                    AudioHelper.CreateSilentWavAudio(silenceAudioPatternTemp, statement.AudioDuration, token);

                    statement.AudioPath = silenceAudioPatternTemp;
                    statement.AudioPathWave = statement.AudioPath + ".wav";
                    File.Copy(statement.AudioPath, statement.AudioPathWave, true);
                }
                else if (!string.IsNullOrEmpty(statement.Prompt))
                {
                    var audioFileName = $"{statement.Prompt[..Math.Min(statement.Prompt.Length, Constants.MAX_PATH)]}";
                    audioFileName = Path.Combine(projectPath, $"v-{PathHelper.CleanFileName(audioFileName)}.wav");
                    statement.AudioPath = audioFileName;

                    await GetVoice(new TtsVoice
                    {
                        ModelId = "eleven_multilingual_v2", // selectedVoice.ModelId,
                        Id = selectedVoice.Id
                    }
                    , statement
                    , token);
                }
                else
                {
                    throw new Exception();
                }
            }

            // Concatenating Voices
            progressBar.ShowMessage("Concatenating voices");

            var tempVoiceFileA = $"{Path.GetTempFileName()}.wav";
            var tempVoiceFileB = $"{Path.GetTempFileName()}.wav";
            var previosVoiceAudioPath = "";

            // First audio
            Statement previousStatement = statements.First();
            previosVoiceAudioPath = previousStatement.AudioPathWave;

            var silenceAudioTemp = $"{Path.GetTempFileName()}.wav";

            if (options.DurationBetweenVideo != null && options.DurationBetweenVideo.Value.TotalSeconds != 0)
            {
                AudioHelper.CreateSilentWavAudio(silenceAudioTemp, (options.DurationBetweenVideo ?? new TimeSpan()), token);
            }

            // The rest of the other audios
            foreach (var s in statements.Skip(1))
            {
                if (token.IsCancellationRequested)
                {
                    throw new CustomApplicationException("Operation cancelled by User");
                }

                AudioHelper.ConcatenateAudioFiles(tempVoiceFileA, [previosVoiceAudioPath, s.AudioPathWave]);

                File.Copy(tempVoiceFileA, tempVoiceFileB, true);


                previosVoiceAudioPath = tempVoiceFileB;
                previousStatement = s;
            }

            var concatenatedVoicesPath = Path.Combine(projectPath, $"voices-concatenated.wav");
            File.Copy(previosVoiceAudioPath, concatenatedVoicesPath, true);

            RemoveTempFile(tempVoiceFileA);
            RemoveTempFile(tempVoiceFileB);
            RemoveTempFile(silenceAudioTemp);

            return concatenatedVoicesPath;
        }

        //Making Background Music 
        private async Task<string> ProcessBackgroundMusic(string selectedMusicFile, List<Statement> statements, TimeSpan totalDuration, TTSToVideoOptions options, string projectPath, CancellationToken token)
        {
            progressBar.ShowMessage($"Making Background Music Audio.");

            var audioFilePath = selectedMusicFile;


            var audioFileReal = AudioHelper.OpenAudio(audioFilePath);
            double cut = audioFileReal.TotalTime.TotalSeconds - (options.DurationEndVideo?.TotalSeconds ?? 0);

            var tempAudioFileA = $"{Path.GetTempFileName()}.wav";
            File.Copy(audioFilePath, tempAudioFileA, true);

            var tempAudioFileB = $"{Path.GetTempFileName()}.wav";
            File.Copy(audioFilePath, tempAudioFileB, true);

            var outputMusicFile = Path.Combine(projectPath, "output-music.wav");

            var tempAudioFileC = $"{Path.GetTempFileName()}.wav";
            for (double s = 0; s < totalDuration.TotalSeconds; s += audioFileReal.TotalTime.TotalSeconds)
            {
                if (token.IsCancellationRequested)
                {
                    throw new CustomApplicationException("Operation Cancelled by User");
                }

                AudioHelper.ConcatenateAudioFiles(tempAudioFileC, new[] { tempAudioFileA, tempAudioFileB });
                File.Copy(tempAudioFileC, tempAudioFileA, true);
            }

            using var a = AudioHelper.OpenAudio(tempAudioFileC);
            cut = Math.Min(a.TotalTime.TotalSeconds, totalDuration.TotalSeconds);
            await a.DisposeAsync();

            AudioHelper.CutAudio(tempAudioFileC, tempAudioFileA, cut);

            AudioHelper.DecreaseVolumeAtSpecificTime(tempAudioFileA,
                                                     tempAudioFileB,
                                                     TimeSpan.FromSeconds(0),
                                                     totalDuration,
                                                     (float)options.MusicaOptions.MusicVolume);

            File.Copy(tempAudioFileB, outputMusicFile, true);

            RemoveTempFile(tempAudioFileA);
            RemoveTempFile(tempAudioFileB);
            RemoveTempFile(tempAudioFileC);

            return outputMusicFile;
        }

    }
}
