using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace TTSToVideo.WPF.Converters
{
    public class VideoVisibilityConverter : IValueConverter
    {
        private static readonly string[] VideoExts = [".mp4",".mov",".mkv",".webm",".avi",".m4v"];
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string p &&
                !string.IsNullOrWhiteSpace(p) &&
                File.Exists(p) &&
                VideoExts.Contains(Path.GetExtension(p).ToLowerInvariant()))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    public class InverseVisibilityConverter : IValueConverter
    {
        private readonly VideoVisibilityConverter _inner = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var v = (Visibility)_inner.Convert(value, typeof(Visibility), parameter, culture);
            return v == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}