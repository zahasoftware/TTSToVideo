using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace TTSToVideo.WPF.Converters
{
    public class FileToUriConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string p && !string.IsNullOrWhiteSpace(p) && File.Exists(p))
            {
                try
                {
                    return new Uri(p, UriKind.Absolute);
                }
                catch { }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}