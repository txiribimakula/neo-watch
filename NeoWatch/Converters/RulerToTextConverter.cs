using System;
using System.Globalization;
using System.Windows.Data;
using NeoWatch.Geometries;

namespace NeoWatch.Converters
{
    public class RulerToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value != null) {
                var segment = (LineSegment)value;

                double length = Math.Sqrt(Math.Pow(segment.FinalPoint.X - segment.InitialPoint.X, 2) + Math.Pow(segment.FinalPoint.Y - segment.InitialPoint.Y, 2));

                return length.ToString("0.00", CultureInfo.InvariantCulture);
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }
    }
}
