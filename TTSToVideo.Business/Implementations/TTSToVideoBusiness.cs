using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;
using NAudio.Wave;
using NetXP.Exceptions;
using NetXP.IAs.ImageGeneratorAI;
using NetXP.Tts;
using System.Text.RegularExpressions;
using TTSToVideo.Business.Models;
using TTSToVideo.Business.PatternProcessors;
using TTSToVideo.Helpers;
using TTSToVideo.Helpers.Audios;
using TTSToVideo.Helpers.Implementations.Ffmpeg;
using Constants = TTSToVideo.Helpers.Constants;

namespace TTSToVideo.Business.Implementations
{
    public class TTSToVideoBusiness : ITTSToVideoBusiness
    {
        private readonly IImageGeneratorAI imageGeneratorAI;
        private readonly IVideoGeneratorFactory videoFactory;
        private readonly ITts tts;
        private readonly IProgressBar progressBar;
        private readonly IPromptPatternProcessorFactory patternProcessorFactory;

        public TTSToVideoBusiness(
            IImageGeneratorAI imageGeneratorAI,
            IVideoGeneratorFactory videoFactory,
            ITts tts,
            IProgressBar progressBar,
            IPromptPatternProcessorFactory patternProcessorFactory)
        {
            this.imageGeneratorAI = imageGeneratorAI;
            this.videoFactory = videoFactory;
            this.tts = tts;
            this.progressBar = progressBar;
            this.patternProcessorFactory = patternProcessorFactory;
        }

        public async Task GeneratePortraitVideoCommandExecute(
            VideoGenerationRequest request,
            string outputPath,
            CancellationToken token = default)
        {
            progressBar.ShowMessage($"Generating video ({request.Version}) from \"{Path.GetFileName(request.SourceImagePath)}\"");

            DeleteFileIfExists(outputPath);

            var generator = videoFactory.Resolve(request);
            var video = await generator.GenerateVideoAsync(request, token);

            File.WriteAllBytes(outputPath, video.Video);

            progressBar.ShowMessage($"Video \"{Path.GetFileName(outputPath)}\" created");
        }

        public async Task GeneratePortraitImageCommandExecute(Statement statement, string[] imageModelIds, string projectPath, TTSToVideoOptions options, CancellationToken token)
        {
            await GenerateImage(projectPath, imageModelIds, 1, statement, options, token);
        }

        public async Task<List<Statement>> ProcessCommandExecute(
            string projectPath,
            string projectName,
            string prompt,
            string negativePrompt,
            string globalPrompt,
            string selectedMusicFile,
            string[] imageModelIds,
            TtsVoice selectedVoice,
            bool portraitEnabled,
            TTSToVideoOptions options,
            CancellationToken token)
        {
            List<Statement> statements = [];
            try
            {
                ValidateOptions(options, globalPrompt);
                Directory.CreateDirectory(projectPath);

                statements = ParsePromptIntoStatements(prompt, globalPrompt);
                
                progressBar.Total = statements.Count * 3;
                
                if (portraitEnabled && statements.Count > 0)
                {
                    statements[0].IsProtrait = true;
                }

                await ProcessImages(statements, imageModelIds, projectPath, options, token);
                AssignVideoPathsToStatements(statements, projectPath, options);
                ProcessSubtitles(statements, negativePrompt, globalPrompt, options);
                AddSilenceStatements(statements, options);
                AddFinalSilenceStatement(statements, options);

                var statementsUnion = FlattenStatements(statements);
                var concatenatedVoicesPath = await ProcessVoices(statementsUnion, projectPath, selectedVoice, options, token);

                await MergeVideosAndSubtitles(statementsUnion, projectPath, projectName, options, token);
                await AddMusicAndVoice(projectPath, projectName, selectedMusicFile, concatenatedVoicesPath, statements, options, token);

                return statements;
            }
            finally
            {
                progressBar.ShowMessage("Process Finished.");
            }
        }

        #region Private Helper Methods

