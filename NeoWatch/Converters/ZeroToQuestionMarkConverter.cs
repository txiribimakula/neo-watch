using System;
using System.Globalization;
using System.Windows.Data;

namespace NeoWatch.Converters
{
    public class ZeroToQuestionMarkConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int i && i > 0 ? i.ToString(culture) : "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
