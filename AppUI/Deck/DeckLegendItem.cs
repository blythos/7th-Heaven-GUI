using AppUI.Deck.Input;

namespace AppUI.Deck
{
    /// <summary>
    /// One entry in the contextual button legend: a glyph badge (keyboard or pad,
    /// via <see cref="DeckGlyphs"/>) and its sentence-case action label. Entries
    /// with a <see cref="Command"/> are clickable for mouse users and route the
    /// same logical command a key press would.
    /// </summary>
    public class DeckLegendItem
    {
        public string Glyph { get; }
        public string Label { get; }

        /// <summary>The logical command a mouse click raises; null for move/cycle hints.</summary>
        public DeckCommand? Command { get; }

        public bool IsClickable
        {
            get { return Command.HasValue; }
        }

        public DeckLegendItem(string glyph, string label, DeckCommand? command = null)
        {
            Glyph = glyph;
            Label = label;
            Command = command;
        }
    }
}
