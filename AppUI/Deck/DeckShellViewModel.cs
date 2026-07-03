using AppUI.Classes;
using AppUI.Deck.Input;
using AppUI.ViewModels;
using Iros.Workshop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace AppUI.Deck
{
    /// <summary>
    /// ViewModel for the Deck dashboard shell. Wraps the existing
    /// <see cref="MainWindowViewModel"/> (and through it <see cref="MyModsViewModel"/>);
    /// all navigation arrives as logical <see cref="DeckCommand"/>s from the router.
    /// </summary>
    public class DeckShellViewModel : ViewModelBase, IDeckCommandHandler
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private const int PageJumpSize = 10;

        public enum FocusArea
        {
            Sidebar,
            ModList,
        }

        private FocusArea _focusArea = FocusArea.Sidebar;
        private int _currentSectionIndex = 1; // start on My mods
        private int _focusedModIndex = -1;
        private InstalledModViewModel _focusedMod;
        private List<DeckLegendItem> _legendItems = new List<DeckLegendItem>();

        private bool _isLaunching;
        private bool _launchFailed;
        private string _launchStatusLog = "";
        private GameLaunchViewModel _launchViewModel;

        private bool _isReorderMode;
        private Guid _reorderingModId;
        private int _reorderOriginalIndex;

        private Options.DeckModOptionsViewModel _activeOptionsScreen;

        public MainWindowViewModel Main { get; }

        public Catalog.DeckCatalogViewModel CatalogSection { get; }

        public ObservableCollection<DeckSectionItemViewModel> Sections { get; }

        public DeckShellViewModel(MainWindowViewModel main)
        {
            Main = main;

            Sections = new ObservableCollection<DeckSectionItemViewModel>()
            {
                new DeckSectionItemViewModel(DeckSection.Play, "Play"),
                new DeckSectionItemViewModel(DeckSection.MyMods, "My mods"),
                new DeckSectionItemViewModel(DeckSection.BrowseCatalog, "Browse catalog"),
                new DeckSectionItemViewModel(DeckSection.LoadOrder, "Load order"),
                new DeckSectionItemViewModel(DeckSection.Settings, "Settings"),
            };

            Main.MyMods.PropertyChanged += MyMods_PropertyChanged;

            CatalogSection = new Catalog.DeckCatalogViewModel(main.CatalogMods);
            CatalogSection.PropertyChanged += CatalogSection_PropertyChanged;

            UpdateSectionFlags();
            RebuildLegend();
        }

        private void CatalogSection_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Catalog.DeckCatalogViewModel.LegendItems))
            {
                RebuildLegend();
            }
            else if (e.PropertyName == nameof(Catalog.DeckCatalogViewModel.IsSearchOverlayOpen))
            {
                NotifyPropertyChanged(nameof(IsTextEntryActive));
            }
        }

        /// <summary>The window watches this to switch the keyboard source into text-entry mode.</summary>
        public bool IsTextEntryActive
        {
            get { return CatalogSection.IsSearchOverlayOpen; }
        }

        /// <summary>
        /// Call after <see cref="MainWindowViewModel.InitViewModel"/> has populated the mod list.
        /// </summary>
        public void OnDataReady()
        {
            if (Main.MyMods.ModList.Any() && FocusedModIndex < 0)
            {
                FocusedModIndex = 0;
            }
        }

        public DeckSection CurrentSection
        {
            get { return Sections[_currentSectionIndex].Section; }
        }

        public FocusArea CurrentFocusArea
        {
            get { return _focusArea; }
            private set
            {
                _focusArea = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(IsModListFocused));
                UpdateSectionFlags();
                UpdateCatalogFocus();
                RebuildLegend();
            }
        }

        private void UpdateCatalogFocus()
        {
            if (CatalogSection != null)
            {
                CatalogSection.IsContentFocused =
                    (_focusArea == FocusArea.ModList && CurrentSection == DeckSection.BrowseCatalog);
            }
        }

        public bool IsModListFocused
        {
            get { return _focusArea == FocusArea.ModList; }
        }

        public int FocusedModIndex
        {
            get { return _focusedModIndex; }
            set
            {
                _focusedModIndex = value;
                NotifyPropertyChanged();
                RefreshFocusedMod();
            }
        }

        public InstalledModViewModel FocusedMod
        {
            get { return _focusedMod; }
            private set
            {
                _focusedMod = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(HasFocusedMod));
            }
        }

        public bool HasFocusedMod
        {
            get { return _focusedMod != null; }
        }

        public bool IsModListVisible
        {
            get { return CurrentSection == DeckSection.MyMods; }
        }

        public bool IsPlaySectionVisible
        {
            get { return CurrentSection == DeckSection.Play; }
        }

        public bool IsCatalogVisible
        {
            get { return CurrentSection == DeckSection.BrowseCatalog; }
        }

        public bool IsPlaceholderVisible
        {
            get { return !IsModListVisible && !IsPlaySectionVisible && !IsCatalogVisible; }
        }

        public string PlaceholderText
        {
            get
            {
                switch (CurrentSection)
                {
                    case DeckSection.LoadOrder: return "Load order is coming in a later milestone.";
                    case DeckSection.Settings: return "Settings is coming in a later milestone.";
                    default: return "";
                }
            }
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

        /// <summary>Non-null while the mod options takeover is open; it receives all commands.</summary>
        public Options.DeckModOptionsViewModel ActiveOptionsScreen
        {
            get { return _activeOptionsScreen; }
            private set
            {
                _activeOptionsScreen = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(IsOptionsScreenActive));
            }
        }

        public bool IsOptionsScreenActive
        {
            get { return _activeOptionsScreen != null; }
        }

        /// <summary>True while the focused row is "lifted" and up/down move it in the load order.</summary>
        public bool IsReorderMode
        {
            get { return _isReorderMode; }
            private set
            {
                _isReorderMode = value;
                NotifyPropertyChanged();
            }
        }

        #region Launch state

        public bool IsLaunching
        {
            get { return _isLaunching; }
            private set
            {
                _isLaunching = value;
                NotifyPropertyChanged();
            }
        }

        public bool LaunchFailed
        {
            get { return _launchFailed; }
            private set
            {
                _launchFailed = value;
                NotifyPropertyChanged();
            }
        }

        public string LaunchStatusLog
        {
            get { return _launchStatusLog; }
            private set
            {
                _launchStatusLog = value;
                NotifyPropertyChanged();
            }
        }

        #endregion

        public bool HandleCommand(DeckCommand command)
        {
            // while the launch overlay is up it owns all input
            if (IsLaunching)
            {
                if (command == DeckCommand.Back && LaunchFailed)
                {
                    DismissLaunchOverlay();
                }

                return true;
            }

            // the options takeover owns all input while open
            if (ActiveOptionsScreen != null)
            {
                return ActiveOptionsScreen.HandleCommand(command);
            }

            // a lifted row owns navigation until dropped or cancelled
            if (IsReorderMode)
            {
                return HandleReorderModeCommand(command);
            }

            // catalog content handles its own two-column navigation; commands it
            // declines (sections, play, back at its root) fall through to the shell
            if (CurrentSection == DeckSection.BrowseCatalog
                && (CurrentFocusArea == FocusArea.ModList || CatalogSection.IsSearchOverlayOpen)
                && CatalogSection.HandleCommand(command))
            {
                return true;
            }

            switch (command)
            {
                case DeckCommand.SectionPrev:
                    MoveSection(-1);
                    return true;

                case DeckCommand.SectionNext:
                    MoveSection(1);
                    return true;

                case DeckCommand.NavigateUp:
                    return HandleNavigateVertical(-1);

                case DeckCommand.NavigateDown:
                    return HandleNavigateVertical(1);

                case DeckCommand.NavigateLeft:
                    if (CurrentFocusArea == FocusArea.ModList)
                    {
                        CurrentFocusArea = FocusArea.Sidebar;
                    }
                    return true;

                case DeckCommand.NavigateRight:
                    TryFocusModList();
                    return true;

                case DeckCommand.Activate:
                    return HandleActivate();

                case DeckCommand.Back:
                    if (CurrentFocusArea == FocusArea.ModList)
                    {
                        CurrentFocusArea = FocusArea.Sidebar;
                    }
                    return true;

                case DeckCommand.PageUp:
                    return HandlePageJump(-PageJumpSize);

                case DeckCommand.PageDown:
                    return HandlePageJump(PageJumpSize);

                case DeckCommand.PlayShort:
                    StartLaunch();
                    return true;

                case DeckCommand.ReorderToggle:
                    EnterReorderMode();
                    return true;

                case DeckCommand.OpenOptions:
                    OpenModOptions();
                    return true;

                // deferred to later milestones; consumed so nothing else reacts
                case DeckCommand.PlayLong:
                case DeckCommand.Search:
                    Logger.Info($"Deck command {command} is not implemented in this milestone");
                    return true;

                default:
                    return false;
            }
        }

        private void MoveSection(int change)
        {
            int count = Sections.Count;
            _currentSectionIndex = (_currentSectionIndex + change + count) % count;

            CurrentFocusArea = FocusArea.Sidebar;
            NotifySectionChanged();
        }

        private bool HandleNavigateVertical(int change)
        {
            if (CurrentFocusArea == FocusArea.Sidebar)
            {
                int target = _currentSectionIndex + change;

                if (target >= 0 && target < Sections.Count)
                {
                    _currentSectionIndex = target;
                    NotifySectionChanged();
                    UpdateSectionFlags();
                }

                return true;
            }

            MoveModFocus(change);
            return true;
        }

        private bool HandlePageJump(int change)
        {
            if (CurrentFocusArea == FocusArea.ModList)
            {
                MoveModFocus(change);
            }

            return true;
        }

        private void MoveModFocus(int change)
        {
            int count = Main.MyMods.ModList.Count;

            if (count == 0)
            {
                return;
            }

            int target = Math.Max(0, Math.Min(count - 1, FocusedModIndex + change));
            FocusedModIndex = target;
        }

        private bool HandleActivate()
        {
            if (CurrentFocusArea == FocusArea.Sidebar)
            {
                if (CurrentSection == DeckSection.Play)
                {
                    StartLaunch();
                }
                else
                {
                    TryFocusModList();
                }

                return true;
            }

            ToggleFocusedMod();
            return true;
        }

        private void TryFocusModList()
        {
            if (CurrentSection == DeckSection.MyMods && Main.MyMods.ModList.Any())
            {
                if (FocusedModIndex < 0)
                {
                    FocusedModIndex = 0;
                }

                CurrentFocusArea = FocusArea.ModList;
            }
            else if (CurrentSection == DeckSection.BrowseCatalog && CatalogSection.OnFocusEntered())
            {
                CurrentFocusArea = FocusArea.ModList;
            }
        }

        private void ToggleFocusedMod()
        {
            InstalledModViewModel mod = FocusedMod;

            if (mod == null)
            {
                return;
            }

            int index = FocusedModIndex;
            Main.MyMods.ToggleActivateMod(mod.InstallInfo.ModID);

            // reload replaced the collection; restore focus to the same row
            int count = Main.MyMods.ModList.Count;
            FocusedModIndex = Math.Max(0, Math.Min(count - 1, index));
        }

        #region Mod options screen

        private void OpenModOptions()
        {
            if (CurrentFocusArea != FocusArea.ModList || FocusedMod == null)
            {
                return;
            }

            if (FocusedMod.ActiveModInfo == null)
            {
                Sys.Message(new WMessage("Activate the mod before configuring its options", true));
                return;
            }

            Options.DeckModOptionAccess access = Options.DeckModOptionAccess.TryCreate(FocusedMod);

            if (access == null)
            {
                return; // TryCreate already reported why via Sys.Message
            }

            Logger.Info($"Deck mode: opening options for {FocusedMod.Name}");

            var optionsScreen = new Options.DeckModOptionsViewModel(FocusedMod, access, onClosed: CloseModOptions);
            optionsScreen.PropertyChanged += OptionsScreen_PropertyChanged;

            ActiveOptionsScreen = optionsScreen;
            RebuildLegend();
        }

        private void CloseModOptions()
        {
            if (_activeOptionsScreen != null)
            {
                _activeOptionsScreen.PropertyChanged -= OptionsScreen_PropertyChanged;
            }

            ActiveOptionsScreen = null;
            RebuildLegend();
        }

        private void OptionsScreen_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Options.DeckModOptionsViewModel.LegendItems))
            {
                RebuildLegend();
            }
        }

        #endregion

        #region Reorder mode

        private void EnterReorderMode()
        {
            if (CurrentFocusArea != FocusArea.ModList || FocusedMod == null)
            {
                return;
            }

            _reorderingModId = FocusedMod.InstallInfo.ModID;
            _reorderOriginalIndex = FocusedModIndex;
            IsReorderMode = true;
            RebuildLegend();
        }

        private bool HandleReorderModeCommand(DeckCommand command)
        {
            switch (command)
            {
                case DeckCommand.NavigateUp:
                    MoveReorderingMod(-1);
                    return true;

                case DeckCommand.NavigateDown:
                    MoveReorderingMod(1);
                    return true;

                case DeckCommand.ReorderToggle:
                case DeckCommand.Activate:
                    // drop: ReorderProfileItem already persisted each move to the profile
                    ExitReorderMode();
                    return true;

                case DeckCommand.Back:
                    CancelReorder();
                    return true;

                default:
                    // everything else is inert while a row is lifted
                    return true;
            }
        }

        private void MoveReorderingMod(int change)
        {
            InstalledModViewModel mod = FindReorderingMod();

            if (mod == null)
            {
                // the mod vanished (e.g. removed from filesystem mid-reorder)
                ExitReorderMode();
                return;
            }

            Main.MyMods.ReorderProfileItem(mod, change);
            FollowReorderingMod();
        }

        private void CancelReorder()
        {
            InstalledModViewModel mod = FindReorderingMod();

            if (mod != null)
            {
                int currentIndex = Main.MyMods.ModList.IndexOf(mod);
                int delta = _reorderOriginalIndex - currentIndex;

                if (delta != 0)
                {
                    Main.MyMods.ReorderProfileItem(mod, delta);
                }

                FollowReorderingMod();
            }

            ExitReorderMode();
        }

        private void ExitReorderMode()
        {
            IsReorderMode = false;
            RebuildLegend();
        }

        /// <summary>
        /// Reload recreates the row viewmodels, so the lifted mod is tracked by id.
        /// </summary>
        private InstalledModViewModel FindReorderingMod()
        {
            return Main.MyMods.ModList.FirstOrDefault(m => m.InstallInfo.ModID == _reorderingModId);
        }

        private void FollowReorderingMod()
        {
            InstalledModViewModel mod = FindReorderingMod();

            if (mod != null)
            {
                FocusedModIndex = Main.MyMods.ModList.IndexOf(mod);
            }
        }

        #endregion

        private void NotifySectionChanged()
        {
            NotifyPropertyChanged(nameof(CurrentSection));
            NotifyPropertyChanged(nameof(IsModListVisible));
            NotifyPropertyChanged(nameof(IsPlaySectionVisible));
            NotifyPropertyChanged(nameof(IsCatalogVisible));
            NotifyPropertyChanged(nameof(IsPlaceholderVisible));
            NotifyPropertyChanged(nameof(PlaceholderText));
            UpdateCatalogFocus();
            RebuildLegend();
        }

        private void UpdateSectionFlags()
        {
            for (int i = 0; i < Sections.Count; i++)
            {
                Sections[i].IsCurrent = (i == _currentSectionIndex);
                Sections[i].IsFocused = (i == _currentSectionIndex && CurrentFocusArea == FocusArea.Sidebar);
            }
        }

        private void MyMods_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MyModsViewModel.ModList))
            {
                // collection instance was replaced (reload); keep focus index in range
                int count = Main.MyMods.ModList.Count;
                FocusedModIndex = count == 0 ? -1 : Math.Max(0, Math.Min(count - 1, FocusedModIndex));
            }
        }

        private void RefreshFocusedMod()
        {
            var list = Main.MyMods.ModList;

            FocusedMod = (_focusedModIndex >= 0 && _focusedModIndex < list.Count)
                ? list[_focusedModIndex]
                : null;
        }

        private void RebuildLegend()
        {
            var items = new List<DeckLegendItem>();

            if (ActiveOptionsScreen != null)
            {
                LegendItems = ActiveOptionsScreen.LegendItems;
                return;
            }

            if (!IsLaunching && CurrentSection == DeckSection.BrowseCatalog
                && (CurrentFocusArea == FocusArea.ModList || CatalogSection.IsSearchOverlayOpen))
            {
                LegendItems = CatalogSection.LegendItems;
                return;
            }

            if (IsLaunching)
            {
                if (LaunchFailed)
                {
                    items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Dismiss"));
                }
            }
            else if (IsReorderMode)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move mod"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Drop"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"));
            }
            else if (CurrentFocusArea == FocusArea.ModList)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Toggle mod"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Reorder, "Reorder"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Options, "Options"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Back"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Sections, "Section"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Play, "Play"));
            }
            else
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, CurrentSection == DeckSection.Play ? "Play" : "Select"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Sections, "Section"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Play, "Play"));
            }

            LegendItems = items;
        }

        #region Launch flow

        /// <summary>
        /// Launches the game with default flags by driving <see cref="GameLaunchViewModel"/> /
        /// <see cref="GameLauncher"/> directly, showing progress inline. Deliberately does not
        /// call <see cref="MainWindowViewModel.LaunchGame"/> (that opens the desktop window).
        /// </summary>
        private void StartLaunch()
        {
            if (IsLaunching)
            {
                return;
            }

            Logger.Info("Deck mode: starting game launch (default flags)");

            LaunchFailed = false;
            LaunchStatusLog = "Preparing…\n";
            IsLaunching = true;
            RebuildLegend();

            _launchViewModel = new GameLaunchViewModel(variableDump: false, debugLogging: false);

            // subscribe directly: GameLaunchViewModel.StatusLog is only appended to when
            // the ShowLauncherWindow setting is on, so it can't be relied on here
            GameLauncher.Instance.ProgressChanged += Launch_ProgressChanged;

            _launchViewModel.BeginLaunchProcessAsync().ContinueWith(result =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    GameLauncher.Instance.ProgressChanged -= Launch_ProgressChanged;

                    bool didLaunch = false;

                    if (result.IsFaulted)
                    {
                        Logger.Error(result.Exception);
                        AppendToLaunchLog($"Unexpected error: {result.Exception.GetBaseException().Message}");
                    }
                    else if (!result.IsCanceled)
                    {
                        didLaunch = result.Result;
                    }

                    if (didLaunch)
                    {
                        IsLaunching = false;
                    }
                    else
                    {
                        AppendToLaunchLog("Launch did not complete. Press back to dismiss.");
                        LaunchFailed = true;
                    }

                    RebuildLegend();
                });
            });
        }

        private void DismissLaunchOverlay()
        {
            IsLaunching = false;
            LaunchFailed = false;
            RebuildLegend();
        }

        private void Launch_ProgressChanged(string message)
        {
            App.Current.Dispatcher.Invoke(() => AppendToLaunchLog(message));
        }

        private void AppendToLaunchLog(string message)
        {
            LaunchStatusLog += $"{message}\n";
        }

        #endregion
    }
}
