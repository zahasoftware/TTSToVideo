using NetXP.ImageGeneratorAI;
using System;
using System.Collections.Generic;

namespace TTSToVideo.WPF.ViewModel.Implementations
{
    internal class Statement
    {
        public Statement()
        {
            Images = new List<StatementImage>();
        }
        public string Text { get; internal set; }
        public bool IsFinalParagraph { get; internal set; }
        public string ImageId { get; internal set; }
        public List<StatementImage> Images { get; set; }
        public string AudioPath { get; internal set; }
        public TimeSpan AudioDuration { get; internal set; }
    }
}