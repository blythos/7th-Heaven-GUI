using AppUI.ViewModels;

namespace AppUI.Deck
{
    public enum DeckSection
    {
        Play,
        MyMods,
        BrowseCatalog,
        LoadOrder,
        Settings,
    }

    /// <summary>
    /// A sidebar entry in the Deck shell.
    /// </summary>
    public class DeckSectionItemViewModel : ViewModelBase
    {
        private bool _isCurrent;
        private bool _isFocused;

        public DeckSection Section { get; }
        public string Title { get; }

        /// <summary>The section whose content is shown (highlighted row).</summary>
        public bool IsCurrent
        {
            get { return _isCurrent; }
            set
            {
                _isCurrent = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>True when navigation focus is on this sidebar row (accent ring).</summary>
        public bool IsFocused
        {
            get { return _isFocused; }
            set
            {
                _isFocused = value;
                NotifyPropertyChanged();
            }
        }

        public DeckSectionItemViewModel(DeckSection section, string title)
        {
            Section = section;
            Title = title;
        }
    }
}
