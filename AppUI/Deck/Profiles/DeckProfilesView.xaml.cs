using AppUI.Deck.Input;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            DataContextChanged += DeckProfilesView_DataContextChanged;
        }

        private void DeckProfilesView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DeckProfilesViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is DeckProfilesViewModel newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // the name editor needs real WPF keyboard focus while open
            if (e.PropertyName == nameof(DeckProfilesViewModel.IsTextOverlayOpen))
            {
                var vm = (DeckProfilesViewModel)sender;

                if (vm.IsTextOverlayOpen)
                {
                    Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        txtProfileName.Focus();
                        txtProfileName.CaretIndex = txtProfileName.Text.Length;
                        Keyboard.Focus(txtProfileName);
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                else
                {
                    Keyboard.ClearFocus();
                }
            }
        }

        private void lstProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstProfiles.SelectedItem != null)
            {
                lstProfiles.ScrollIntoView(lstProfiles.SelectedItem);
            }
        }

        private void lstProfiles_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            (DataContext as DeckProfilesViewModel)?.HandleCommand(DeckCommand.Activate);
        }

        private void lstActions_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            (DataContext as DeckProfilesViewModel)?.HandleCommand(DeckCommand.Activate);
        }
    }
}
