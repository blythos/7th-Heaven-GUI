using System.Windows;
using System.Windows.Media;

namespace AppUI.Deck.Themes
{
    /// <summary>
    /// Deck mode dark theme — the default in Deck mode. Mirrors the light
    /// variant's role structure, using the app's existing dark theme values
    /// as reference (page #1f1f1f, foreground near-white, muted secondary).
    /// Pressed/mouse-over stay neutral: the accent is reserved for focus.
    /// </summary>
    public class DeckDarkTheme : Classes.Themes.ITheme
    {
        public string Name { get => "Deck dark"; }

        public string PrimaryAppBackground { get => "#1F1F1F"; }
        public string SecondaryAppBackground { get => "#292929"; }
        public string PrimaryControlBackground { get => "#292929"; }
        public string PrimaryControlForeground { get => "#EDEDEB"; }
        public string PrimaryControlSecondary { get => "#9C9B97"; }
        public string PrimaryControlPressed { get => "#3A3A38"; }
        public string PrimaryControlMouseOver { get => "#333331"; }
        public string PrimaryControlDisabledBackground { get => "#242424"; }
        public string PrimaryControlDisabledForeground { get => "#6B6A66"; }

        public string BackgroundImageName { get => null; }
        public string BackgroundImageBase64 { get => null; }
        public HorizontalAlignment BackgroundHorizontalAlignment { get => HorizontalAlignment.Center; }
        public VerticalAlignment BackgroundVerticalAlignment { get => VerticalAlignment.Center; }
        public Stretch BackgroundStretch { get => Stretch.Uniform; }
    }
}
