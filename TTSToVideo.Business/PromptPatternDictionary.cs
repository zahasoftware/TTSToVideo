using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.Business
{
    public static class PromptPatternDictionary
    {

        public static readonly Dictionary<PromptPatternsEnum, PromptPattern> Patterns = new()
        {
                { PromptPatternsEnum.SilentVoice, new PromptPattern{ Pattern = "(<(?:[sS]|[sS]ilence):\\d+>)" , TypeRegex = PromptPatternsEnum.SilentVoice} },
                { PromptPatternsEnum.NewParagraph,new PromptPattern { Pattern = "\r\n\r\n" , IsParagraphSeparator = true , TypeRegex = PromptPatternsEnum.NewParagraph} },
                { PromptPatternsEnum.NewParagraph2,new PromptPattern { Pattern = "\n\n" , IsParagraphSeparator = true , TypeRegex = PromptPatternsEnum.NewParagraph2} },
        };

    }
}