        private static void ValidateOptions(TTSToVideoOptions options, string globalPrompt)
        {
            if (!options.ImageOptions.UseTextForPrompt && string.IsNullOrEmpty(globalPrompt))
            {
                throw new CustomApplicationException("If option 'Use paragraph for prompt' is not selected you need to define a 'Additional prompt'");
            }
        }

        private List<Statement> ParsePromptIntoStatements(string prompt, string globalPrompt)
        {
            var statements = new List<Statement>();
            var pattern = string.Join("|", PromptPatternDictionary.Patterns.Values
                .Where(o => o.IsParagraphSeparator)
                .Select(o => o.Pattern));
            
            var paragraphs = Regex.Split(prompt, pattern, RegexOptions.None)
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .ToArray();

            foreach (var paragraph in paragraphs)
            {
                // Try to find a processor for this paragraph
                var processor = patternProcessorFactory.FindProcessorForParagraph(paragraph);
                
                if (processor != null)
                {
                    // Use the processor to handle the paragraph with special patterns
                    processor.Process(paragraph, globalPrompt, statements);
                }
                else
                {
                    // No special pattern detected, treat as regular text
                    statements.Add(new Statement { Prompt = paragraph, GlobalPrompt = globalPrompt });
                }
            }

            return statements;
        }

        private async Task ProcessImages(List<Statement> statements, string[] imageModelIds, string projectPath, TTSToVideoOptions options, CancellationToken token)
        {
            var firstStatement = statements.First();
            
            for (int i = 0; i < statements.Count; i++)
            {
                var statement = statements[i];
                progressBar.Increment();
                progressBar.ShowMessage($"Getting Picture {i}");
                token.ThrowIfCancellationRequested();

                var imageFileName = GetImageFileName(statement, statements, projectPath, i);
                
                if (!File.Exists(imageFileName))
                {
                    if (options.ImageOptions.UseOnlyFirstImage && statement != firstStatement && i > 0)
                    {
                        statement.Images.Add(new StatementImage { Path = statements[i - 1].Images[0].Path });
                    }
                    else
                    {
                        await GenerateImage(projectPath, imageModelIds, i + 1, statement, options, token);
                    }
                }
                else
                {
                    statement.Images.Add(new StatementImage { Path = imageFileName });
                }
            }
        }

        private string GetImageFileName(Statement statement, List<Statement> statements, string projectPath, int currentIndex)
        {
            if (statement.PropmtPatterType == PromptPatternsEnum.SilentVoice)
            {
                var previousIndex = currentIndex - 1;
                if (previousIndex < 0)
                {
                    throw new CustomApplicationException("Silent voice statement cannot be first.");
                }
                return statements[previousIndex].Images.FirstOrDefault()?.Path 
                    ?? throw new CustomApplicationException("Previous statement image path is null.");
            }
            
            return PathHelper.GenerateImagePath(projectPath, statement.Prompt);
        }

        private void AssignVideoPathsToStatements(List<Statement> statements, string projectPath, TTSToVideoOptions options)
        {
            var firstStatement = statements.FirstOrDefault();
            
            for (int i = 0; i < statements.Count; i++)
            {
                var statement = statements[i];
                
                if (statement.PropmtPatterType == PromptPatternsEnum.SilentVoice && i > 0)
                {
                    statement.ImageAnimatedPath = statements[i - 1].ImageAnimatedPath;
                    continue;
                }

                var imageFileName = PathHelper.GenerateImagePath(projectPath, statement.Prompt);
                var videoPath = $"{imageFileName}.mp4";
                
                if (File.Exists(videoPath))
                {
                    statement.ImageAnimatedPath = videoPath;
                }
                else if (options.ImageOptions.UseOnlyFirstImage && statement != firstStatement && i > 0 
                    && !string.IsNullOrEmpty(statements[i - 1].ImageAnimatedPath))
                {
                    statement.ImageAnimatedPath = statements[i - 1].ImageAnimatedPath;
                }
            }
        }

