# Video Prompt Quick Reference

## Tag Syntax
```xml
<vp>Video prompt text here</vp>
```
or
```xml
<video-prompt>Video prompt text here</video-prompt>
```

## Three Usage Modes

### 1?? Image + Video (Image-to-Video Conversion)
```xml
<p>
Your text here.
<ip>Image description</ip>
<vp>Video motion/effects description</vp>
</p>
```
? Generates image ? Converts to video with motion

### 2?? Video Only (Direct Video Generation)
```xml
<p>
<vp>Video description</vp>
Your text here.
</p>
```
? Generates video directly (no image)

### 3?? Image Only (Traditional)
```xml
<p>
<ip>Image description</ip>
Your text here.
</p>
```
? Generates image only (existing behavior)

## Quick Examples

### Example 1: Landscape Scene with Camera Motion
```xml
<p>
The sun slowly sets behind the distant mountains.

<ip>A panoramic view of mountain peaks at golden hour</ip>
<vp>Slow horizontal pan from left to right, warm sunset lighting</vp>

The colors paint the sky in shades of orange and purple.
</p>
```

### Example 2: Dynamic Weather Timelapse
```xml
<p>
<vp>Dramatic timelapse of storm clouds gathering and moving across the sky</vp>

The weather changes rapidly in the mountains.
Thunder rumbles in the distance.
</p>
```

### Example 3: Character Scene
```xml
<p>
She walks through the forest path.

<ip>A young woman in hiking gear walking on a forest trail</ip>
<vp>Smooth tracking shot following the character from behind, dappled sunlight through trees</vp>

Birds chirp in the canopy above.
</p>
```

## Rules
- ? Both tags are case-insensitive
- ? Can use short (`<vp>`) or long (`<video-prompt>`) form
- ? Video prompt applies to ALL paragraphs in the `<p>` block
- ? Tags are removed from final text
- ? Can combine with `<ip>` tags
- ? Works with existing features (silent voice, etc.)

## Common Patterns

### Establishing Shot
```xml
<vp>Wide aerial view slowly descending to reveal the landscape</vp>
```

### Close-up Detail
```xml
<vp>Slow zoom in to focus on fine details, shallow depth of field</vp>
```

### Transition
```xml
<vp>Smooth fade transition between scenes with crossfade effect</vp>
```

### Action Sequence
```xml
<vp>Fast-paced cutting between angles, dynamic camera movements</vp>
```

### Ambient Scene
```xml
<vp>Gentle floating camera movement, soft ambient lighting, peaceful mood</vp>
```

## Integration with Code

### Checking for Video Prompt
```csharp
if (!string.IsNullOrEmpty(statement.VideoPrompt))
{
    // Video prompt is available
    Console.WriteLine($"Video Prompt: {statement.VideoPrompt}");
}
```

### Handling Both Prompts
```csharp
if (!string.IsNullOrEmpty(statement.ImagePrompt) && 
    !string.IsNullOrEmpty(statement.VideoPrompt))
{
    // Image-to-video conversion scenario
    var image = await GenerateImage(statement.ImagePrompt);
    var video = await ConvertToVideo(image, statement.VideoPrompt);
}
else if (!string.IsNullOrEmpty(statement.VideoPrompt))
{
    // Direct video generation scenario
    var video = await GenerateVideo(statement.VideoPrompt);
}
```

## Tips
- ?? Be specific about camera movements (pan, tilt, zoom, tracking)
- ?? Describe lighting conditions (golden hour, dramatic, soft, etc.)
- ?? Mention pacing (slow, fast, smooth, sudden)
- ?? Include mood/atmosphere descriptions
- ?? Specify angles when relevant (wide, close-up, aerial)

## Property Details
```csharp
public class Statement
{
    // ... other properties ...
    
    /// <summary>
    /// Custom video prompt description for this statement.
    /// Used when the paragraph is inside a <p> block with <vp> or <video-prompt> tags.
    /// If both ImagePrompt and VideoPrompt are present, the image is converted to 
    /// video using this prompt. If only VideoPrompt is present, the video is 
    /// generated without an image.
    /// </summary>
    public string? VideoPrompt { get; set; }
}
```

---

**See Also:**
- Full Documentation: `TTSToVideo.WPF/Prompts/vp-tag-usage.txt`
- Implementation Summary: `VIDEO_PROMPT_FEATURE_IMPLEMENTATION.md`
