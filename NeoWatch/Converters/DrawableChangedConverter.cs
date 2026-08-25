using System;
using System.Globalization;
using System.Windows.Data;
using NeoWatch.Drawing;
using NeoWatch.Loading;

namespace NeoWatch.Converters
{
    public class DrawableChangedConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return false;

            var drawable = values[0] as IDrawable;
            var watchItem = values[1] as WatchItem;
            return drawable != null && watchItem != null && watchItem.IsDrawableChanged(drawable);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
