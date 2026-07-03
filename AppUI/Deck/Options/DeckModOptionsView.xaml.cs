using System.Windows.Controls;

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
    }
}
