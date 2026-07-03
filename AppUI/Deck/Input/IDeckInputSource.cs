using System;

namespace AppUI.Deck.Input
{
    /// <summary>
    /// A device-specific input reader that translates raw input into logical
    /// <see cref="DeckCommand"/>s. The keyboard source is the v1 implementation;
    /// a controller-reading source can be added later without touching any views.
    /// </summary>
    public interface IDeckInputSource
    {
        event Action<DeckCommand> CommandRaised;
    }
}
