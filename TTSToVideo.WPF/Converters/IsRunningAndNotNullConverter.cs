using System;
using System.Globalization;
using System.Windows.Data;

namespace TTSToVideo.WPF.Converters
{
    public class IsRunningAndNotNullConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            bool isRunning = values[0] is bool b && b;
            bool isNotNull = values[1] != null;
            return !isRunning && isNotNull;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}