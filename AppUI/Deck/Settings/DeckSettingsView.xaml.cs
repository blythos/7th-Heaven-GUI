using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AppUI.Deck.Settings
{
    /// <summary>
    /// Settings section content (see <see cref="DeckSettingsViewModel"/>).
    /// </summary>
    public partial class DeckSettingsView : UserControl
    {
        public DeckSettingsView()
        {
            InitializeComponent();
            DataContextChanged += DeckSettingsView_DataContextChanged;
        }

        private void DeckSettingsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DeckSettingsViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is DeckSettingsViewModel newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // the keyboard-fallback field needs real WPF keyboard focus while open
            if (e.PropertyName == nameof(DeckSettingsViewModel.IsTextOverlayOpen))
            {
                var vm = (DeckSettingsViewModel)sender;

                if (vm.IsTextOverlayOpen)
                {
                    Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        txtValue.Focus();
                        txtValue.CaretIndex = txtValue.Text.Length;
                        Keyboard.Focus(txtValue);
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                else
                {
                    Keyboard.ClearFocus();
                }
            }
        }

        private void lstSettings_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstSettings.SelectedItem != null)
            {
                lstSettings.ScrollIntoView(lstSettings.SelectedItem);
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