        private void ProcessSubtitles(List<Statement> statements, string negativePrompt, string globalPrompt, TTSToVideoOptions options)
        {
            foreach (var statement in statements)
            {
                if (statement.PropmtPatterType != PromptPatternsEnum.SilentVoice)
                {
                    statement.Id = HashMD5.GenerateHash(statement.Prompt);
                }

                statement.GlobalPrompt = globalPrompt;
                statement.NegativePrompt = negativePrompt;

                ApplyFontStyle(statement, options);
                SplitIntoSubStatements(statement, options);
            }
        }

        private void ApplyFontStyle(Statement statement, TTSToVideoOptions options)
        {
            var statementOption = options.StatementOptions.FirstOrDefault(o => o.Id == statement.Id);
            if (statementOption != null)
            {
                statement.FontStyle = statementOption.FontStyle;
                statement.FontStyle.FontSize ??= options.SubtitleOptions.SubtitleSize;
                statement.FontStyle.MarginV ??= options.SubtitleOptions.MarginV;
                statement.FontStyle.Alignment ??= FfmpegAlignment.TopCenter;
            }
        }

        private void SplitIntoSubStatements(Statement statement, TTSToVideoOptions options)
        {
            var fontSize = statement.FontStyle?.FontSize ?? options.SubtitleOptions.SubtitleSize;
            var maxChars = SubtitleHelper.CalculateMaxChars(
                FFMPEGDefinitions.WidthResolution,
                fontSize,
                statement.FontStyle?.MarginL ?? 0,
                statement.FontStyle?.MarginR ?? 0);
            
            var chunks = SubtitleHelper.SplitByMaxChars(statement.Prompt, maxChars);

            if (chunks.Count > 1)
            {
                statement.SubStatements = [];
                foreach (var chunk in chunks.Where(c => !string.IsNullOrEmpty(c)))
                {
                    statement.SubStatements.Add(CreateSubStatement(statement, chunk));
                    statement.HasSubstatements = true;
                }

                if (options.DurationBetweenVideo?.TotalSeconds > 0)
                {
                    statement.SubStatements.Add(CreateSilentSubStatement(statement, options.DurationBetweenVideo.Value));
                    statement.HasSubstatements = true;
                }
            }
            else
            {
                statement.IsSubtitle = false;
                statement.HasSubstatements = false;
            }
        }

        private Statement CreateSubStatement(Statement parent, string chunk)
        {
            return new Statement
            {
                Id = HashMD5.GenerateHash(parent.Id + "-" + chunk),
                Prompt = chunk,
                NegativePrompt = parent.NegativePrompt,
                GlobalPrompt = parent.GlobalPrompt,
                FontStyle = parent.FontStyle,
                PropmtPatterType = parent.PropmtPatterType,
                IsProtrait = parent.IsProtrait,
                Images = parent.Images,
                ImageAnimatedPath = parent.ImageAnimatedPath,
                ParentId = parent.Id,
                IsSubtitle = true,
                IsTheLastSubtitle = false
            };
        }

        private Statement CreateSilentSubStatement(Statement parent, TimeSpan duration)
        {
            return new Statement
            {
                PropmtPatterType = PromptPatternsEnum.SilentVoice,
                AudioDuration = duration,
                Id = HashMD5.GenerateHash(parent.Id + "-silent"),
                ParentId = parent.Id,
                IsSubtitle = true,
                IsTheLastSubtitle = true,
                Images = parent.Images,
                ImageAnimatedPath = parent.ImageAnimatedPath,
            };
        }

        private void AddSilenceStatements(List<Statement> statements, TTSToVideoOptions options)
        {
            for (int i = 0; i < statements.Count; i++)
            {
                var statement = statements[i];
                if (!statement.HasSubstatements)
                {
                    var silenceStatement = new Statement
                    {
                        PropmtPatterType = PromptPatternsEnum.SilentVoice,
                        AudioDuration = options.DurationBetweenVideo ?? TimeSpan.Zero,
                        Id = HashMD5.GenerateHash(statement.Id + "-silent"),
                        ParentId = statement.Id,
                        IsSubtitle = true,
                        IsTheLastSubtitle = true,
                        Images = statement.Images,
                        ImageAnimatedPath = statement.ImageAnimatedPath
                    };
                    statements.Insert(i + 1, silenceStatement);
                    i++;
                }
            }
        }

