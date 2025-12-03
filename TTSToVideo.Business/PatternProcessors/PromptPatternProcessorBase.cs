using System.Text.RegularExpressions;
using TTSToVideo.Business.Models;

namespace TTSToVideo.Business.PatternProcessors
{
    /// <summary>
    /// Abstract base class providing common functionality for pattern processors.
    /// Inherit from this class to create new pattern processors with less boilerplate.
    /// </summary>
    public abstract class PromptPatternProcessorBase : IPromptPatternProcessor
    {
        public abstract PromptPatternsEnum PatternType { get; }
        public abstract string Pattern { get; }
        public virtual int Priority => 100; // Default priority

        public virtual bool CanProcess(string paragraph)
        {
            return !string.IsNullOrWhiteSpace(paragraph) && 
                   Regex.IsMatch(paragraph, Pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        }

        public abstract void Process(string paragraph, string globalPrompt, List<Statement> statements);

        /// <summary>
        /// Helper method to create a standard statement
        /// </summary>
        protected Statement CreateStatement(string prompt, string globalPrompt, PromptPatternsEnum? patternType = null)
        {
            return new Statement
            {
                Prompt = prompt,
                GlobalPrompt = globalPrompt,
                PropmtPatterType = patternType ?? PromptPatternsEnum.None
            };
        }

        /// <summary>
        /// Helper method to split paragraph by pattern while keeping matched groups
        /// </summary>
        protected IEnumerable<string> SplitByPattern(string paragraph, string pattern)
        {
            return Regex.Split(paragraph, pattern, RegexOptions.None, TimeSpan.FromSeconds(1))
                        .Where(s => !string.IsNullOrWhiteSpace(s));
        }

        /// <summary>
        /// Helper method to check if a string matches the pattern
        /// </summary>
        protected bool IsMatch(string text, string pattern)
        {
            return Regex.IsMatch(text, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Helper method to get regex matches
        /// </summary>
        protected MatchCollection GetMatches(string text, string pattern)
        {
            return Regex.Matches(text, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        }
    }
}
