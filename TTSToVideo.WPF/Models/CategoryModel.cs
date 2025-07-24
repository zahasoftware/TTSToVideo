using PropertyChanged;
using System.IO;

namespace TTSToVideo.WPF.Models
{
    [AddINotifyPropertyChangedInterface]
    public class CategoryModel
    {
        public string? CategoryName { get; set; }
        public string? DirectoryPath { get; set; }
        public string? IAChatInstructions { get; set; } 
        public string? HashTags { get; set; }
        public ChatAIModel? ChatAIModelSelected { get; set; } 

    }
}