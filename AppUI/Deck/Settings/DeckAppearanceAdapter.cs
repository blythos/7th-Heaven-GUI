using System;
using System.Collections.Generic;
using System.Linq;

namespace AppUI.Deck.Settings
{
    /// <summary>
    /// Appearance rows over <see cref="DeckPreferences"/>: theme, UI scale and the
    /// manual glyph-brand override (SPEC → "Input architecture"). The preference
    /// setters persist and raise change events; the shell window listens and applies
    /// theme/scale live.
    /// </summary>
    internal static class DeckAppearanceAdapter
    {
        private static readonly (string Key, string Label)[] Themes = new[]
        {
            (DeckPreferences.ThemeDark, "Deck dark"),
            (DeckPreferences.ThemeLight, "Deck light"),
            (DeckPreferences.ThemeFF7, "Sevenish"),
        };

        private static readonly double[] Scales = new[] { 1.0, 1.1, 1.25, 1.4 };

        private static readonly string[] Brands = new[] { "Xbox", "PlayStation", "Nintendo" };

        public static List<DeckSettingRowViewModel> BuildRows()
        {
            return new List<DeckSettingRowViewModel>()
            {
                DeckSettingRowViewModel.Choice("Theme", "Colour scheme for Deck mode (the desktop UI keeps its own theme)",
                    Themes.Select(t => t.Label).ToList(),
                    () => Math.Max(0, Array.FindIndex(Themes, t => t.Key == DeckPreferences.Theme)),
                    i => DeckPreferences.Theme = Themes[i].Key),

                DeckSettingRowViewModel.Choice("UI scale", "Overall size of the interface; 125% suits the Deck's 7\" screen",
                    Scales.Select(s => $"{s * 100:0}%").ToList(),
                    () => NearestScaleIndex(DeckPreferences.UiScale),
                    i => DeckPreferences.UiScale = Scales[i]),

                DeckSettingRowViewModel.Choice("Controller buttons", "Button glyphs shown in the legend and hints when using a pad",
                    Brands.ToList(),
                    () => Math.Max(0, Array.IndexOf(Brands, DeckPreferences.GlyphBrand)),
                    i => DeckPreferences.GlyphBrand = Brands[i]),
            };
        }

        private static int NearestScaleIndex(double scale)
        {
            int best = 0;

            for (int i = 1; i < Scales.Length; i++)
            {
                if (Math.Abs(Scales[i] - scale) < Math.Abs(Scales[best] - scale))
                {
                    best = i;
                }
            }

            return best;
        }
    }
}
