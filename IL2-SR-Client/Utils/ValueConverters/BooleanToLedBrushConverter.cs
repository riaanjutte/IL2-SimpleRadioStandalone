using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils.ValueConverters
{
    public class BooleanToLedBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush OnBrush = CreateFrozen(0x8F, 0xB5, 0x73);
        private static readonly SolidColorBrush OffBrush = CreateFrozen(0x54, 0x56, 0x4F);

        private static SolidColorBrush CreateFrozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isOn && isOn ? OnBrush : OffBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
