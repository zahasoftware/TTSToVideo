using NetXP.Exceptions;
using TTSToVideo.Business.Models;

namespace TTSToVideo.Business.PatternProcessors
{
    /// <summary>
    /// Processes silent voice patterns like &lt;s:10&gt; or &lt;silence:5&gt;
    /// Format: &lt;s:seconds&gt; or &lt;silence:seconds&gt;
    /// Example: "Hello &lt;s:5&gt; World" creates 5 seconds of silence between "Hello" and "World"
    /// </summary>
    public class SilentVoicePatternProcessor : PromptPatternProcessorBase
    {
        private const int MAX_SILENCE_DURATION = 600; // 10 minutes max
        private const int MIN_SILENCE_DURATION = 0;

        public override PromptPatternsEnum PatternType => PromptPatternsEnum.SilentVoice;

        public override string Pattern => @"(<(?:[sS]|[sS]ilence):\d+>)";

        public override int Priority => 10; // High priority

        public override void Process(string paragraph, string globalPrompt, List<Statement> statements)
        {
            var matches = SplitByPattern(paragraph, Pattern);

            foreach (var match in matches)
            {
                if (IsMatch(match, Pattern))
                {
                    // This is a silent voice tag
                    var statement = ParseSilentVoiceTag(match, globalPrompt);
                    statements.Add(statement);
                }
                else
                {
                    // Regular text between silent voice tags
                    statements.Add(CreateStatement(match, globalPrompt));
                }
            }
        }

        /// <summary>
        /// Parses a silent voice tag and creates a corresponding statement
        /// </summary>
        private Statement ParseSilentVoiceTag(string tag, string globalPrompt)
        {
            // Remove < and > characters
            var cleanTag = tag.Trim('<', '>');

            // Split by colon to get the seconds part
            var parts = cleanTag.Split(':', StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
            {
                throw new CustomApplicationException(
                    $"Invalid silent voice tag format: \"{tag}\". " +
                    $"Expected format: <s:seconds> or <silence:seconds>");
            }

            var secondsText = parts[1];

            if (!int.TryParse(secondsText, out int seconds))
            {
                throw new CustomApplicationException(
                    $"Invalid seconds value in tag \"{tag}\". Seconds must be a valid integer.");
            }

            if (seconds < MIN_SILENCE_DURATION || seconds > MAX_SILENCE_DURATION)
            {
                throw new CustomApplicationException(
                    $"Silent duration in tag \"{tag}\" must be between {MIN_SILENCE_DURATION} and {MAX_SILENCE_DURATION} seconds.");
            }

            return new Statement
            {
                PropmtPatterType = PromptPatternsEnum.SilentVoice,
                AudioDuration = TimeSpan.FromSeconds(seconds),
                GlobalPrompt = globalPrompt,
            };
        }
    }
}
