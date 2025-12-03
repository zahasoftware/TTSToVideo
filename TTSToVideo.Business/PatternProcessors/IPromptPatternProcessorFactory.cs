using TTSToVideo.Business.Models;

namespace TTSToVideo.Business.PatternProcessors
{
    /// <summary>
    /// Factory interface for managing pattern processors.
    /// Use this to register and retrieve pattern processors.
    /// </summary>
    public interface IPromptPatternProcessorFactory
    {
        /// <summary>
        /// Register a new pattern processor
        /// </summary>
        /// <param name="processor">The processor to register</param>
        void RegisterProcessor(IPromptPatternProcessor processor);

        /// <summary>
        /// Get a processor by pattern type
        /// </summary>
        /// <param name="patternType">The pattern type to look for</param>
        /// <returns>The processor if found, null otherwise</returns>
        IPromptPatternProcessor? GetProcessor(PromptPatternsEnum patternType);

        /// <summary>
        /// Get all registered processors
        /// </summary>
        /// <returns>Collection of all registered processors</returns>
        IEnumerable<IPromptPatternProcessor> GetAllProcessors();

        /// <summary>
        /// Find the best processor that can handle the given paragraph.
        /// Returns the processor with highest priority (lowest priority number).
        /// </summary>
        /// <param name="paragraph">The paragraph to process</param>
        /// <returns>The best matching processor, or null if none found</returns>
        IPromptPatternProcessor? FindProcessorForParagraph(string paragraph);

        /// <summary>
        /// Get all processors that can handle the given paragraph, ordered by priority
        /// </summary>
        /// <param name="paragraph">The paragraph to process</param>
        /// <returns>Collection of processors that can handle the paragraph</returns>
        IEnumerable<IPromptPatternProcessor> FindAllProcessorsForParagraph(string paragraph);
    }
}