        private void AddFinalSilenceStatement(List<Statement> statements, TTSToVideoOptions options)
        {
            var lastStatement = statements.LastOrDefault(o => o.PropmtPatterType != PromptPatternsEnum.SilentVoice);
            if (lastStatement == null) return;

            var lastImage = lastStatement.Images?.FirstOrDefault()?.Path;
            statements.Add(new Statement
            {
                PropmtPatterType = PromptPatternsEnum.SilentVoice,
                AudioDuration = options.DurationEndVideo ?? TimeSpan.Zero,
                Id = HashMD5.GenerateHash("last-silent"),
                Images = lastStatement.Images,
                ImageAnimatedPath = lastStatement.ImageAnimatedPath,
                OutputVideoPath = lastImage == null ? lastStatement.ImageAnimatedPath : $"{lastImage}-last-video-part.mp4"
            });
        }

        private List<Statement> FlattenStatements(List<Statement> statements) => [.. statements.SelectMany<Statement, Statement>(s => (s.SubStatements != null && s.SubStatements.Count > 0) ? s.SubStatements : new[] { s })];

        private async Task MergeVideosAndSubtitles(List<Statement> statementsUnion, string projectPath, string projectName, TTSToVideoOptions options, CancellationToken token)
        {
            var finalProjectVideoPath = Path.Combine(projectPath, $"final-{projectName}.mp4");
            DeleteFileIfExists(finalProjectVideoPath);

            progressBar.ShowMessage("Creating Videos.");

            await CreateIntermediateVideos(statementsUnion, projectPath, token);
            
            progressBar.ShowMessage("Merging Videos.");
            await JoinVideos(statementsUnion, finalProjectVideoPath, token);
            
            await InjectSubtitles(statementsUnion, finalProjectVideoPath, options, token);
        }

        private async Task CreateIntermediateVideos(List<Statement> statementsUnion, string projectPath, CancellationToken token)
        {
            var groupedByImage = statementsUnion
                .GroupBy(o => new
                {
                    ImagePath = o.Images.FirstOrDefault()?.Path,
                    AnimatedPath = o.ImageAnimatedPath ?? string.Empty
                })
                .ToList();

            foreach (var group in groupedByImage)
            {
                var first = group.First();
                var duration = TimeSpan.FromMilliseconds(group.Sum(o => o.AudioDuration.TotalMilliseconds));

                var outputVideoPath = GenerateIntermediateVideoPath(first, statementsUnion, projectPath);
                first.OutputVideoPath = outputVideoPath;

                var sourcePath = File.Exists(first.ImageAnimatedPath) 
                    ? first.ImageAnimatedPath 
                    : first.Images.First().Path;

                await FFMPEGHelpers.CreateVideo(first.OutputVideoPath, sourcePath, duration, token);
            }
        }

        private string GenerateIntermediateVideoPath(Statement statement, List<Statement> statementsUnion, string projectPath)
        {
            var promptPath = string.IsNullOrEmpty(statement.Prompt) 
                ? $"video.{statementsUnion.IndexOf(statement)}" 
                : statement.Prompt;
            
            var truncatedPath = promptPath[..Math.Min(promptPath.Length, Constants.MAX_PATH)];
            Directory.CreateDirectory(Path.Combine(projectPath, "Intermediate"));
            
            var fileName = PathHelper.CleanFileName(truncatedPath);
            var suffix = statement.PropmtPatterType == PromptPatternsEnum.SilentVoice 
                ? $".{statementsUnion.IndexOf(statement)}" 
                : "";
            
            return Path.Combine(projectPath, "Intermediate", $"{fileName}{suffix}.mp4");
        }

