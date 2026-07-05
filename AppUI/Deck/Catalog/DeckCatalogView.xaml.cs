using AppUI.Deck.Input;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AppUI.Deck.Catalog
{
    /// <summary>
    /// Catalog browsing section content (see <see cref="DeckCatalogViewModel"/>).
    /// </summary>
    public partial class DeckCatalogView : UserControl
    {
        public DeckCatalogView()
        {
            InitializeComponent();
            DataContextChanged += DeckCatalogView_DataContextChanged;
        }

        private void DeckCatalogView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is DeckCatalogViewModel oldVm)
            {
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is DeckCatalogViewModel newVm)
            {
                newVm.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // the free-text field needs real WPF keyboard focus while the overlay is up
            if (e.PropertyName == nameof(DeckCatalogViewModel.IsSearchOverlayOpen))
            {
                var vm = (DeckCatalogViewModel)sender;

                if (vm.IsSearchOverlayOpen)
                {
                    Dispatcher.BeginInvoke(new System.Action(() =>
                    {
                        txtSearch.Focus();
                        txtSearch.CaretIndex = txtSearch.Text.Length;
                        Keyboard.Focus(txtSearch);
                    }), System.Windows.Threading.DispatcherPriority.Input);
                }
                else
                {
                    Keyboard.ClearFocus();
                }
            }
        }

        private void lstCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstCategories.SelectedItem != null)
            {
                lstCategories.ScrollIntoView(lstCategories.SelectedItem);
            }
        }

        private void lstCatalogMods_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstCatalogMods.SelectedItem != null)
            {
                lstCatalogMods.ScrollIntoView(lstCatalogMods.SelectedItem);
            }
        }

        private void lstDownloads_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstDownloads.SelectedItem != null)
            {
                lstDownloads.ScrollIntoView(lstDownloads.SelectedItem);
            }
        }

        private void DownloadPause_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is ViewModels.DownloadItemViewModel item)
            {
                (DataContext as DeckCatalogViewModel)?.PauseOrResumeViaMouse(item);
                e.Handled = true;
            }
        }

        private void DownloadCancel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is ViewModels.DownloadItemViewModel item)
            {
                (DataContext as DeckCatalogViewModel)?.CancelViaMouse(item);
                e.Handled = true;
            }
        }

        #region Mouse support

        private void lstCategories_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            (DataContext as DeckCatalogViewModel)?.FocusColumnViaMouse(DeckCatalogViewModel.CatalogColumn.Categories);
        }

        private void lstCatalogMods_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            (DataContext as DeckCatalogViewModel)?.FocusColumnViaMouse(DeckCatalogViewModel.CatalogColumn.Mods);
        }

        private void lstCatalogMods_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            (DataContext as DeckCatalogViewModel)?.HandleCommand(DeckCommand.Activate);
        }

        private void ModLink_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DeckLinkOpener.Open((DataContext as DeckCatalogViewModel)?.FocusedModLink);
        }

        #endregion
    }
}
