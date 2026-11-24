using System;
using System.Globalization;
using System.Windows.Data;

namespace FlowClicker.UI.Converters
{
    public sealed class NumberValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                double d => d,
                int i => (double)i,
                long l => (double)l,
                _ => null
            };
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            double numeric = value is double d && !double.IsNaN(d) && !double.IsInfinity(d)
                ? d
                : ParseFallback(parameter);

            if (targetType == typeof(int))
                return (int)Math.Clamp(Math.Round(numeric), int.MinValue, int.MaxValue);
            if (targetType == typeof(long))
                return (long)Math.Clamp(Math.Round(numeric), long.MinValue, long.MaxValue);

            return numeric;
        }

        private static double ParseFallback(object? parameter)
        {
            if (parameter is double d) return d;
            if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                return parsed;
            return 0d;
        }
    }
}
