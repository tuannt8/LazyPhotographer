using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace LazyPhotographer.Avalonia.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public IBrush TrueBrush { get; set; } = Brushes.DodgerBlue;
        public IBrush FalseBrush { get; set; } = Brushes.Transparent;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b && b) return TrueBrush;
            return FalseBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class FormatFloatConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is float f)
            {
                string format = parameter as string ?? "F1";
                return f.ToString(format, culture);
            }
            if (value is double d)
            {
                string format = parameter as string ?? "F1";
                return d.ToString(format, culture);
            }
            return value?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (float.TryParse(value?.ToString(), NumberStyles.Any, culture, out float f))
            {
                return f;
            }
            return 0.0f;
        }
    }
}
