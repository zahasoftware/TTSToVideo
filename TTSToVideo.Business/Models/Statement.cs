using NetXP.ImageGeneratorAI;
using System;
using System.Collections.Generic;
using TTSToVideo.Helpers.Implementations.Ffmpeg;

namespace TTSToVideo.Business.Models
{
    public class Statement
    {
        public Statement()
        {
            Images = new List<StatementImage>();
        }
        public string Prompt { get;  set; }
        public bool IsFinalParagraph { get;  set; }
        public string ImageId { get;  set; }
        public List<StatementImage> Images { get; set; }
        public string VoiceAudioPath { get;  set; }
        public TimeSpan AudioDuration { get;  set; }
        public string NegativePrompt { get;  set; }
        public string GlobalPrompt { get; internal set; }
        public bool IsNewAudio { get; internal set; }
        public string VoiceAudioPathWave { get; internal set; }
        public bool IsProtrait { get; internal set; }
        public string? VideoPath { get; internal set; }
        public FfmpegFontStyle? FontStyle { get; set; }
        public PromptPatternsEnum PropmtPatterType { get; internal set; }
    }
}