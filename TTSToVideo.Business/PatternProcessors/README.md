# Pattern Processor Architecture

## Overview

This architecture uses the **Strategy Pattern** to make it easy to add new pattern processing capabilities to the TTS-to-Video system without modifying existing code.

## Architecture Components

### 1. **IPromptPatternProcessor** (Interface)
Defines the contract for all pattern processors.

### 2. **PromptPatternProcessorBase** (Abstract Base Class)
Provides common functionality and helper methods to reduce boilerplate in concrete processors.

### 3. **Concrete Processors**
- `SilentVoicePatternProcessor` - Handles `<s:X>` and `<silence:X>` tags
- `EmphasisPatternProcessor` - Example processor for `<emphasis:level>text</emphasis>` tags

### 4. **IPromptPatternProcessorFactory** / **PromptPatternProcessorFactory**
Manages registration and retrieval of processors using the Factory Pattern.

## How to Add a New Pattern Processor

### Step 1: Add Enum Value
Add your pattern type to `PromptPatternsEnum`:
```csharp
public enum PromptPatternsEnum
{
    None,
    SilentVoice,
    Emphasis,
    YourNewPattern  // Add here
}
```

### Step 2: Create Processor Class
Create a new class inheriting from `PromptPatternProcessorBase`:

```csharp
using TTSToVideo.Business.Models;
using TTSToVideo.Business.PatternProcessors;

namespace TTSToVideo.Business.PatternProcessors
{
    /// <summary>
    /// Processes your custom pattern.
    /// Format: <your:pattern>
    /// Example: "Text <your:param> more text"
    /// </summary>
    public class YourCustomPatternProcessor : PromptPatternProcessorBase
    {
        public override PromptPatternsEnum PatternType => PromptPatternsEnum.YourNewPattern;

        public override string Pattern => @"<your:(\w+)>";

        public override int Priority => 50; // Adjust priority as needed

        public override void Process(string paragraph, string globalPrompt, List<Statement> statements)
        {
            // Your processing logic here
            var matches = SplitByPattern(paragraph, Pattern);

            foreach (var match in matches)
            {
                if (IsMatch(match, Pattern))
                {
                    // Process your pattern
                    // Create statement based on pattern
                }
                else
                {
                    // Regular text
                    statements.Add(CreateStatement(match, globalPrompt));
                }
            }
        }
    }
}
```

### Step 3: Register Processor
In `PromptPatternProcessorFactory.RegisterDefaultProcessors()`, add:
```csharp
RegisterProcessor(new YourCustomPatternProcessor());
```

### Step 4: Done!
No other changes needed. The system will automatically use your processor.

## Pattern Priority System

Processors are executed based on priority (lower number = higher priority):
- **1-20**: Critical patterns (e.g., structure-altering patterns)
- **21-50**: High priority patterns (e.g., SilentVoice = 10)
- **51-100**: Normal priority patterns (e.g., Emphasis = 50)
- **100+**: Low priority patterns

## Design Patterns Used

1. **Strategy Pattern**: Each processor implements a strategy for handling specific patterns
2. **Factory Pattern**: Factory manages processor creation and retrieval
3. **Template Method Pattern**: Base class provides template methods for common operations
4. **Chain of Responsibility**: Processors are tried in order of priority

## Benefits

- ? **Open/Closed Principle**: Open for extension, closed for modification
- ? **Single Responsibility**: Each processor handles one pattern type
- ? **Easy Testing**: Each processor can be unit tested independently
- ? **Low Coupling**: Processors don't depend on each other
- ? **High Cohesion**: Related logic is grouped together
- ? **Maintainability**: Clear structure makes code easy to understand and modify

## Example Patterns

### Silent Voice Pattern
**Format**: `<s:10>` or `<silence:10>`  
**Purpose**: Creates 10 seconds of silence  
**Example**: `"Hello <s:5> World"` creates 5 seconds between "Hello" and "World"

### Emphasis Pattern (Example)
**Format**: `<emphasis:high>text</emphasis>`  
**Purpose**: Emphasizes text with special font styling  
**Levels**: low, medium, high  
**Example**: `"This is <emphasis:high>important</emphasis>!"`

## Testing

Each processor can be tested independently:

```csharp
[Test]
public void SilentVoiceProcessor_ParsesCorrectly()
{
    var processor = new SilentVoicePatternProcessor();
    var statements = new List<Statement>();
    
    processor.Process("Hello <s:5> World", "", statements);
    
    Assert.AreEqual(3, statements.Count);
    Assert.AreEqual(PromptPatternsEnum.SilentVoice, statements[1].PropmtPatterType);
    Assert.AreEqual(5, statements[1].AudioDuration.TotalSeconds);
}
```

## Migration Notes

The old `ProcessSilentVoicePattern` method has been replaced by `SilentVoicePatternProcessor`.
All functionality remains the same, but is now more maintainable and extensible.
