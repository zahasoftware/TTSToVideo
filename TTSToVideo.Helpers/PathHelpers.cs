using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TTSToVideo.Helpers;

namespace System.IO
{
    //Extends System.IO.Path class with one useful method

    public class PathHelper
    {
        public static class PathHelpers
        {
            private static readonly string CacheDir = Path.Combine(Path.GetTempPath(), "tts-video-cache");
            public static string EnsureShortVideoPath(string fullPath)
            {
                if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
                    return fullPath;

                // Under safe threshold => return original
                if (fullPath.Length < 230)
                    return fullPath;

                Directory.CreateDirectory(CacheDir);
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath))).Substring(0, 16);
                var ext = Path.GetExtension(fullPath);
                var shortPath = Path.Combine(CacheDir, hash + ext);

                if (!File.Exists(shortPath))
                    File.Copy(fullPath, shortPath, true);

                return shortPath;
            }
        }

        public static string CleanFileName(string fileName)
        {
            string safeFileName = fileName;
            char[] invalidChars = [.. Path.GetInvalidFileNameChars(), '"', '”', '“', '’'];

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

        public static string GenerateImagePath(string projectPath, string prompt, string extension = ".jpg")
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                throw new ArgumentNullException(nameof(projectPath));
            
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentNullException(nameof(prompt));

            var truncatedPrompt = prompt[..Math.Min(prompt.Length, Constants.MAX_PATH)];
            var cleanedName = CleanFileName(truncatedPrompt);
            
            // Ensure extension starts with a dot
            if (!extension.StartsWith("."))
                extension = "." + extension;
            
            return Path.Combine(projectPath, $"{cleanedName}{extension}");
        }
    }
}
