using AppUI.Deck.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AppUI.Deck.Options
{
    /// <summary>
    /// Full-screen takeover rendering a mod's options (see <see cref="DeckModOptionsViewModel"/>).
    /// </summary>
    public partial class DeckModOptionsView : UserControl
    {
        public DeckModOptionsView()
        {
            InitializeComponent();
        }

        private void lstOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstOptions.SelectedItem != null)
            {
                lstOptions.ScrollIntoView(lstOptions.SelectedItem);
            }
        }

        private void lstValues_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstValues.SelectedItem != null)
            {
                lstValues.ScrollIntoView(lstValues.SelectedItem);
            }
        }

        #region Mouse support

        private void lstOptions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstOptions.SelectedItem is DeckOptionRowViewModel row)
            {
                (DataContext as DeckModOptionsViewModel)?.ActivateRowViaMouse(row);
            }
        }

        private void BoolPill_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is DeckOptionRowViewModel row)
            {
                (DataContext as DeckModOptionsViewModel)?.ActivateRowViaMouse(row);
                e.Handled = true;
            }
        }

        private void lstValues_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            (DataContext as DeckModOptionsViewModel)?.HandleCommand(DeckCommand.Activate);
        }

        #endregion
    }
}
