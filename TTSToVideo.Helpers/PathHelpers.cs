using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.IO
{
    //Extends System.IO.Path class with one useful method

    public class PathHelper
    {
        public static string CleanFileName(string fileName) 
        {
            string safeFileName = fileName;
            char[] invalidChars = Path.GetInvalidFileNameChars();

            foreach (char invalidChar in invalidChars) 
            {
                safeFileName = safeFileName.Replace(invalidChar.ToString(), "_");
            }

            return safeFileName;
        }

        public static string RemoveAccentuation(string input)
        {
            // Create a NormalizationForm that decomposes accented characters into multiple separate characters
            NormalizationForm normalizationForm = NormalizationForm.FormD;

            // Normalize the input string using the specified normalization form
            string normalizedString = input.Normalize(normalizationForm);

            // Remove any non-spacing combining characters (accentuation marks)
            StringBuilder result = new();
            foreach (char c in normalizedString)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                    result.Append(c);
            }

            // Return the final result as a normalized string without accentuation characters
            return result.ToString().Normalize(NormalizationForm.FormC);
        }

    }
}
