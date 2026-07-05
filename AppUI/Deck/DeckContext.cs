namespace AppUI.Deck
{
    /// <summary>
    /// Process-wide Deck-mode flag, set by the existing <c>--deck</c> branch in
    /// <c>App.xaml.cs</c>. Shared code that must behave differently under Deck mode
    /// (currently only <c>MessageDialogWindow.Show</c>) checks this instead of every
    /// caller learning about Deck mode.
    /// </summary>
    public static class DeckContext
    {
        public static bool IsActive { get; set; }
    }
}
