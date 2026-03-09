using System;
using System.Globalization;
using System.Windows.Data;

namespace WpfApp3.Converters
{
    public class EnumEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return value.ToString() == parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Only used for IsChecked binding; return the enum value when checked
            if (value is bool b && b && parameter != null)
                return Enum.Parse(targetType, parameter.ToString()!);

            return Binding.DoNothing;
        }
    }
}
