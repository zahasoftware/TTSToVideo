using Microsoft.VisualBasic;
using Microsoft.VisualBasic.FileIO;
using NAudio.Wave;
using NetXP.Exceptions;
using NetXP.IAs.ImageGeneratorAI;
using NetXP.Tts;
using System.Text.RegularExpressions;
using TTSToVideo.Business.Models;
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

            // Step 1: Split by <p> tags to get blocks
            var blocks = SplitIntoParagraphBlocks(prompt);

            // Step 2: Process each block
            foreach (var block in blocks)
            {
                ProcessParagraphBlock(block, globalPrompt, statements);
            }

            return statements;
        }

        /// <summary>
        /// Splits the input text into paragraph blocks based on <p></p> tags.
        /// Returns a list of (content, imagePrompt) tuples.
        /// </summary>
        private List<(string Content, string? ImagePrompt)> SplitIntoParagraphBlocks(string input)
        {
            var blocks = new List<(string Content, string? ImagePrompt)>();

            // Pattern to match <p>...</p> blocks
            var pBlockPattern = @"<p>(.*?)</p>";
            var matches = Regex.Matches(input, pBlockPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var lastIndex = 0;

            foreach (Match match in matches)
            {
                // Add content before the <p> tag as a block without image prompt
                if (match.Index > lastIndex)
                {
                    var beforeContent = input[lastIndex..match.Index].Trim();
                    if (!string.IsNullOrWhiteSpace(beforeContent))
                    {
                        blocks.Add((beforeContent, null));
                    }
                }

                // Extract content inside <p> tags
                var blockContent = match.Groups[1].Value;

                // Extract image prompt if exists (<ip> or <image-prompt>)
                var imagePrompt = ExtractImagePrompt(ref blockContent);

                // Add the block with its image prompt
                if (!string.IsNullOrWhiteSpace(blockContent))
                {
                    blocks.Add((blockContent.Trim(), imagePrompt));
                }

                lastIndex = match.Index + match.Length;
            }

            // Add remaining content after the last <p> tag
            if (lastIndex < input.Length)
            {
                var remainingContent = input[lastIndex..].Trim();
                if (!string.IsNullOrWhiteSpace(remainingContent))
                {
                    blocks.Add((remainingContent, null));
                }
            }

            // If no <p> tags found, treat the entire input as one block
            if (blocks.Count == 0 && !string.IsNullOrWhiteSpace(input))
            {
                blocks.Add((input.Trim(), null));
            }

            return blocks;
        }

        /// <summary>
        /// Extracts image prompt from content using <ip> or <image-prompt> tags.
        /// Removes the tag from the content and returns the image prompt text.
        /// </summary>
        private string? ExtractImagePrompt(ref string content)
        {
            // Pattern to match <ip>...</ip> or <image-prompt>...</image-prompt>
            var ipPattern = @"<(?:ip|image-prompt)>(.*?)</(?:ip|image-prompt)>";
            var match = Regex.Match(content, ipPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var imagePrompt = match.Groups[1].Value.Trim();

                // Remove the image prompt tag from content
                content = Regex.Replace(content, ipPattern, string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase).Trim();

                return !string.IsNullOrWhiteSpace(imagePrompt) ? imagePrompt : null;
            }

            return null;
        }

        /// <summary>
        /// Processes a paragraph block by splitting it into individual paragraphs
        /// and creating statements with the associated image prompt.
        /// </summary>
        private void ProcessParagraphBlock((string Content, string? ImagePrompt) block, string globalPrompt, List<Statement> statements)
        {
            // Get paragraph separators from dictionary
            var pattern = string.Join("|", PromptPatternDictionary.Patterns.Values
                .Where(o => o.IsParagraphSeparator)
                .Select(o => o.Pattern));

            // Split block content by paragraph separators
            var paragraphs = Regex.Split(block.Content, pattern, RegexOptions.None)
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .ToArray();

            // Process each paragraph
            foreach (var paragraph in paragraphs)
            {
                var trimmedParagraph = paragraph.Trim();

                // Check for silent voice pattern
                var silentVoicePattern = PromptPatternDictionary.Patterns.Values
                    .FirstOrDefault(p => !p.IsParagraphSeparator && p.TypeRegex == PromptPatternsEnum.SilentVoice);

                if (silentVoicePattern != null && Regex.IsMatch(trimmedParagraph, silentVoicePattern.Pattern))
                {
                    ProcessSilentVoicePattern(trimmedParagraph, silentVoicePattern.Pattern, globalPrompt, block.ImagePrompt, statements);
                }
                else
                {
                    // Create statement with image prompt if available
                    statements.Add(new Statement
                    {
                        Prompt = trimmedParagraph,
                        GlobalPrompt = globalPrompt,
                        ImagePrompt = block.ImagePrompt
                    });
                }
            }
        }

        private void ProcessSilentVoicePattern(string paragraph, string pattern, string globalPrompt, string imagePrompt, List<Statement> statements)
        {
            var matches = Regex.Split(paragraph, pattern).Where(ms => !string.IsNullOrWhiteSpace(ms));

            foreach (var match in matches)
            {
                if (Regex.IsMatch(match, pattern))
                {
                    var parts = match.Split(":", StringSplitOptions.TrimEntries);
                    if (parts.Length != 2)
                    {
                        throw new CustomApplicationException($"Format of \"{match}\" incorrect in prompt.");
                    }

                    var seconds = parts[1].Replace(">", "");
                    if (!int.TryParse(seconds, out int secondsInt) || secondsInt > 600)
                    {
                        throw new CustomApplicationException($"Format of \"{match}\" incorrect in prompt. Seconds should be an integer between 0 and 600.");
                    }

                    statements.Add(new Statement
                    {
                        PropmtPatterType = PromptPatternsEnum.SilentVoice,
                        AudioDuration = TimeSpan.FromSeconds(secondsInt),
                        GlobalPrompt = globalPrompt,
                        ImagePrompt = imagePrompt,
                    });
                }
                else
                {
                    statements.Add(new Statement
                    {
                        Prompt = match,
                        GlobalPrompt = globalPrompt,
                        ImagePrompt = imagePrompt,
                    });
                }
            }
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

            var imagePrompt = string.IsNullOrEmpty(statement.ImagePrompt) ? statement.Prompt : statement.ImagePrompt;
            return PathHelper.GenerateImagePath(projectPath, imagePrompt);
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

            progressBar.ShowMessage("Creating Intermediate Videos.");

            await CreateIntermediateVideos(statementsUnion, projectPath, token);

            progressBar.ShowMessage("Merging Intermediate Videos.");
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
                    NextText = statement.nextStatement?.Prompt ?? string.Empty
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
                var imagePrompt = string.IsNullOrEmpty(statement.ImagePrompt) ? statement.Prompt : statement.ImagePrompt;
                var imageFileName = PathHelper.GenerateImagePath(projectPath, imagePrompt);

                statement.Images.Add(new StatementImage
                {
                    Path = imageFileName,
                    Id = image.Id
                });

                File.WriteAllBytes(imageFileName, image.Image);
            }
        }

        private static string BuildImagePrompt(Statement statement, TTSToVideoOptions options)
        {
            if (!string.IsNullOrEmpty(statement.ImagePrompt))
            {
                return statement.ImagePrompt;
            }

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
            int voiceCounter = 0;
            for (int i = 0; i < statements.Count; i++)
            {
                var statement = statements[i];

                statement.nextStatement = (i + 1 < statements.Count) ? statements[i + 1] : null;

                progressBar.Increment();

                if (statement.PropmtPatterType == PromptPatternsEnum.SilentVoice)
                {
                    CreateSilentAudio(statement, projectPath, token);
                }
                else if (!string.IsNullOrEmpty(statement.Prompt))
                {

                    progressBar.ShowMessage($"Getting voices {++voiceCounter}");
                    var audioFileName = GenerateAudioFileName(statement.Prompt, projectPath);
                    statement.AudioPath = audioFileName;

                    await GetVoice(new TtsVoice
                    {
                        ModelId = "eleven_multilingual_v2", //eleven_multilingual_v2, eleven_v3
                        Id = selectedVoice.Id,
                    }, statement, token);
                }
                else
                {
                    throw new CustomApplicationException("Statement has no prompt and is not a silent voice.");
                }
            }
        }

        private static void CreateSilentAudio(Statement statement, string projectPath, CancellationToken token)
        {
            var silencePath = Path.Combine(projectPath, $"silencevoice_{statement.AudioDuration.TotalSeconds}.wav");
            AudioHelper.CreateSilentWavAudio(silencePath, statement.AudioDuration, token);

            statement.AudioPath = silencePath;
            statement.AudioPathWave = $"{statement.AudioPath}.wav";
            File.Copy(statement.AudioPath, statement.AudioPathWave, true);
        }

        private static string GenerateAudioFileName(string prompt, string projectPath)
        {
            var truncated = prompt[..Math.Min(prompt.Length, Constants.MAX_PATH)];
            var cleanFileName = PathHelper.CleanFileName(truncated);
            Directory.CreateDirectory(Path.Combine(projectPath, "voices"));
            return Path.Combine(projectPath, "voices", $"{cleanFileName}.wav");
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