        private async Task JoinVideos(List<Statement> statementsUnion, string finalProjectVideoPath, CancellationToken token)
        {
            var groupedByImage = statementsUnion
                .GroupBy(o => new
                {
                    ImagePath = o.Images.FirstOrDefault()?.Path,
                    AnimatedPath = o.ImageAnimatedPath ?? string.Empty
                });

            var videoPaths = groupedByImage.Select(o => o.First().OutputVideoPath).ToArray();

            await FFMPEGHelpers.JoiningVideos(videoPaths, finalProjectVideoPath, new FfmpegOptions
            {
                HeightResolution = FFMPEGDefinitions.HeightResolution,
                WidthResolution = FFMPEGDefinitions.WidthResolution,
            }, token);
        }

        private async Task InjectSubtitles(List<Statement> statementsUnion, string finalProjectVideoPath, TTSToVideoOptions options, CancellationToken token)
        {
            var subtitleSegments = statementsUnion
                .Select(o => new FFMPEGHelpers.AssSubtitleSegment(o.AudioDuration, o.Prompt, o.FontStyle))
                .ToList();

            var subtitleFile = FFMPEGHelpers.CreateAssSubtitleFile(subtitleSegments);
            var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            File.Copy(finalProjectVideoPath, tempFile);

            await FFMPEGHelpers.InjectSubtitlesAsync(tempFile, subtitleFile, finalProjectVideoPath, false);
        }

        private async Task AddMusicAndVoice(string projectPath, string projectName, string selectedMusicFile, string concatenatedVoicesPath, List<Statement> statements, TTSToVideoOptions options, CancellationToken token)
        {
            progressBar.ShowMessage("Processing Music.");
            
            var finalProjectVideoPath = Path.Combine(projectPath, $"final-{projectName}.mp4");
            var audioFile = AudioHelper.OpenAudio(finalProjectVideoPath);
            var outputMusicFile = await ProcessBackgroundMusic(selectedMusicFile, statements, audioFile.TotalTime, options, projectPath, token);

            progressBar.ShowMessage("Merging Music to the Video.");
            var finalProjectVideoPathWithAudio = Path.Combine(projectPath, $"{projectName}-Music-Final.mp4");
            await FFMPEGHelpers.MixAudioWithVideo(finalProjectVideoPath, outputMusicFile, finalProjectVideoPathWithAudio, token);

            progressBar.ShowMessage("Merging Voice to the Video.");
            var finalProjectVideoPathWithVoice = Path.Combine(projectPath, $"{projectName}-Final.mp4");
            await FFMPEGHelpers.MixAudioWithVideo(finalProjectVideoPathWithAudio, concatenatedVoicesPath, finalProjectVideoPathWithVoice, token);
        }

        private async Task GetVoice(TtsVoice ttsVoice, Statement statement, CancellationToken token)
        {
            ArgumentException.ThrowIfNullOrEmpty(statement.AudioPath);

            if (File.Exists(statement.AudioPath))
            {
                using var file = AudioHelper.OpenAudio(statement.AudioPath);
                statement.AudioDuration = file.TotalTime;
                statement.IsNewAudio = false;
            }
            else
            {
                var audio = await tts.Convert(new TtsConvertOption
                {
                    Text = statement.Prompt,
                    Voice = ttsVoice,
                }, token);

                File.WriteAllBytes(statement.AudioPath, audio.File.GetBuffer());

                using var audioFile = AudioHelper.OpenAudio(statement.AudioPath);
                statement.AudioDuration = audioFile.TotalTime;
                statement.IsNewAudio = true;
            }

            statement.AudioPathWave = $"{statement.AudioPath}.wav";
            if (!File.Exists(statement.AudioPathWave) || statement.IsNewAudio)
            {
                AudioHelper.ConvertMp3ToWav(statement.AudioPath, statement.AudioPathWave);
            }
        }

