using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TTSToVideo.Helpers
{
    public partial class HashMD5 // Make the class partial to support partial methods  
    {
        // Use GeneratedRegexAttribute to generate the regular expression implementation at compile-time  
        private static Regex RemoveTagsRegex() => new Regex("<.*?>", RegexOptions.Compiled); // Provide implementation for the partial method  

        // Generate a hash from a string  
        public static string GenerateHash(string input)
        {
            // Remove all substrings matching the pattern <XXX> where XXX is any content  
            input = RemoveTagsRegex().Replace(input, string.Empty);

            ArgumentNullException.ThrowIfNull(input, nameof(input));

            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = System.Security.Cryptography.MD5.HashData(inputBytes);
            return Convert.ToHexString(hashBytes);
        }
    }
}
