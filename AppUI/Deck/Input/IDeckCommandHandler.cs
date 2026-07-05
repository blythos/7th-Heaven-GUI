namespace AppUI.Deck.Input
{
    /// <summary>
    /// Something that consumes logical commands — a screen, overlay, or the shell
    /// itself. The router forwards each command to the innermost active handler.
    /// </summary>
    public interface IDeckCommandHandler
    {
        /// <summary>Handle a logical command. Return true if consumed.</summary>
        bool HandleCommand(DeckCommand command);
    }
}
