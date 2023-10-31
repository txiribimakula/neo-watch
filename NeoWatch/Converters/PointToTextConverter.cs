using System;
using System.Globalization;
using System.Windows.Data;
using NeoWatch.Geometries;

namespace NeoWatch.Converters
{
    class PointToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value != null) {
                var point = (Point)value;

                return point.X.ToString("0.00", CultureInfo.InvariantCulture) + ", " + point.Y.ToString("0.00", CultureInfo.InvariantCulture);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
}
