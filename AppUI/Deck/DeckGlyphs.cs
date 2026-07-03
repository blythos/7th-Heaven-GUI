using System;

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
        public static DeckGlyphSet CurrentSet { get; private set; } = DeckGlyphSet.Keyboard;

        /// <summary>Raised when the active glyph set changes so legends can rebuild.</summary>
        public static event Action GlyphSetChanged;

        /// <summary>Input sources call this with their set whenever they raise a command,
        /// so the legend always shows the device the user is actually holding.</summary>
        public static void SetCurrentSet(DeckGlyphSet set)
        {
            if (CurrentSet != set)
            {
                CurrentSet = set;
                GlyphSetChanged?.Invoke();
            }
        }

        public static DeckLegendItem Item(DeckLegendInput input, string label)
        {
            return new DeckLegendItem(Get(input), label, GetCommand(input));
        }

        /// <summary>The single logical command a legend entry maps to for mouse clicks (null for multi-direction hints).</summary>
        private static Input.DeckCommand? GetCommand(DeckLegendInput input)
        {
            switch (input)
            {
                case DeckLegendInput.Activate: return Input.DeckCommand.Activate;
                case DeckLegendInput.Back: return Input.DeckCommand.Back;
                case DeckLegendInput.Reorder: return Input.DeckCommand.ReorderToggle;
                case DeckLegendInput.Options: return Input.DeckCommand.OpenOptions;
                case DeckLegendInput.Search: return Input.DeckCommand.Search;
                case DeckLegendInput.Play: return Input.DeckCommand.PlayShort;
                default: return null;
            }
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
