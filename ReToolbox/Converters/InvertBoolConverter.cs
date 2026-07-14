using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace ReToolbox.Converters
{
    public partial class InvertBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool b ? !b : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value is bool b ? !b : value;
        }
    }
}
