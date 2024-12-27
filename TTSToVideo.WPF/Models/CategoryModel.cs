using PropertyChanged;
using System.IO;

namespace TTSToVideo.WPF.Models
{
    [AddINotifyPropertyChangedInterface]
    public class CategoryModel
    {
        public string? CategoryName { get; set; }
        public string? DirectoryPath { get; set; }
    }
}