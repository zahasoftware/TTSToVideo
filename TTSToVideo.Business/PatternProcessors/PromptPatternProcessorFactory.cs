using TTSToVideo.Business.Models;

namespace TTSToVideo.Business.PatternProcessors
{
    /// <summary>
    /// Factory for creating and managing pattern processors.
    /// This uses the Strategy Pattern to allow easy addition of new processors.
    /// 
    /// To add a new processor:
    /// 1. Create a class implementing IPromptPatternProcessor (or inherit from PromptPatternProcessorBase)
    /// 2. Add the pattern type to PromptPatternsEnum
    /// 3. Register it in RegisterDefaultProcessors() method
    /// 4. Done! No other changes needed.
    /// </summary>
    public class PromptPatternProcessorFactory : IPromptPatternProcessorFactory
    {
        private readonly Dictionary<PromptPatternsEnum, IPromptPatternProcessor> _processors;
        private readonly object _lock = new();

        public PromptPatternProcessorFactory()
        {
            _processors = new Dictionary<PromptPatternsEnum, IPromptPatternProcessor>();
            RegisterDefaultProcessors();
        }

        /// <summary>
        /// Register all default processors here.
        /// To add a new processor, simply instantiate it and call RegisterProcessor.
        /// </summary>
        private void RegisterDefaultProcessors()
        {
            // Register the silent voice processor (active)
            RegisterProcessor(new SilentVoicePatternProcessor());

            // Register the emphasis processor (example - uncomment to activate)
            // RegisterProcessor(new EmphasisPatternProcessor());

            // Add more processors here as needed:
            // RegisterProcessor(new YourCustomPatternProcessor());
        }

        public void RegisterProcessor(IPromptPatternProcessor processor)
        {
            ArgumentNullException.ThrowIfNull(processor);

            lock (_lock)
            {
                _processors[processor.PatternType] = processor;
            }
        }

        public IPromptPatternProcessor? GetProcessor(PromptPatternsEnum patternType)
        {
            lock (_lock)
            {
                return _processors.TryGetValue(patternType, out var processor) ? processor : null;
            }
        }

        public IEnumerable<IPromptPatternProcessor> GetAllProcessors()
        {
            lock (_lock)
            {
                return _processors.Values.ToList();
            }
        }

        public IPromptPatternProcessor? FindProcessorForParagraph(string paragraph)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
                return null;

            lock (_lock)
            {
                // Return the processor with highest priority (lowest number) that can handle the paragraph
                return _processors.Values
                    .Where(p => p.CanProcess(paragraph))
                    .OrderBy(p => p.Priority)
                    .FirstOrDefault();
            }
        }

        public IEnumerable<IPromptPatternProcessor> FindAllProcessorsForParagraph(string paragraph)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
                return Enumerable.Empty<IPromptPatternProcessor>();

            lock (_lock)
            {
                return _processors.Values
                    .Where(p => p.CanProcess(paragraph))
                    .OrderBy(p => p.Priority)
                    .ToList();
            }
        }
    }
}
