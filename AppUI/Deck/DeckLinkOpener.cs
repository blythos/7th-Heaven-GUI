using System;
using System.Diagnostics;

namespace AppUI.Deck
{
    /// <summary>
    /// Opens a mod's external link in the default browser (mouse affordance;
    /// mirrors MainWindowViewModel.OpenPreviewModLink).
    /// </summary>
    public static class DeckLinkOpener
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static void Open(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                Logger.Warn(e, $"Failed to open link {url}");
            }
        }
    }
}
