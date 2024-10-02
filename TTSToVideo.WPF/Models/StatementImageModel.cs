using PropertyChanged;
using System;
using TTSToVideo.Business.Models;

namespace TTSToVideo.WPF.Models
{

    [AddINotifyPropertyChangedInterface]
    public class StatementImageModel
    {
        public string? Path { get; set; }

        internal StatementImage ToStatementImage()
        {
            return new StatementImage { Path = Path };
        }
    }
}