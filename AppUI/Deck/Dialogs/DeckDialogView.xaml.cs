using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AppUI.Deck.Dialogs
{
    /// <summary>
    /// The Deck-native modal dialog overlay (see <see cref="DeckDialogService"/>).
    /// </summary>
    public partial class DeckDialogView : UserControl
    {
        public DeckDialogView()
        {
            InitializeComponent();
        }

        private void DialogButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is DeckDialogButtonViewModel button)
            {
                (DataContext as DeckDialogViewModel)?.ActivateButtonViaMouse(button);
                e.Handled = true;
            }
        }
    }
}
