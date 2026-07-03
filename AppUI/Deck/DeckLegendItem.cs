namespace AppUI.Deck
{
    /// <summary>
    /// One entry in the contextual button legend: a controller glyph badge and
    /// its sentence-case action label. Glyph text is the default Xbox set for
    /// now; a Settings override (Xbox/PS/Nintendo) swaps it later.
    /// </summary>
    public class DeckLegendItem
    {
        public string Glyph { get; }
        public string Label { get; }

        public DeckLegendItem(string glyph, string label)
        {
            Glyph = glyph;
            Label = label;
        }
    }
}
