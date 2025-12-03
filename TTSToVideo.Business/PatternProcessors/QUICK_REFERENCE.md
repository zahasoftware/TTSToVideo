# Quick Reference: Pattern Processor System

## Adding a New Pattern (3 Steps)

### 1. Define Enum
```csharp
// TTSToVideo.Business/PromptPatternsEnum.cs
public enum PromptPatternsEnum
{
    YourPattern  // Add your pattern
}
```

### 2. Create Processor
```csharp
// TTSToVideo.Business/PatternProcessors/YourPatternProcessor.cs
public class YourPatternProcessor : PromptPatternProcessorBase
{
    public override PromptPatternsEnum PatternType => PromptPatternsEnum.YourPattern;
    public override string Pattern => @"your-regex-here";
    public override int Priority => 50;

    public override void Process(string paragraph, string globalPrompt, List<Statement> statements)
    {
        // Your logic here
    }
}
```

### 3. Register
```csharp
// TTSToVideo.Business/PatternProcessors/PromptPatternProcessorFactory.cs
private void RegisterDefaultProcessors()
{
    RegisterProcessor(new YourPatternProcessor());
}
```

## Helper Methods Available

```csharp
// In PromptPatternProcessorBase
CreateStatement(prompt, globalPrompt)              // Create standard statement
SplitByPattern(paragraph, pattern)                 // Split by regex
IsMatch(text, pattern)                             // Check regex match
GetMatches(text, pattern)                          // Get all regex matches
```

## Pattern Examples

### Silent Voice
```
Input:  "Hello <s:5> World"
Output: ["Hello", 5sec silence, "World"]
```

### Emphasis (Example)
```
Input:  "This is <emphasis:high>important</emphasis>!"
Output: ["This is ", "important" (large font), "!"]
```

## Priority Guidelines

| Range  | Usage                          | Example         |
|--------|--------------------------------|-----------------|
| 1-20   | Structure-altering patterns    | Paragraphs      |
| 21-50  | High priority content patterns | SilentVoice(10) |
| 51-100 | Normal formatting patterns     | Emphasis(50)    |
| 100+   | Low priority decorations       | -               |

## Testing Template

```csharp
[Test]
public void YourProcessor_Works()
{
    var processor = new YourPatternProcessor();
    var statements = new List<Statement>();
    
    processor.Process("your input", "", statements);
    
    Assert.AreEqual(expectedCount, statements.Count);
}
```

## Current Processors

| Processor  | Pattern              | Priority | Status  |
|------------|----------------------|----------|---------|
| SilentVoice| `<s:X>`, `<silence:X>`| 10       | Active  |
| Emphasis   | `<emphasis:level>...`| 50       | Example |

## Need Help?

- Full docs: `PatternProcessors/README.md`
- Summary: `PatternProcessors/REFACTORING_SUMMARY.md`
- Example: See `EmphasisPatternProcessor.cs`
