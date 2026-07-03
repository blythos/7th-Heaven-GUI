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

        public DeckCatalogCategoryViewModel(string name, int modCount)
        {
            Name = name;
            ModCount = modCount;
        }
    }
}
