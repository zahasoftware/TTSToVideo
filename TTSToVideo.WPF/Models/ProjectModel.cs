using PropertyChanged;
using System;
using System.Collections.ObjectModel;

namespace TTSToVideo.WPF.Models
{
    [AddINotifyPropertyChangedInterface]
    public class ProjectModel
    {
        public string? FullPath { get; set; }
        public string? FileName { get; set; }
        public string? ProjectName { get; set; }
        public DateTime CreatedAt { get; set; }
        public CategoryModel Category { get; internal set; }
    }
}