using System;
using System.Globalization;
using System.Windows.Data;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils.ValueConverters
{
    /// <summary>
    /// Maps a ProgressBar's (Value, Minimum, Maximum) to a VU dial needle angle.
    /// The dial scale spans -48..+48 degrees around vertical.
    /// </summary>
    public class VuNeedleAngleConverter : IMultiValueConverter
    {
        private const double MinAngle = -48.0;
        private const double MaxAngle = 48.0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3
                || !(values[0] is double value)
                || !(values[1] is double min)
                || !(values[2] is double max)
                || max <= min)
            {
                return MinAngle;
            }

            var fraction = (value - min) / (max - min);
            if (fraction < 0) fraction = 0;
            if (fraction > 1) fraction = 1;
            return MinAngle + (MaxAngle - MinAngle) * fraction;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
