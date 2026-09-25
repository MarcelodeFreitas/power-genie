using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PowerGenie.App.Views;

// System.Drawing.Color/ColorConverter/Brushes are also implicitly in scope here (this project
// enables UseWindowsForms), so the WPF media types below are fully qualified to avoid ambiguity.
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex)
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch (FormatException)
            {
                // Falls through to the transparent default below.
            }
        }

        return System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
