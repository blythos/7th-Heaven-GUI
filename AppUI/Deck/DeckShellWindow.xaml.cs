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
            ResetDeckThemeExtras();

            // amber for the load-order conflict badge and detail block (dark text on top)
            App.Current.Resources["DeckWarningBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xA9, 0x3E));

            InitializeComponent();
        }

        /// <summary>
        /// The Deck-specific resources the flat themes use. Focus accent is a fixed
        /// app-level resource for now (single accent across light/dark), deliberately
        /// not an ITheme property — flagged for a later decision. The panel border is
        /// invisible outside the FF7 theme.
        /// </summary>
        private static void ResetDeckThemeExtras()
        {
            App.Current.Resources["DeckAccentColor"] = System.Windows.Media.Color.FromRgb(0x2F, 0xBF, 0x71);
            App.Current.Resources["DeckAccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2F, 0xBF, 0x71));
            App.Current.Resources["DeckPanelBorderBrush"] = System.Windows.Media.Brushes.Transparent;
            App.Current.Resources["DeckPanelBorderThickness"] = new Thickness(0);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Info("Deck mode: initializing");

            // same init sequence the desktop MainWindow runs
            _mainViewModel = new MainWindowViewModel();
            _mainViewModel.InitViewModel();

            // InitViewModel applied the saved desktop theme; Deck mode uses its own,
            // chosen in Settings -> Appearance and persisted in deck.json
            ApplyDeckTheme();
            ApplyUiScale();

            DeckPreferences.ThemeChanged += ApplyDeckTheme;
            DeckPreferences.UiScaleChanged += ApplyUiScale;
            DeckPreferences.GlyphBrandChanged += ApplyGlyphBrand;
            ApplyGlyphBrand();

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

            // from here on, MessageDialogWindow.Show renders as the in-shell Deck modal
            Dialogs.DeckDialogService.Attach(ViewModel.DialogHost, _router);

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

        private void ApplyDeckTheme()
        {
            AppTheme theme;

            switch (DeckPreferences.Theme)
            {
                case DeckPreferences.ThemeLight: theme = AppTheme.DeckLight; break;
                case DeckPreferences.ThemeFF7: theme = AppTheme.DeckFF7; break;
                default: theme = AppTheme.DeckDark; break;
            }

            Logger.Info($"Deck mode: applying theme {theme}");

            // rewrites all the shared brush keys, which also undoes the FF7 gradient fills
            new ThemeSettingsViewModel(loadThemeXml: false).ApplyBuiltInTheme(theme);

            if (theme == AppTheme.DeckFF7)
            {
                ApplyFF7ThemeExtras();
            }
            else
            {
                ResetDeckThemeExtras();
            }
        }

        private void ApplyUiScale()
        {
            rootScale.ScaleX = DeckPreferences.UiScale;
            rootScale.ScaleY = DeckPreferences.UiScale;
        }

        private void ApplyGlyphBrand()
        {
            switch (DeckPreferences.GlyphBrand)
            {
                case "PlayStation": DeckGlyphs.SetPadSet(DeckGlyphSet.PlayStation); break;
                case "Nintendo": DeckGlyphs.SetPadSet(DeckGlyphSet.Nintendo); break;
                default: DeckGlyphs.SetPadSet(DeckGlyphSet.Xbox); break;
            }
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

            // the game's windows shade diagonally: bright blue at the top-left corner
            // falling to near-black at the bottom-right (relative coordinates, so wide
            // rows lean mostly horizontal exactly like the game's wide dialog boxes)
            var boxFill = new System.Windows.Media.LinearGradientBrush()
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new System.Windows.Media.GradientStopCollection()
                {
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x28, 0x38, 0xC0), 0.0),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x0D, 0x17, 0x6E), 0.55),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x02, 0x04, 0x1A), 1.0),
                },
            };
            boxFill.Freeze();

            // rows and the overlay panels all paint from these two brushes
            App.Current.Resources["PrimaryControlBackground"] = boxFill;
            App.Current.Resources["SecondaryAppBackground"] = boxFill;

            // the bezel is lit the same way as the fill: white-silver at the top-left
            // of the frame shading to dark steel at the bottom-right, like the game's
            // rounded pipe border
            var bevel = new System.Windows.Media.LinearGradientBrush()
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                GradientStops = new System.Windows.Media.GradientStopCollection()
                {
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF), 0.0),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0xC0, 0xC0, 0xCC), 0.45),
                    new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x42, 0x42, 0x4E), 1.0),
                },
            };
            bevel.Freeze();

            App.Current.Resources["DeckPanelBorderBrush"] = bevel;
            App.Current.Resources["DeckPanelBorderThickness"] = new Thickness(3);
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
            DeckPreferences.ThemeChanged -= ApplyDeckTheme;
            DeckPreferences.UiScaleChanged -= ApplyUiScale;
            DeckPreferences.GlyphBrandChanged -= ApplyGlyphBrand;
            Dialogs.DeckDialogService.Detach();
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
