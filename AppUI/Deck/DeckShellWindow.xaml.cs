using System.Windows;
using System.Windows.Input;

namespace AppUI.Deck
{
    /// <summary>
    /// Fullscreen shell window for Deck mode (launched with <c>--deck</c>).
    /// </summary>
    public partial class DeckShellWindow : Window
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public DeckShellWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Info("Deck mode");
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Temporary exit until the input command layer owns Back handling
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
