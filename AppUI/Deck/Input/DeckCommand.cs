namespace AppUI.Deck.Input
{
    /// <summary>
    /// Logical commands the Deck UI reacts to. Views never see raw device input;
    /// input sources (keyboard now, controller later) translate to these.
    /// </summary>
    public enum DeckCommand
    {
        NavigateUp,
        NavigateDown,
        NavigateLeft,
        NavigateRight,
        Activate,
        Back,
        SectionPrev,
        SectionNext,
        PageUp,
        PageDown,
        OpenOptions,
        Search,
        ReorderToggle,
        PlayShort,
        PlayLong,
    }
}
