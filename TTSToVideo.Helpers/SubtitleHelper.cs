using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.Helpers
{
    public static class SubtitleHelper

    {
        public static List<string> SplitByMaxChars(string textParam, int maxChars)
        {
            string text = textParam;
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(text) || maxChars <= 0)
                return result;

            if (text.Length <= maxChars)
                return [text];

            int length;
            int lastSpace;

            while (true)
            {
                length = Math.Min(maxChars, text.Length);
                int searchLength = Math.Min(length, text.Length);

                if (length < maxChars)
                {
                    result.Add(text);
                    return result;
                }

                do
                {
                    lastSpace = text.LastIndexOf('.', searchLength - 1, searchLength);
                    if (lastSpace == -1)
                    {
                        searchLength += (searchLength/2);
                    }
                } while (lastSpace == -1 && searchLength <= text.Length);

                if (lastSpace == -1)
                {
                    result.Add(text);
                    return result;
                }

                string chunk = text[..(lastSpace + 1)];


                if (!string.IsNullOrEmpty(chunk))
                    result.Add(chunk);

                text = text[(lastSpace + 1)..];
                text = text.TrimStart();
            }
        }
        public static int CalculateMaxChars(int screenWidth, int fontSize, int marginL, int marginR)
        {
            // Estimate average character width as half the font size (adjust as needed)
            double avgCharWidth = fontSize * 0.5;
            int usableWidth = screenWidth - marginL - marginR;
            return (int)(usableWidth / avgCharWidth);
        }

    }
}
