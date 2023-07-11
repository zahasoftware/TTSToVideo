﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace TTSToVideo.WPF.Converters
{
    public class ImageCollectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is List<Image> images)
            {
                StackPanel stack = new();
                stack.Orientation = Orientation.Horizontal;

                foreach (Image image in images)
                {
                    stack.Children.Add(image);
                }

                return stack;
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
