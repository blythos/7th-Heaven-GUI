using AppUI.Classes.Themes;
using AppUI.Deck.Input;
using AppUI.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace AppUI.Deck
{
    /// <summary>
    /// Fullscreen shell window for Deck mode (launched with <c>--deck</c>).
    /// Hosts the sidebar, My Mods list, passive details pane, and button legend;
    /// all navigation flows through the logical command layer.
    /// </summary>
    public partial class DeckShellWindow : Window
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        internal DeckShellViewModel ViewModel { get; private set; }

        private MainWindowViewModel _mainViewModel;
        private DeckCommandRouter _router;
        private KeyboardInputSource _keyboardSource;
        private ControllerInputSource _controllerSource;

        public DeckShellWindow()
        {
            // Focus accent for all Deck surfaces: a fixed app-level resource for now
            // (single accent across light/dark), deliberately not an ITheme property —
            // flagged for a later decision.
            App.Current.Resources["DeckAccentColor"] = System.Windows.Media.Color.FromRgb(0x2F, 0xBF, 0x71);
            App.Current.Resources["DeckAccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2F, 0xBF, 0x71));

            // structural panel border (used for the FF7 theme's bevel; invisible otherwise)
            App.Current.Resources["DeckPanelBorderBrush"] = System.Windows.Media.Brushes.Transparent;
            App.Current.Resources["DeckPanelBorderThickness"] = new Thickness(0);

            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Info("Deck mode: initializing");

            // same init sequence the desktop MainWindow runs
            _mainViewModel = new MainWindowViewModel();
            _mainViewModel.InitViewModel();

            // InitViewModel applied the saved desktop theme; Deck mode always uses its own.
            // FF7 theme is the temporary default for review (switch back to DeckDark later);
            // its accent is the game's teal-green label colour.
            new ThemeSettingsViewModel(loadThemeXml: false).ApplyBuiltInTheme(AppTheme.DeckFF7);
            ApplyFF7ThemeExtras();

            ViewModel = new DeckShellViewModel(_mainViewModel);
            DataContext = ViewModel;
            ViewModel.OnDataReady();

            _router = new DeckCommandRouter();
            _router.PushHandler(ViewModel);

            _keyboardSource = new KeyboardInputSource(this);
            _router.AddSource(_keyboardSource);

            // physical pad support; polls and hot-plugs in the background
            _controllerSource = new ControllerInputSource();
            _router.AddSource(_controllerSource);

            // free-text fields (catalog search) need raw keys to reach the TextBox
            ViewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(DeckShellViewModel.IsTextEntryActive))
                {
                    _keyboardSource.TextEntryMode = ViewModel.IsTextEntryActive;
                }
            };

            Logger.Info("Deck mode: shell ready");
        }

        /// <summary>
        /// The FF7 look beyond the flat ITheme palette: vertical blue gradient fills
        /// (like the game's menu boxes) for rows and panels, and a silver bevel-style
        /// border on the structural overlay panels.
        /// </summary>
        private static void ApplyFF7ThemeExtras()
        {
            App.Current.Resources["DeckAccentColor"] = System.Windows.Media.Color.FromRgb(0x2F, 0xD6, 0xA3);
            App.Current.Resources["DeckAccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2F, 0xD6, 0xA3));

            var boxFill = new System.Windows.Media.LinearGradientBrush(
                new System.Windows.Media.GradientStopCollection()
                {
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x1E, 0x2E, 0xB4), 0.0),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x0C, 0x16, 0x64), 0.55),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x04, 0x07, 0x2A), 1.0),
                },
                90);
            boxFill.Freeze();

            // rows and the overlay panels all paint from these two brushes
            App.Current.Resources["PrimaryControlBackground"] = boxFill;
            App.Current.Resources["SecondaryAppBackground"] = boxFill;

            var bevel = new System.Windows.Media.LinearGradientBrush(
                new System.Windows.Media.GradientStopCollection()
                {
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0xE6, 0xE6, 0xEE), 0.0),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x9A, 0x9A, 0xA8), 0.5),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x5A, 0x5A, 0x68), 1.0),
                },
                90);
            bevel.Freeze();

            App.Current.Resources["DeckPanelBorderBrush"] = bevel;
            App.Current.Resources["DeckPanelBorderThickness"] = new Thickness(2);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // dev-only exit; controller users close via the launcher/Steam
            if (e.Key == Key.Q && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Close();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            _keyboardSource?.Detach();
            _controllerSource?.Dispose();
        }

        private void lstMods_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (lstMods.SelectedItem != null)
            {
                lstMods.ScrollIntoView(lstMods.SelectedItem);
            }
        }

        #region Mouse support

        /// <summary>Clicking anywhere in section content moves navigation focus there.</summary>
        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel == null || topBar.IsMouseOver || legendBar.IsMouseOver)
            {
                return;
            }

            ViewModel.FocusContentViaMouse();
        }

        private void SectionTab_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is DeckSectionItemViewModel item)
            {
                ViewModel?.SelectSectionViaMouse(item.Section);
                e.Handled = true;
            }
        }

        private void LegendChip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is DeckLegendItem item && item.Command.HasValue)
            {
                _router?.Route(item.Command.Value);
                e.Handled = true;
            }
        }

        private void lstMods_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.HandleCommand(DeckCommand.Activate);
        }

        private void ModLink_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            DeckLinkOpener.Open(ViewModel?.FocusedModLink);
        }

        private void lstPlayVariants_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.HandleCommand(DeckCommand.Activate);
        }

        private void PlayButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.HandleCommand(DeckCommand.PlayShort);
            e.Handled = true;
        }

        private void PlayVariantButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.HandleCommand(DeckCommand.PlayLong);
            e.Handled = true;
        }

        #endregion
    }
}
