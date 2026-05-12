using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TTSToVideo.Helpers
{
    public static class Constants
    {
        public const int MAX_PATH = 128 - 4;//-4 Extension

        public static int SUBTITTLE_SIZE_DEFAULT { get; set; } = 12;

        public static string LANG_DEFAULT { get; set; } = "Default";
        public static string CONFIG_FILE_PROJECT { get; set; } = "TTSToVideo.json";
        public static int MARGINV_SIZE_DEFAULT { get; set; } = 110;
        public static string? FONT_BACKCOLOR_DEFAULT { get; internal set; } = "#FF000000"; //ARGB
        public static string? FONT_COLOR_DETAUL { get; internal set; } = "#FFFFFFFF";//ARGB
    }
}
