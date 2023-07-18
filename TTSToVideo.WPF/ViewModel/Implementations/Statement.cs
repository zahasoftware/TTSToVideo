using NetXP.ImageGeneratorAI;

namespace TTSToVideo.WPF.ViewModel.Implementations
{
    internal class Statement
    {
        public string Text { get; internal set; }
        public bool IsFinalParagraph { get; internal set; }
        public string ImageId { get; internal set; }
    }
}