        private async Task GenerateImage(string projectPath, string[] imageModelIds, int countImageMain, Statement statement, TTSToVideoOptions options, CancellationToken token)
        {
            var random = new Random();
            var selectedModelId = imageModelIds[random.Next(imageModelIds.Length)];

            var prompt = BuildImagePrompt(statement, options);

            var imageId = await imageGeneratorAI.Generate(new OptionsImageGenerator
            {
                Width = FFMPEGDefinitions.WidthResolution,
                Height = FFMPEGDefinitions.HeightResolution,
                ModelId = selectedModelId,
                NumImages = 1,
                Prompt = prompt,
                NegativePrompt = statement.NegativePrompt
            });
            
            statement.ImageId = imageId.Id;

            var response = await WaitForImageGeneration(imageId.Id, token);

            foreach (var image in response.Images)
            {
                var imageFileName = PathHelper.GenerateImagePath(projectPath, statement.Prompt);

                statement.Images.Add(new StatementImage
                {
                    Path = imageFileName,
                    Id = image.Id
                });

                File.WriteAllBytes(imageFileName, image.Image);
            }
        }

        private string BuildImagePrompt(Statement statement, TTSToVideoOptions options)
        {
            var prompt = statement.GlobalPrompt;
            
            if (!string.IsNullOrEmpty(prompt) && options.ImageOptions.UseTextForPrompt)
            {
                prompt += ",";
            }
            
            if (options.ImageOptions.UseTextForPrompt)
            {
                prompt += statement.Prompt;
            }
            
            return prompt;
        }

        private async Task<ResultImagesGenerated> WaitForImageGeneration(string imageId, CancellationToken token)
        {
            ResultImagesGenerated response;
            do
            {
                token.ThrowIfCancellationRequested();
                
                response = await imageGeneratorAI.GetImages(new ResultGenerate { Id = imageId });

                if (response == null || response.Images.Count == 0)
                {
                    await Task.Delay(3000, token);
                }
            } 
            while (response == null || response.Images.Count == 0);

            return response;
        }

        private async Task<string> ProcessVoices(List<Statement> statements, string projectPath, TtsVoice selectedVoice, TTSToVideoOptions options, CancellationToken token)
        {
            await GenerateVoices(statements, projectPath, selectedVoice, token);
            return await ConcatenateVoices(statements, projectPath, options, token);
        }

        private async Task GenerateVoices(List<Statement> statements, string projectPath, TtsVoice selectedVoice, CancellationToken token)
        {
            for (int i = 0; i < statements.Count; i++)
            {
                var statement = statements[i];
                progressBar.Increment();
                progressBar.ShowMessage($"Getting voices {i + 1}");

                if (statement.PropmtPatterType == PromptPatternsEnum.SilentVoice)
                {
                    CreateSilentAudio(statement, projectPath, token);
                }
                else if (!string.IsNullOrEmpty(statement.Prompt))
                {
                    var audioFileName = GenerateAudioFileName(statement.Prompt, projectPath);
                    statement.AudioPath = audioFileName;

                    await GetVoice(new TtsVoice
                    {
                        ModelId = "eleven_v3",
                        Id = selectedVoice.Id
                    }, statement, token);
                }
                else
                {
                    throw new CustomApplicationException("Statement has no prompt and is not a silent voice.");
                }
            }
        }

        private void CreateSilentAudio(Statement statement, string projectPath, CancellationToken token)
        {
            var silencePath = Path.Combine(projectPath, $"silencevoice_{statement.AudioDuration.TotalSeconds}.wav");
            AudioHelper.CreateSilentWavAudio(silencePath, statement.AudioDuration, token);

            statement.AudioPath = silencePath;
            statement.AudioPathWave = $"{statement.AudioPath}.wav";
            File.Copy(statement.AudioPath, statement.AudioPathWave, true);
        }

        private string GenerateAudioFileName(string prompt, string projectPath)
        {
            var truncated = prompt[..Math.Min(prompt.Length, Constants.MAX_PATH)];
            var cleanFileName = PathHelper.CleanFileName(truncated);
            return Path.Combine(projectPath, $"v-{cleanFileName}.wav");
        }

