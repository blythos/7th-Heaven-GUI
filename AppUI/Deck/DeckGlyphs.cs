namespace AppUI.Deck
{
    public enum DeckGlyphSet
    {
        Keyboard,
        Xbox,
    }

    /// <summary>Logical inputs the legend can reference; glyph text depends on the active set.</summary>
    public enum DeckLegendInput
    {
        Move,
        LeftRight,
        Activate,
        Back,
        Reorder,
        Options,
        Search,
        Sections,
        Pages,
        Play,
    }

    /// <summary>
    /// Maps logical legend inputs to display glyphs for the active input device.
    /// Keyboard is the default (and only) v1 set; a controller source later switches
    /// this to a pad set (with the manual Xbox/PS/Nintendo override from Settings).
    /// </summary>
    public static class DeckGlyphs
    {
        public static DeckGlyphSet CurrentSet { get; set; } = DeckGlyphSet.Keyboard;

        public static DeckLegendItem Item(DeckLegendInput input, string label)
        {
            return new DeckLegendItem(Get(input), label);
        }

        public static string Get(DeckLegendInput input)
        {
            if (CurrentSet == DeckGlyphSet.Keyboard)
            {
                switch (input)
                {
                    case DeckLegendInput.Move: return "↑↓";
                    case DeckLegendInput.LeftRight: return "← →";
                    case DeckLegendInput.Activate: return "Enter";
                    case DeckLegendInput.Back: return "Esc";
                    case DeckLegendInput.Reorder: return "R";
                    case DeckLegendInput.Options: return "O";
                    case DeckLegendInput.Search: return "F";
                    case DeckLegendInput.Sections: return "Q E";
                    case DeckLegendInput.Pages: return "PgUp PgDn";
                    case DeckLegendInput.Play: return "P";
                }
            }
            else
            {
                switch (input)
                {
                    case DeckLegendInput.Move: return "↑↓";
                    case DeckLegendInput.LeftRight: return "◄►";
                    case DeckLegendInput.Activate: return "A";
                    case DeckLegendInput.Back: return "B";
                    case DeckLegendInput.Reorder: return "X";
                    case DeckLegendInput.Options: return "Y";
                    case DeckLegendInput.Search: return "Y";
                    case DeckLegendInput.Sections: return "LB RB";
                    case DeckLegendInput.Pages: return "LT RT";
                    case DeckLegendInput.Play: return "☰";
                }
            }

            return "";
        }
    }
}
