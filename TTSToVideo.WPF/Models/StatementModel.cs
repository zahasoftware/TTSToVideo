using NetXP.ImageGeneratorAI;
using PropertyChanged;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using TTSToVideo.Business.Models;
using TTSToVideo.Helpers;
using TTSToVideo.Helpers.Implementations.Ffmpeg;
using static System.IO.PathHelper;

namespace TTSToVideo.WPF.Models
{
    [AddINotifyPropertyChangedInterface]
    public class StatementModel
    {
        private FfmpegFontStyle? fontStyle;

        public StatementModel()
        {
            Text = string.Empty;
            ImageId = string.Empty;
            AudioPath = string.Empty;
            Images = [];
            FontStyle = new FfmpegFontStyle();
        }

        public string Text { get; set; }
        public bool IsFinalParagraph { get; set; }
        public string ImageId { get; set; }
        public ObservableCollection<StatementImageModel> Images { get; set; }
        public string AudioPath { get; set; }
        public TimeSpan AudioDuration { get; set; }
        public FfmpegFontStyle? FontStyle
        {
            get => fontStyle ??= new FfmpegFontStyle();
            set => fontStyle = value;
        }
        public string? ImageAnimatedPath { get; set; }

        // New: Short version used for playback
        public string? PlaybackVideoPath => PathHelpers.EnsureShortVideoPath(ImageAnimatedPath);

        public bool IsVideoLoaded { get; internal set; }
        public string? ImagePrompt { get; set; }
        public string? VideoPrompt { get; set; }

        internal Statement ToStatement()
        {
            return new Statement
            {
                Prompt = Text,
                IsFinalParagraph = IsFinalParagraph,
                ImageId = ImageId,
                Images = [.. Images.Select(i => i.ToStatementImage())],
                AudioPath = AudioPath,
                AudioDuration = AudioDuration,
                FontStyle = FontStyle,
                ImageAnimatedPath = ImageAnimatedPath,
                VideoPrompt = VideoPrompt,
                ImagePrompt = ImagePrompt,
            };
        }
    }
}