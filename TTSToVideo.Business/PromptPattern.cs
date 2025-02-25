using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.Business
{
    public class PromptPattern
    {
        public string Pattern { get; set; }

        public bool IsParagraphSeparator { get; set; }
        public PromptPatternsEnum TypeRegex { get; set; }
    }
}
