using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace ReToolbox.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool flag = value is bool b && b;
            // Passing parameter "invert" flips the result (true -> Collapsed).
            if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            bool flag = value is Visibility v && v == Visibility.Visible;
            if (parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }

            return flag;
        }
    }
}
