using System.Windows;
using System.Windows.Media;

namespace AppUI.Deck.Themes
{
    /// <summary>
    /// Theme styled after the original Final Fantasy VII menus: deep blue window
    /// fills on a near-black page, white text, grey for disabled entries, with the
    /// game's teal-green label colour used as the Deck accent (set alongside theme
    /// application). The game's boxes use a vertical blue gradient and silver
    /// bevelled borders — approximated flat here to stay within the ITheme palette.
    /// </summary>
    public class DeckFF7Theme : Classes.Themes.ITheme
    {
        public string Name { get => "Final Fantasy VII"; }

        public string PrimaryAppBackground { get => "#000420"; }
        public string SecondaryAppBackground { get => "#0A1858"; }
        public string PrimaryControlBackground { get => "#0E1A66"; }
        public string PrimaryControlForeground { get => "#F2F2F2"; }
        public string PrimaryControlSecondary { get => "#A0AEDC"; }
        public string PrimaryControlPressed { get => "#22308C"; }
        public string PrimaryControlMouseOver { get => "#1A2680"; }
        public string PrimaryControlDisabledBackground { get => "#0A1448"; }
        public string PrimaryControlDisabledForeground { get => "#8A8A96"; }

        public string BackgroundImageName { get => null; }
        public string BackgroundImageBase64 { get => null; }
        public HorizontalAlignment BackgroundHorizontalAlignment { get => HorizontalAlignment.Center; }
        public VerticalAlignment BackgroundVerticalAlignment { get => VerticalAlignment.Center; }
        public Stretch BackgroundStretch { get => Stretch.Uniform; }
    }
}
