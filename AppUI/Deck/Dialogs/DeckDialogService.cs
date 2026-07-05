using AppUI.Deck.Input;
using AppUI.ViewModels;
using System.Windows;
using System.Windows.Threading;

namespace AppUI.Deck.Dialogs
{
    /// <summary>
    /// Bridges <c>MessageDialogWindow.Show(...)</c> to the Deck-native modal. The shell
    /// window attaches its dialog host and command router on load; from then on the two
    /// static Show entry points route here instead of opening a real WPF window, which
    /// a controller (raising only Deck logical commands, never system keystrokes) could
    /// not interact with.
    ///
    /// <see cref="Show"/> is synchronous by design: a nested dispatcher frame pumps the
    /// message loop until a button is chosen, so the dozens of existing
    /// <c>if (MessageDialogWindow.Show(...).Result == ...)</c> call sites work unchanged.
    /// </summary>
    public static class DeckDialogService
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private static DeckDialogHostViewModel _host;
        private static DeckCommandRouter _router;

        /// <summary>False until the Deck shell has loaded — callers fall back to the
        /// desktop window then (early-startup dialogs, or Deck flag without a shell).</summary>
        public static bool CanShow
        {
            get { return _host != null && _router != null; }
        }

        internal static void Attach(DeckDialogHostViewModel host, DeckCommandRouter router)
        {
            _host = host;
            _router = router;
        }

        internal static void Detach()
        {
            _host = null;
            _router = null;
        }

        /// <summary>
        /// Shows the Deck modal and blocks (pumping) until dismissed. Must be called on
        /// the UI thread — both <c>MessageDialogWindow.Show</c> overloads already wrap
        /// their body in <c>Dispatcher.Invoke</c>, which satisfies that.
        /// Returns a <see cref="MessageDialogViewModel"/> so call sites see the same
        /// contract as the desktop dialog.
        /// </summary>
        public static MessageDialogViewModel Show(string windowTitle, string prompt, string details, MessageBoxButton buttons)
        {
            var dialog = new DeckDialogViewModel(windowTitle, prompt, details, buttons);

            var frame = new DispatcherFrame();
            dialog.Completed += () => frame.Continue = false;

            _host.Push(dialog);
            _router.PushHandler(dialog);

            try
            {
                Dispatcher.PushFrame(frame);
            }
            finally
            {
                _router.PopHandler(dialog);
                _host.Pop(dialog);
            }

            Logger.Info($"Deck dialog '{windowTitle}' -> {dialog.Result}");

            return new MessageDialogViewModel()
            {
                WindowTitle = windowTitle,
                Message = prompt,
                Details = details,
                Result = dialog.Result,
            };
        }
    }
}
