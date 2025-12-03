using TTSToVideo.Business.Models;
using TTSToVideo.Helpers.Implementations.Ffmpeg;

namespace TTSToVideo.Business.PatternProcessors
{
    /// <summary>
    /// EXAMPLE PATTERN PROCESSOR - Demonstrates how to create custom pattern processors.
    /// 
    /// This processor handles emphasis tags for highlighting important text.
    /// Format: &lt;emphasis:level&gt;text&lt;/emphasis&gt;
    /// Levels: low, medium, high
    /// 
    /// Example: "This is &lt;emphasis:high&gt;very important&lt;/emphasis&gt; information"
    /// 
    /// To use this processor:
    /// 1. Add PromptPatternsEnum.Emphasis to the enum
    /// 2. Register this processor in the factory
    /// 3. Done! The system will automatically use it.
    /// </summary>
    public class EmphasisPatternProcessor : PromptPatternProcessorBase
    {
        public override PromptPatternsEnum PatternType => PromptPatternsEnum.Emphasis;

        // Pattern to match: <emphasis:level>text</emphasis>
        public override string Pattern => @"<emphasis:(low|medium|high)>(.*?)</emphasis>";

        public override int Priority => 50; // Medium priority

        public override void Process(string paragraph, string globalPrompt, List<Statement> statements)
        {
            var matches = GetMatches(paragraph, Pattern);
            var lastIndex = 0;

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Add text before the emphasis tag
                if (match.Index > lastIndex)
                {
                    var beforeText = paragraph[lastIndex..match.Index];
                    if (!string.IsNullOrWhiteSpace(beforeText))
                    {
                        statements.Add(CreateStatement(beforeText, globalPrompt));
                    }
                }

                // Process the emphasized text
                var level = match.Groups[1].Value.ToLower();
                var text = match.Groups[2].Value;

                var statement = new Statement
                {
                    Prompt = text,
                    GlobalPrompt = globalPrompt,
                    PropmtPatterType = PromptPatternsEnum.Emphasis,
                    FontStyle = CreateEmphasisFontStyle(level)
                };

                statements.Add(statement);
                lastIndex = match.Index + match.Length;
            }

            // Add remaining text after the last match
            if (lastIndex < paragraph.Length)
            {
                var remainingText = paragraph[lastIndex..];
                if (!string.IsNullOrWhiteSpace(remainingText))
                {
                    statements.Add(CreateStatement(remainingText, globalPrompt));
                }
            }
        }

        /// <summary>
        /// Creates font style based on emphasis level
        /// </summary>
        private FfmpegFontStyle CreateEmphasisFontStyle(string level)
        {
            return level switch
            {
                "high" => new FfmpegFontStyle
                {
                    FontSize = 30,
                    Alignment = FfmpegAlignment.MiddleCenter,
                    SubtitleVisible = true
                },
                "medium" => new FfmpegFontStyle
                {
                    FontSize = 24,
                    Alignment = FfmpegAlignment.MiddleCenter,
                    SubtitleVisible = true
                },
                "low" => new FfmpegFontStyle
                {
                    FontSize = 18,
                    Alignment = FfmpegAlignment.TopCenter,
                    SubtitleVisible = true
                },
                _ => new FfmpegFontStyle
                {
                    FontSize = 20,
                    Alignment = FfmpegAlignment.TopCenter,
                    SubtitleVisible = true
                }
            };
        }
    }
}