        private async Task<string> ConcatenateVoices(List<Statement> statements, string projectPath, TTSToVideoOptions options, CancellationToken token)
        {
            progressBar.ShowMessage("Concatenating voices");

            var tempVoiceFileA = $"{Path.GetTempFileName()}.wav";
            var tempVoiceFileB = $"{Path.GetTempFileName()}.wav";
            
            var currentAudioPath = statements.First().AudioPathWave;

            foreach (var statement in statements.Skip(1))
            {
                token.ThrowIfCancellationRequested();

                AudioHelper.ConcatenateAudioFiles(tempVoiceFileA, new[] { currentAudioPath, statement.AudioPathWave });
                File.Copy(tempVoiceFileA, tempVoiceFileB, true);
                currentAudioPath = tempVoiceFileB;
            }

            var concatenatedVoicesPath = Path.Combine(projectPath, "voices-concatenated.wav");
            File.Copy(currentAudioPath, concatenatedVoicesPath, true);

            RemoveTempFile(tempVoiceFileA);
            RemoveTempFile(tempVoiceFileB);

            return concatenatedVoicesPath;
        }

        private async Task<string> ProcessBackgroundMusic(string selectedMusicFile, List<Statement> statements, TimeSpan totalDuration, TTSToVideoOptions options, string projectPath, CancellationToken token)
        {
            progressBar.ShowMessage("Making Background Music Audio.");

            using var audioFileReal = AudioHelper.OpenAudio(selectedMusicFile);
            
            var tempAudioFileA = $"{Path.GetTempFileName()}.wav";
            var tempAudioFileB = $"{Path.GetTempFileName()}.wav";
            var tempAudioFileC = $"{Path.GetTempFileName()}.wav";
            
            File.Copy(selectedMusicFile, tempAudioFileA, true);
            File.Copy(selectedMusicFile, tempAudioFileB, true);

            await LoopMusicToMatchDuration(tempAudioFileA, tempAudioFileB, tempAudioFileC, audioFileReal.TotalTime, totalDuration, token);

            var cut = await TrimMusicToDuration(tempAudioFileC, tempAudioFileA, totalDuration);

            AudioHelper.DecreaseVolumeAtSpecificTime(
                tempAudioFileA,
                tempAudioFileB,
                TimeSpan.Zero,
                totalDuration,
                (float)options.MusicaOptions.MusicVolume);

            var outputMusicFile = Path.Combine(projectPath, "output-music.wav");
            File.Copy(tempAudioFileB, outputMusicFile, true);

            RemoveTempFile(tempAudioFileA);
            RemoveTempFile(tempAudioFileB);
            RemoveTempFile(tempAudioFileC);

            return outputMusicFile;
        }

        private async Task LoopMusicToMatchDuration(string tempA, string tempB, string tempC, TimeSpan musicDuration, TimeSpan totalDuration, CancellationToken token)
        {
            for (double seconds = 0; seconds < totalDuration.TotalSeconds; seconds += musicDuration.TotalSeconds)
            {
                token.ThrowIfCancellationRequested();
                AudioHelper.ConcatenateAudioFiles(tempC, new[] { tempA, tempB });
                File.Copy(tempC, tempA, true);
            }
        }

        private async Task<double> TrimMusicToDuration(string sourceFile, string outputFile, TimeSpan totalDuration)
        {
            using var audio = AudioHelper.OpenAudio(sourceFile);
            var cut = Math.Min(audio.TotalTime.TotalSeconds, totalDuration.TotalSeconds);
            await audio.DisposeAsync();

            AudioHelper.CutAudio(sourceFile, outputFile, cut);
            return cut;
        }

        private static void DeleteFileIfExists(string path)
        {
            if (!File.Exists(path)) return;

            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                throw new CustomApplicationException("File in use cannot be removed", ex);
            }

            if (File.Exists(path))
            {
                throw new CustomApplicationException("File in use cannot be removed");
            }
        }

        private static void RemoveTempFile(string pathToRemove)
        {
            if (File.Exists(pathToRemove))
            {
                File.Delete(pathToRemove);
            }

            var basePath = pathToRemove
                .Replace(".mp4", "")
                .Replace(".wav", "")
                .Replace(".mp3", "");
            
            if (File.Exists(basePath))
            {
                File.Delete(basePath);
            }
        }

        #endregion
    }
}
