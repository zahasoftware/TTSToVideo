using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.Helpers
{
    public interface IProgressBar
    {
        event EventHandler<int>? Incrementing;
        event EventHandler<string>? MessageChanged;

        double Total { get; set; }

        bool IsIncrementing { get; }

        void Increment();

        void ShowMessage(string message);

        void Clear();
    }
}
