using PropertyChanged;

namespace TTSToVideo.WPF.Models
{

    [AddINotifyPropertyChangedInterface]
    public class StatementImageModel
    {
        public string? Path { get; set; }
    }
}