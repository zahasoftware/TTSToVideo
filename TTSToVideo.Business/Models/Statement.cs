using System;
using System.Collections.Generic;
using TTSToVideo.Helpers.Implementations.Ffmpeg;

namespace TTSToVideo.Business.Models
{
    public class Statement
    {
        internal Statement? nextStatement;

        public Statement()
        {
            Images = [];
        }
        public string Prompt { get; set; }
        public bool IsFinalParagraph { get; set; }
        public string ImageId { get; set; }
        public List<StatementImage> Images { get; set; }
        public TimeSpan AudioDuration { get; set; }
        public string NegativePrompt { get; set; }
        public string GlobalPrompt { get; set; }
        public bool IsNewAudio { get; internal set; }
        public bool IsProtrait { get; internal set; }
        public string? ImageAnimatedPath { get; set; }
        public FfmpegFontStyle? FontStyle { get; set; }
        public PromptPatternsEnum PropmtPatterType { get; internal set; }
        public string OutputVideoPath { get; internal set; }
        public string SilenceAudioAcum { get; internal set; }
        public string Id { get; internal set; }
        public List<Statement> SubStatements { get; set; }
        public string? AudioPath { get; set; }
        public string AudioPathWave { get; internal set; }
        public bool IsSubtitle { get; internal set; }
        public bool IsTheLastSubtitle { get; internal set; }
        public string ParentId { get; internal set; }
        public bool HasSubstatements { get; internal set; }
        
        /// <summary>
        /// Custom image prompt description for this statement.
        /// Used when the paragraph is inside a <p> block with <ip> or <image-prompt> tags.
        /// </summary>
        public string? ImagePrompt { get; set; }
        
        /// <summary>
        /// Custom video prompt description for this statement.
        /// Used when the paragraph is inside a <p> block with <vp> or <video-prompt> tags.
        /// If both ImagePrompt and VideoPrompt are present, the image is converted to video using this prompt.
        /// If only VideoPrompt is present, the video is generated without an image.
        /// </summary>
        public string? VideoPrompt { get; set; }
        
        /// <summary>
        /// Voice ID to use for this statement.
        /// If not specified, the global voice will be used.
        /// </summary>
        public string? VoiceId { get; set; }
    }

}