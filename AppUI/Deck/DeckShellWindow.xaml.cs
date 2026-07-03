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

        public DeckShellWindow()
        {
            // Focus accent for all Deck surfaces: a fixed app-level resource for now
            // (single accent across light/dark), deliberately not an ITheme property —
            // flagged for a later decision.
            App.Current.Resources["DeckAccentColor"] = System.Windows.Media.Color.FromRgb(0x2F, 0xBF, 0x71);
            App.Current.Resources["DeckAccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2F, 0xBF, 0x71));

            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Info("Deck mode: initializing");

            // same init sequence the desktop MainWindow runs
            _mainViewModel = new MainWindowViewModel();
            _mainViewModel.InitViewModel();

            // InitViewModel applied the saved desktop theme; Deck mode always uses its own
            new ThemeSettingsViewModel(loadThemeXml: false).ApplyBuiltInTheme(AppTheme.DeckDark);

            ViewModel = new DeckShellViewModel(_mainViewModel);
            DataContext = ViewModel;
            ViewModel.OnDataReady();

            _router = new DeckCommandRouter();
            _router.PushHandler(ViewModel);

            _keyboardSource = new KeyboardInputSource(this);
            _router.AddSource(_keyboardSource);

            Logger.Info("Deck mode: shell ready");
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
        }

        private void lstMods_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (lstMods.SelectedItem != null)
            {
                lstMods.ScrollIntoView(lstMods.SelectedItem);
            }
        }
    }
}
