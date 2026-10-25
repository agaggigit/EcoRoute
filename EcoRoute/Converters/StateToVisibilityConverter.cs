// Menampilkan elemen hanya pada state tertentu.
// Contoh: Visibility="{Binding CurrentState, Converter={StaticResource StateToVisibility}, ConverterParameter='StateC_InputRoute,StateD_RouteResult'}"
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EcoRoute.Converters;

public class StateToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var states = (parameter as string ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return states.Contains(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
