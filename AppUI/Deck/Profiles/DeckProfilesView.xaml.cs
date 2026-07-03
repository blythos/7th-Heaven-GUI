using System.Windows.Controls;

namespace AppUI.Deck.Profiles
{
    /// <summary>
    /// Profiles section content (see <see cref="DeckProfilesViewModel"/>).
    /// </summary>
    public partial class DeckProfilesView : UserControl
    {
        public DeckProfilesView()
        {
            InitializeComponent();
        }

        private void lstProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstProfiles.SelectedItem != null)
            {
                lstProfiles.ScrollIntoView(lstProfiles.SelectedItem);
            }
        }
    }
}
