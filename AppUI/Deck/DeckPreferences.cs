using Iros.Workshop;
using System;
using System.IO;
using System.Text.Json;

namespace AppUI.Deck
{
    /// <summary>
    /// Deck-only preferences (theme, UI scale, glyph brand), persisted to deck.json
    /// next to theme.xml in <see cref="Sys.SysFolder"/>. Kept out of the shared
    /// <c>AppCore</c> settings on purpose: golden rule 1 sanctions those files only for
    /// the default-play-command setting, and nothing outside Deck mode reads these.
    /// Setters live-apply: they persist immediately and raise their changed event.
    /// </summary>
    public static class DeckPreferences
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public const string ThemeDark = "DeckDark";
        public const string ThemeLight = "DeckLight";
        public const string ThemeFF7 = "DeckFF7";

        private class Model
        {
            public string Theme { get; set; } = ThemeDark;
            public double UiScale { get; set; } = 1.25;
            public string GlyphBrand { get; set; } = "Xbox";
        }

        private static Model _model;

        public static event Action ThemeChanged;
        public static event Action UiScaleChanged;
        public static event Action GlyphBrandChanged;

        private static string PathToFile
        {
            get { return Path.Combine(Sys.SysFolder, "deck.json"); }
        }

        private static Model Current
        {
            get
            {
                if (_model == null)
                {
                    _model = Load();
                }

                return _model;
            }
        }

        public static string Theme
        {
            get { return Current.Theme; }
            set
            {
                if (Current.Theme != value)
                {
                    Current.Theme = value;
                    Save();
                    ThemeChanged?.Invoke();
                }
            }
        }

        public static double UiScale
        {
            get { return Current.UiScale; }
            set
            {
                if (Math.Abs(Current.UiScale - value) > 0.001)
                {
                    Current.UiScale = value;
                    Save();
                    UiScaleChanged?.Invoke();
                }
            }
        }

        /// <summary>"Xbox", "PlayStation" or "Nintendo" — the manual glyph override from SPEC.</summary>
        public static string GlyphBrand
        {
            get { return Current.GlyphBrand; }
            set
            {
                if (Current.GlyphBrand != value)
                {
                    Current.GlyphBrand = value;
                    Save();
                    GlyphBrandChanged?.Invoke();
                }
            }
        }

        private static Model Load()
        {
            try
            {
                if (File.Exists(PathToFile))
                {
                    return JsonSerializer.Deserialize<Model>(File.ReadAllText(PathToFile)) ?? new Model();
                }
            }
            catch (Exception e)
            {
                Logger.Warn(e, "Failed to read deck.json - using defaults");
            }

            return new Model();
        }

        private static void Save()
        {
            try
            {
                File.WriteAllText(PathToFile, JsonSerializer.Serialize(_model, new JsonSerializerOptions() { WriteIndented = true }));
            }
            catch (Exception e)
            {
                Logger.Warn(e, "Failed to save deck.json");
            }
        }
    }
}
