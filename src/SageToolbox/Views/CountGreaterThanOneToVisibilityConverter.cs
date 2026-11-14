using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SageToolbox.Views;

/// <summary>Visible seulement si la valeur (nombre d'éléments d'un groupe) est supérieure à 1.</summary>
public sealed class CountGreaterThanOneToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count > 1 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
