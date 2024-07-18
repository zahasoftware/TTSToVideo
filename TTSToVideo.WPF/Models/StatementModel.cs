using NetXP.ImageGeneratorAI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TTSToVideo.WPF.Models
{
    public class StatementModel
    {
        public StatementModel()
        {
            Images = [];
        }

        public string Text { get; internal set; }
        public bool IsFinalParagraph { get; internal set; }
        public string ImageId { get; internal set; }
        public ObservableCollection<StatementImageModel> Images { get; set; }
        public string AudioPath { get; internal set; }
        public TimeSpan AudioDuration { get; internal set; }
    }
}