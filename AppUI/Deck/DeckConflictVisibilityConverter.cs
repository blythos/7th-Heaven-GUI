using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AppUI.Deck
{
    /// <summary>
    /// Shows an element when a mod id has an entry in the shell's conflict map.
    /// Values: [0] the row's mod id (Guid), [1] the conflict dictionary (replaced on
    /// each recompute, which is what re-triggers this binding).
    /// </summary>
    public class DeckConflictVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2
                && values[0] is Guid modId
                && values[1] is Dictionary<Guid, List<string>> conflicts
                && conflicts.ContainsKey(modId))
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
