using TTSToVideo.Business.Models;

namespace TTSToVideo.Business.PatternProcessors
{
    /// <summary>
    /// Interface for processing specific prompt patterns.
    /// Implement this interface to add new pattern processing capabilities.
    /// </summary>
    public interface IPromptPatternProcessor
    {
        /// <summary>
        /// The pattern type this processor handles
        /// </summary>
        PromptPatternsEnum PatternType { get; }

        /// <summary>
        /// The regex pattern to match
        /// </summary>
        string Pattern { get; }

        /// <summary>
        /// The priority of this processor (lower = higher priority)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Check if this processor can handle the given paragraph
        /// </summary>
        /// <param name="paragraph">The paragraph to check</param>
        /// <returns>True if this processor can handle the paragraph</returns>
        bool CanProcess(string paragraph);

        /// <summary>
        /// Process a paragraph that contains this pattern
        /// </summary>
        /// <param name="paragraph">The paragraph containing the pattern</param>
        /// <param name="globalPrompt">Global prompt context</param>
        /// <param name="statements">List to add processed statements to</param>
        void Process(string paragraph, string globalPrompt, List<Statement> statements);
    }
}
