using AppUI.ViewModels;

namespace AppUI.Deck.Catalog
{
    /// <summary>
    /// A category entry in the Deck catalog's left column, built dynamically from
    /// the distinct (translated) category strings present in the loaded catalog.
    /// </summary>
    public class DeckCatalogCategoryViewModel : ViewModelBase
    {
        public string Name { get; }
        public int ModCount { get; }

        /// <summary>Synthetic first entry showing the whole catalog unfiltered. Keyed by
        /// this flag (not the display name) so selection restore can never confuse it
        /// with a real category that happens to be called "All".</summary>
        public bool IsAll { get; }

        public DeckCatalogCategoryViewModel(string name, int modCount, bool isAll = false)
        {
            Name = name;
            ModCount = modCount;
            IsAll = isAll;
        }
    }
}
