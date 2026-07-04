using System;

namespace AppUI.Deck
{
    public enum DeckGlyphSet
    {
        Keyboard,
        Xbox,
        PlayStation,
        Nintendo,
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
        Delete,
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

        /// <summary>The pad glyph set a controller source reports (the manual
        /// Xbox/PlayStation/Nintendo override from Settings; Xbox by default).</summary>
        public static DeckGlyphSet PadSet { get; private set; } = DeckGlyphSet.Xbox;

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

        /// <summary>Applies the glyph brand override; refreshes legends live when a pad
        /// set is currently showing.</summary>
        public static void SetPadSet(DeckGlyphSet set)
        {
            PadSet = set;

            if (CurrentSet != DeckGlyphSet.Keyboard)
            {
                SetCurrentSet(set);
            }
        }

        public static DeckLegendItem Item(DeckLegendInput input, string label)
        {
            return new DeckLegendItem(Get(input), label, GetCommand(input));
        }

        /// <summary>Device-sensitive confirm/cancel hint, e.g. "Enter quits · Esc cancels" or "A quits · B cancels".</summary>
        public static string ConfirmHint(string confirmVerb)
        {
            return $"{Get(DeckLegendInput.Activate)} {confirmVerb} · {Get(DeckLegendInput.Back)} cancels";
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
                case DeckLegendInput.Delete: return Input.DeckCommand.Delete;
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
                    case DeckLegendInput.Delete: return "Del";
                    case DeckLegendInput.Sections: return "Q E";
                    case DeckLegendInput.Pages: return "PgUp PgDn";
                    case DeckLegendInput.Play: return "P";
                }
            }
            else if (CurrentSet == DeckGlyphSet.PlayStation)
            {
                switch (input)
                {
                    case DeckLegendInput.Move: return "↑↓";
                    case DeckLegendInput.LeftRight: return "◄►";
                    case DeckLegendInput.Activate: return "✕";
                    case DeckLegendInput.Back: return "○";
                    case DeckLegendInput.Reorder: return "□";
                    case DeckLegendInput.Options: return "△";
                    case DeckLegendInput.Search: return "△";
                    case DeckLegendInput.Delete: return "Share";
                    case DeckLegendInput.Sections: return "L1 R1";
                    case DeckLegendInput.Pages: return "L2 R2";
                    case DeckLegendInput.Play: return "☰";
                }
            }
            else if (CurrentSet == DeckGlyphSet.Nintendo)
            {
                // Nintendo letters sit in different physical positions: Xbox X/Y
                // positions carry Nintendo's Y/X labels
                switch (input)
                {
                    case DeckLegendInput.Move: return "↑↓";
                    case DeckLegendInput.LeftRight: return "◄►";
                    case DeckLegendInput.Activate: return "A";
                    case DeckLegendInput.Back: return "B";
                    case DeckLegendInput.Reorder: return "Y";
                    case DeckLegendInput.Options: return "X";
                    case DeckLegendInput.Search: return "X";
                    case DeckLegendInput.Delete: return "−";
                    case DeckLegendInput.Sections: return "L R";
                    case DeckLegendInput.Pages: return "ZL ZR";
                    case DeckLegendInput.Play: return "+";
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
                    case DeckLegendInput.Delete: return "View";
                    case DeckLegendInput.Sections: return "LB RB";
                    case DeckLegendInput.Pages: return "LT RT";
                    case DeckLegendInput.Play: return "☰";
                }
            }

            return "";
        }
    }
}
