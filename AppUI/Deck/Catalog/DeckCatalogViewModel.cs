using AppUI.Classes;
using AppUI.Deck.Input;
using AppUI.ViewModels;
using Iros.Workshop;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace AppUI.Deck.Catalog
{
    /// <summary>
    /// Deck catalog browsing over the existing <see cref="CatalogViewModel"/>:
    /// dynamic category column on the left, mods within the selected category in
    /// the middle, details with an install action on the right. Y opens a free-text
    /// search overlay. Filtering is done here from the loaded catalog snapshot —
    /// the desktop's stateful reload/filter path is left untouched.
    /// </summary>
    public class DeckCatalogViewModel : ViewModelBase, IDeckCommandHandler
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private const int PageJumpSize = 10;

        public enum CatalogColumn
        {
            Categories,
            Mods,
        }

        private readonly CatalogViewModel _catalog;

        private List<CatalogModItemViewModel> _allMods = new List<CatalogModItemViewModel>();
        private List<DeckCatalogCategoryViewModel> _categories = new List<DeckCatalogCategoryViewModel>();
        private List<CatalogModItemViewModel> _mods = new List<CatalogModItemViewModel>();

        private CatalogColumn _focusedColumn = CatalogColumn.Categories;
        private int _focusedCategoryIndex = -1;
        private int _focusedModIndex = -1;
        private bool _isContentFocused;

        private bool _isSearchOverlayOpen;
        private string _searchText = "";
        private string _activeSearch = "";

        private List<DeckLegendItem> _legendItems = new List<DeckLegendItem>();

        public DeckCatalogViewModel(CatalogViewModel catalog)
        {
            _catalog = catalog;
            _catalog.PropertyChanged += Catalog_PropertyChanged;

            RefreshFromCatalog();
            RebuildLegend();
        }

        #region Bindable state

        /// <summary>The wrapped desktop viewmodel; the view binds its DownloadList for progress.</summary>
        public CatalogViewModel CatalogSource
        {
            get { return _catalog; }
        }

        public List<DeckCatalogCategoryViewModel> Categories
        {
            get { return _categories; }
            private set
            {
                _categories = value;
                NotifyPropertyChanged();
            }
        }

        public List<CatalogModItemViewModel> Mods
        {
            get { return _mods; }
            private set
            {
                _mods = value;
                NotifyPropertyChanged();
            }
        }

        public int FocusedCategoryIndex
        {
            get { return _focusedCategoryIndex; }
            set
            {
                _focusedCategoryIndex = value;
                NotifyPropertyChanged();

                if (!IsSearchActive)
                {
                    ApplyCategoryFilter();
                }
            }
        }

        public int FocusedModIndex
        {
            get { return _focusedModIndex; }
            set
            {
                _focusedModIndex = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(FocusedCatalogMod));
                NotifyPropertyChanged(nameof(HasFocusedCatalogMod));
            }
        }

        public CatalogModItemViewModel FocusedCatalogMod
        {
            get
            {
                return (_focusedModIndex >= 0 && _focusedModIndex < _mods.Count)
                    ? _mods[_focusedModIndex]
                    : null;
            }
        }

        public bool HasFocusedCatalogMod
        {
            get { return FocusedCatalogMod != null; }
        }

        /// <summary>Set by the shell while navigation focus is inside the catalog content.</summary>
        public bool IsContentFocused
        {
            get { return _isContentFocused; }
            set
            {
                _isContentFocused = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(IsCategoryColumnFocused));
                NotifyPropertyChanged(nameof(IsModColumnFocused));
                RebuildLegend();
            }
        }

        public bool IsCategoryColumnFocused
        {
            get { return _isContentFocused && _focusedColumn == CatalogColumn.Categories; }
        }

        public bool IsModColumnFocused
        {
            get { return _isContentFocused && _focusedColumn == CatalogColumn.Mods; }
        }

        public bool IsSearchOverlayOpen
        {
            get { return _isSearchOverlayOpen; }
            private set
            {
                _isSearchOverlayOpen = value;
                NotifyPropertyChanged();
                RebuildLegend();
            }
        }

        public string SearchText
        {
            get { return _searchText; }
            set
            {
                _searchText = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>Non-empty while the mod column shows search results instead of a category.</summary>
        public bool IsSearchActive
        {
            get { return !string.IsNullOrWhiteSpace(_activeSearch); }
        }

        public string ModsHeader
        {
            get
            {
                if (IsSearchActive)
                {
                    return $"Search: {_activeSearch} ({_mods.Count})";
                }

                DeckCatalogCategoryViewModel category = (_focusedCategoryIndex >= 0 && _focusedCategoryIndex < _categories.Count)
                    ? _categories[_focusedCategoryIndex]
                    : null;

                return category?.Name ?? "";
            }
        }

        public bool IsCatalogEmpty
        {
            get { return !_allMods.Any(); }
        }

        public List<DeckLegendItem> LegendItems
        {
            get { return _legendItems; }
            private set
            {
                _legendItems = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        /// <summary>Called by the shell when navigation focus enters the catalog content.</summary>
        public bool OnFocusEntered()
        {
            if (IsCatalogEmpty)
            {
                return false;
            }

            _focusedColumn = CatalogColumn.Categories;

            if (FocusedCategoryIndex < 0 && _categories.Any())
            {
                FocusedCategoryIndex = 0;
            }

            NotifyColumnFocusChanged();
            return true;
        }

        /// <summary>
        /// Handles a command while catalog content has focus. Returns false for
        /// commands the shell should keep handling (sections, play, back at the root).
        /// </summary>
        public bool HandleCommand(DeckCommand command)
        {
            if (IsSearchOverlayOpen)
            {
                if (command == DeckCommand.Back)
                {
                    IsSearchOverlayOpen = false;
                }
                else if (command == DeckCommand.Activate)
                {
                    ApplySearch();
                }

                // swallow everything while typing
                return true;
            }

            switch (command)
            {
                case DeckCommand.NavigateUp:
                    // at the top of the focused column the shell takes over (top bar)
                    if (FocusedIndexOfColumn() <= 0)
                    {
                        return false;
                    }

                    MoveFocus(-1);
                    return true;

                case DeckCommand.NavigateDown:
                    MoveFocus(1);
                    return true;

                case DeckCommand.PageUp:
                    MoveFocus(-PageJumpSize);
                    return true;

                case DeckCommand.PageDown:
                    MoveFocus(PageJumpSize);
                    return true;

                case DeckCommand.NavigateRight:
                    FocusModColumn();
                    return true;

                case DeckCommand.NavigateLeft:
                    if (_focusedColumn == CatalogColumn.Mods)
                    {
                        FocusCategoryColumn();
                        return true;
                    }

                    return false; // let the shell move focus to the sidebar

                case DeckCommand.Activate:
                    if (_focusedColumn == CatalogColumn.Categories)
                    {
                        FocusModColumn();
                    }
                    else
                    {
                        InstallFocusedMod();
                    }

                    return true;

                case DeckCommand.Back:
                    if (_focusedColumn == CatalogColumn.Mods)
                    {
                        if (IsSearchActive)
                        {
                            ClearSearch();
                        }

                        FocusCategoryColumn();
                        return true;
                    }

                    if (IsSearchActive)
                    {
                        ClearSearch();
                        return true;
                    }

                    return false; // at the root: shell sends focus to the sidebar

                case DeckCommand.Search:
                    OpenSearchOverlay();
                    return true;

                default:
                    return false; // sections, play etc. stay with the shell
            }
        }

        private int FocusedIndexOfColumn()
        {
            return _focusedColumn == CatalogColumn.Categories ? FocusedCategoryIndex : FocusedModIndex;
        }

        private void MoveFocus(int change)
        {
            if (_focusedColumn == CatalogColumn.Categories)
            {
                if (!_categories.Any())
                {
                    return;
                }

                FocusedCategoryIndex = Math.Max(0, Math.Min(_categories.Count - 1, FocusedCategoryIndex + change));
            }
            else
            {
                if (!_mods.Any())
                {
                    return;
                }

                FocusedModIndex = Math.Max(0, Math.Min(_mods.Count - 1, FocusedModIndex + change));
            }
        }

        private void FocusModColumn()
        {
            if (!_mods.Any())
            {
                return;
            }

            _focusedColumn = CatalogColumn.Mods;

            if (FocusedModIndex < 0)
            {
                FocusedModIndex = 0;
            }

            NotifyColumnFocusChanged();
        }

        private void FocusCategoryColumn()
        {
            _focusedColumn = CatalogColumn.Categories;
            NotifyColumnFocusChanged();
        }

        private void NotifyColumnFocusChanged()
        {
            NotifyPropertyChanged(nameof(IsCategoryColumnFocused));
            NotifyPropertyChanged(nameof(IsModColumnFocused));
            RebuildLegend();
        }

        private void InstallFocusedMod()
        {
            CatalogModItemViewModel mod = FocusedCatalogMod;

            if (mod == null)
            {
                return;
            }

            // pre-check the states the desktop reports via modal dialogs
            ModStatus status = Sys.GetStatus(mod.Mod.ID);

            if (status == ModStatus.Downloading || status == ModStatus.Updating)
            {
                Sys.Message(new WMessage($"{mod.Name} is already downloading", true));
                return;
            }

            if (status == ModStatus.Installed)
            {
                InstalledItem installed = Sys.Library.GetItem(mod.Mod.ID);

                if (installed != null && !installed.IsUpdateAvailable)
                {
                    Sys.Message(new WMessage($"{mod.Name} is already installed", true));
                    return;
                }
            }

            Logger.Info($"Deck mode: downloading {mod.Name} from catalog");
            _catalog.DownloadMod(mod);
        }

        #region Search

        private void OpenSearchOverlay()
        {
            SearchText = _activeSearch;
            IsSearchOverlayOpen = true;
        }

        private void ApplySearch()
        {
            IsSearchOverlayOpen = false;

            string text = SearchText?.Trim() ?? "";

            if (text.Length == 0)
            {
                ClearSearch();
                return;
            }

            _activeSearch = text;

            Mods = _allMods.Where(m => m.Mod.SearchRelevance(text) > 0)
                           .OrderBy(m => m.Name)
                           .ToList();

            FocusedModIndex = _mods.Any() ? 0 : -1;

            if (_mods.Any())
            {
                _focusedColumn = CatalogColumn.Mods;
            }

            NotifyPropertyChanged(nameof(IsSearchActive));
            NotifyPropertyChanged(nameof(ModsHeader));
            NotifyColumnFocusChanged();
        }

        private void ClearSearch()
        {
            _activeSearch = "";
            SearchText = "";
            NotifyPropertyChanged(nameof(IsSearchActive));
            ApplyCategoryFilter();
        }

        #endregion

        private void Catalog_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CatalogViewModel.CatalogModList))
            {
                App.Current.Dispatcher.Invoke(RefreshFromCatalog);
            }
        }

        /// <summary>
        /// Re-projects categories and the current filter from the loaded catalog,
        /// preserving the focused category by name where possible.
        /// </summary>
        private void RefreshFromCatalog()
        {
            string previousCategory = (_focusedCategoryIndex >= 0 && _focusedCategoryIndex < _categories.Count)
                ? _categories[_focusedCategoryIndex].Name
                : null;

            _allMods = _catalog.CatalogModList.ToList();

            Categories = _allMods.GroupBy(m => m.Category)
                                 .OrderBy(g => g.Key)
                                 .Select(g => new DeckCatalogCategoryViewModel(g.Key, g.Count()))
                                 .ToList();

            int restoredIndex = previousCategory != null
                ? Categories.FindIndex(c => c.Name == previousCategory)
                : -1;

            if (restoredIndex < 0 && Categories.Any())
            {
                restoredIndex = 0;
            }

            // setting the index re-applies the category filter
            FocusedCategoryIndex = restoredIndex;

            if (IsSearchActive)
            {
                ApplySearch();
            }

            NotifyPropertyChanged(nameof(IsCatalogEmpty));
        }

        private void ApplyCategoryFilter()
        {
            DeckCatalogCategoryViewModel category = (_focusedCategoryIndex >= 0 && _focusedCategoryIndex < _categories.Count)
                ? _categories[_focusedCategoryIndex]
                : null;

            Mods = category != null
                ? _allMods.Where(m => m.Category == category.Name).OrderBy(m => m.Name).ToList()
                : new List<CatalogModItemViewModel>();

            FocusedModIndex = _mods.Any() ? 0 : -1;
            NotifyPropertyChanged(nameof(ModsHeader));
        }

        private void RebuildLegend()
        {
            var items = new List<DeckLegendItem>();

            if (IsSearchOverlayOpen)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Search"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"));
            }
            else if (_focusedColumn == CatalogColumn.Mods)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Install"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Search, "Search"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Back"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Sections, "Section"));
            }
            else
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Open category"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Search, "Search"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Back"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Sections, "Section"));
            }

            LegendItems = items;
        }
    }
}
