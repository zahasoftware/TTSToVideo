using NetXP.ImageGeneratorAI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TTSToVideo.Business.Models;
using TTSToVideo.Helpers;

namespace TTSToVideo.WPF.Models
{
    public class StatementModel
    {
        public StatementModel()
        {
            Images = [];
            FontStyle = new FfmpegFontStyle();
        }

        public string Text { get; internal set; }
        public bool IsFinalParagraph { get; internal set; }
        public string ImageId { get; internal set; }
        public ObservableCollection<StatementImageModel> Images { get; set; }
        public string AudioPath { get; internal set; }
        public TimeSpan AudioDuration { get; internal set; }
        public FfmpegFontStyle? FontStyle { get; internal set; }

        internal Statement ToStatement()
        {
            return new Statement
            {
                Prompt = Text,
                IsFinalParagraph = IsFinalParagraph,
                ImageId = ImageId,
                Images = Images.Select(i => i.ToStatementImage()).ToList(),
                VoiceAudioPath = AudioPath,
                AudioDuration = AudioDuration,
                FontStyle = FontStyle
            };
        }
    }
}