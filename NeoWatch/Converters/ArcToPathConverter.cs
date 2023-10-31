using System;
using System.Globalization;
using System.Windows.Data;
using NeoWatch.Geometries;

namespace NeoWatch.Converters
{
    public class ArcToPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var arc = (ArcSegment)value;

            if (arc.SweepAngle == 360) {
                var arc1 = new ArcSegment(arc.CenterPoint, 0, 180, arc.Radius);
                var arc2 = new ArcSegment(arc.CenterPoint, 180, 180, arc.Radius);
                return ConvertArcToPath(arc1) + " " + ConvertArcToPath(arc2);
            } else {
                return ConvertArcToPath(arc);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return null;
        }

        private string ConvertArcToPath(ArcSegment arc) {
            return "M" + arc.InitialPoint.X.ToString(CultureInfo.InvariantCulture) + "," + arc.InitialPoint.Y.ToString(CultureInfo.InvariantCulture) +
                " A" + arc.Radius.ToString(CultureInfo.InvariantCulture) + "," + arc.Radius.ToString(CultureInfo.InvariantCulture) +
                " " + arc.SweepAngle.ToString(CultureInfo.InvariantCulture) +
                " " + (Math.Abs(arc.SweepAngle) >= 180 ? "1" : "0") +
                " " + (arc.SweepAngle > 0 ? "0" : "1") +
                " " + arc.FinalPoint.X.ToString(CultureInfo.InvariantCulture) + "," + arc.FinalPoint.Y.ToString(CultureInfo.InvariantCulture);
        }
    }
}
