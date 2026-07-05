using System;
using System.Collections.Generic;
using System.Linq;

namespace AppUI.Deck.Input
{
    /// <summary>
    /// Connects input sources to command handlers. Handlers form a stack —
    /// overlays push themselves on top and get first refusal on every command,
    /// so views never need to know which input device raised it.
    /// </summary>
    public class DeckCommandRouter
    {
        private readonly List<IDeckInputSource> _sources = new List<IDeckInputSource>();
        private readonly List<IDeckCommandHandler> _handlerStack = new List<IDeckCommandHandler>();

        /// <summary>Raised after every command is routed; carries whether a handler consumed it.</summary>
        public event Action<DeckCommand, bool> CommandRouted;

        public void AddSource(IDeckInputSource source)
        {
            _sources.Add(source);
            source.CommandRaised += Route;
        }

        public void PushHandler(IDeckCommandHandler handler)
        {
            _handlerStack.Add(handler);
        }

        public void PopHandler(IDeckCommandHandler handler)
        {
            _handlerStack.Remove(handler);
        }

        public void Route(DeckCommand command)
        {
            bool handled = false;

            foreach (IDeckCommandHandler handler in Enumerable.Reverse(_handlerStack))
            {
                if (handler.HandleCommand(command))
                {
                    handled = true;
                    break;
                }
            }

            CommandRouted?.Invoke(command, handled);
        }
    }
}
