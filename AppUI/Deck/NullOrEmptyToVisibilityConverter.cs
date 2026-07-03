using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AppUI.Deck
{
    /// <summary>Collapses an element when the bound value is null or an empty/whitespace string.</summary>
    public class NullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || (value is string text && string.IsNullOrWhiteSpace(text)))
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
