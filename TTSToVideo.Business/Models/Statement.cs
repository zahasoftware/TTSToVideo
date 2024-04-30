using NetXP.ImageGeneratorAI;
using System;
using System.Collections.Generic;

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
        public string AudioPath { get;  set; }
        public TimeSpan AudioDuration { get;  set; }
        public string NegativePrompt { get;  set; }
    }
}