using System.Windows;
using System.Windows.Media;

namespace AppUI.Deck.Themes
{
    /// <summary>
    /// Deck mode light theme. Flat palette from SPEC.md; no background image.
    /// The focus accent is a separate fixed resource (see DeckShellWindow resources),
    /// intentionally not part of ITheme.
    /// </summary>
    public class DeckLightTheme : Classes.Themes.ITheme
    {
        public string Name { get => "Deck light"; }

        public string PrimaryAppBackground { get => "#F5F4F2"; }
        public string SecondaryAppBackground { get => "#FFFFFF"; }
        public string PrimaryControlBackground { get => "#FFFFFF"; }
        public string PrimaryControlForeground { get => "#1A1A18"; }
        public string PrimaryControlSecondary { get => "#6B6A66"; }
        public string PrimaryControlPressed { get => "#E8E6E1"; }
        public string PrimaryControlMouseOver { get => "#EFEDE8"; }
        public string PrimaryControlDisabledBackground { get => "#F0EFEC"; }
        public string PrimaryControlDisabledForeground { get => "#B5B3AE"; }

        public string BackgroundImageName { get => null; }
        public string BackgroundImageBase64 { get => null; }
        public HorizontalAlignment BackgroundHorizontalAlignment { get => HorizontalAlignment.Center; }
        public VerticalAlignment BackgroundVerticalAlignment { get => VerticalAlignment.Center; }
        public Stretch BackgroundStretch { get => Stretch.Uniform; }
    }
}
