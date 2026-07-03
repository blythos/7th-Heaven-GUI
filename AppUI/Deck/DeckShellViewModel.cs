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
            TopBar,
            Content,
        }

        private FocusArea _focusArea = FocusArea.TopBar;
        private int _currentSectionIndex = 0; // start on My mods
        private bool _isQuitPromptOpen;
        private int _focusedModIndex = -1;
        private InstalledModViewModel _focusedMod;
        private Uri _focusedModImageSource;
        private string _focusedModReleaseNotes;
        private string _focusedModLink;
        private List<DeckLegendItem> _legendItems = new List<DeckLegendItem>();

        private bool _isLaunching;
        private bool _launchFailed;
        private string _launchStatusLog = "";
        private GameLaunchViewModel _launchViewModel;

        private bool _isPlayConfirmOpen;
        private bool _isPlayPickerOpen;
        private int _focusedPlayVariantIndex;

        private static readonly (AppCore.DefaultPlayCommandOptions Command, string Label)[] PlayVariantList = new[]
        {
            (AppCore.DefaultPlayCommandOptions.PlayWithMods, "Play with mods"),
            (AppCore.DefaultPlayCommandOptions.PlayWithoutMods, "Play without mods"),
            (AppCore.DefaultPlayCommandOptions.PlayWithDebugLog, "Play with debug log"),
            (AppCore.DefaultPlayCommandOptions.PlayWithVariableDump, "Play with variable dump"),
        };

        private bool _isReorderMode;
        private Guid _reorderingModId;
        private int _reorderOriginalIndex;

        private Options.DeckModOptionsViewModel _activeOptionsScreen;

        public MainWindowViewModel Main { get; }

        public Catalog.DeckCatalogViewModel CatalogSection { get; }

        public Profiles.DeckProfilesViewModel ProfilesSection { get; }

        public Settings.DeckSettingsViewModel SettingsSection { get; }

        public ObservableCollection<DeckSectionItemViewModel> Sections { get; }

        public DeckShellViewModel(MainWindowViewModel main)
        {
            Main = main;

            Sections = new ObservableCollection<DeckSectionItemViewModel>()
            {
                new DeckSectionItemViewModel(DeckSection.MyMods, "Installed mods"),
                new DeckSectionItemViewModel(DeckSection.BrowseCatalog, "Browse catalog"),
                new DeckSectionItemViewModel(DeckSection.Profiles, "Profiles"),
                new DeckSectionItemViewModel(DeckSection.Settings, "Settings"),
            };

            Main.MyMods.PropertyChanged += MyMods_PropertyChanged;

            CatalogSection = new Catalog.DeckCatalogViewModel(main.CatalogMods);
            CatalogSection.PropertyChanged += CatalogSection_PropertyChanged;

            ProfilesSection = new Profiles.DeckProfilesViewModel(main);
            ProfilesSection.PropertyChanged += ProfilesSection_PropertyChanged;

            SettingsSection = new Settings.DeckSettingsViewModel(onQuitRequested: () => IsQuitPromptOpen = true);
            SettingsSection.PropertyChanged += SettingsSection_PropertyChanged;

            // rebuild every legend when the active input device (keyboard/pad) changes
            DeckGlyphs.GlyphSetChanged += OnGlyphSetChanged;

            UpdateSectionFlags();
            RebuildLegend();
        }

        private void OnGlyphSetChanged()
        {
            CatalogSection.RefreshLegend();
            ProfilesSection.RefreshLegend();
            SettingsSection.RefreshLegend();
            ActiveOptionsScreen?.RefreshLegend();
            RebuildLegend();
        }

        private void SettingsSection_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.DeckSettingsViewModel.LegendItems))
            {
                RebuildLegend();
            }
            else if (e.PropertyName == nameof(Settings.DeckSettingsViewModel.IsTextOverlayOpen))
            {
                NotifyPropertyChanged(nameof(IsTextEntryActive));
            }
        }

        private void ProfilesSection_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Profiles.DeckProfilesViewModel.LegendItems))
            {
                RebuildLegend();
            }
            else if (e.PropertyName == nameof(Profiles.DeckProfilesViewModel.IsTextOverlayOpen))
            {
                NotifyPropertyChanged(nameof(IsTextEntryActive));
            }
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
            get
            {
                return CatalogSection.IsSearchOverlayOpen
                    || SettingsSection.IsTextOverlayOpen
                    || ProfilesSection.IsTextOverlayOpen;
            }
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
                NotifyPropertyChanged(nameof(IsContentFocused));
                UpdateSectionFlags();
                UpdateContentFocusFlags();
                RebuildLegend();
            }
        }

        private void UpdateContentFocusFlags()
        {
            if (CatalogSection != null)
            {
                CatalogSection.IsContentFocused =
                    (_focusArea == FocusArea.Content && CurrentSection == DeckSection.BrowseCatalog);
            }

            if (ProfilesSection != null)
            {
                ProfilesSection.IsContentFocused =
                    (_focusArea == FocusArea.Content && CurrentSection == DeckSection.Profiles);
            }

            if (SettingsSection != null)
            {
                SettingsSection.IsContentFocused =
                    (_focusArea == FocusArea.Content && CurrentSection == DeckSection.Settings);
            }
        }

        public bool IsContentFocused
        {
            get { return _focusArea == FocusArea.Content; }
        }

        public bool IsQuitPromptOpen
        {
            get { return _isQuitPromptOpen; }
            private set
            {
                _isQuitPromptOpen = value;
                NotifyPropertyChanged();
                RebuildLegend();
            }
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

        public Uri FocusedModImageSource
        {
            get { return _focusedModImageSource; }
            private set
            {
                _focusedModImageSource = value;
                NotifyPropertyChanged();
            }
        }

        public string FocusedModReleaseNotes
        {
            get { return _focusedModReleaseNotes; }
            private set
            {
                _focusedModReleaseNotes = value;
                NotifyPropertyChanged();
            }
        }

        public string FocusedModLink
        {
            get { return _focusedModLink; }
            private set
            {
                _focusedModLink = value;
                NotifyPropertyChanged();
            }
        }

        public bool IsModListVisible
        {
            get { return CurrentSection == DeckSection.MyMods; }
        }

        public bool IsCatalogVisible
        {
            get { return CurrentSection == DeckSection.BrowseCatalog; }
        }

        public bool IsProfilesVisible
        {
            get { return CurrentSection == DeckSection.Profiles; }
        }

        public bool IsSettingsVisible
        {
            get { return CurrentSection == DeckSection.Settings; }
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

        public bool IsPlayConfirmOpen
        {
            get { return _isPlayConfirmOpen; }
            private set
            {
                _isPlayConfirmOpen = value;
                NotifyPropertyChanged();
                RebuildLegend();
            }
        }

        public string PlayConfirmText
        {
            get
            {
                switch (Sys.Settings.GameLaunchSettings.DefaultPlayCommand)
                {
                    case AppCore.DefaultPlayCommandOptions.PlayWithDebugLog:
                        return "Launch with debug logging? This slows the game down and writes large log files.";
                    case AppCore.DefaultPlayCommandOptions.PlayWithVariableDump:
                        return "Launch with variable dump? This is for advanced debugging.";
                    default:
                        return "";
                }
            }
        }

        public bool IsPlayPickerOpen
        {
            get { return _isPlayPickerOpen; }
            private set
            {
                _isPlayPickerOpen = value;
                NotifyPropertyChanged();
                RebuildLegend();
            }
        }

        public List<string> PlayVariants
        {
            get { return PlayVariantList.Select(v => v.Label).ToList(); }
        }

        public int FocusedPlayVariantIndex
        {
            get { return _focusedPlayVariantIndex; }
            set
            {
                _focusedPlayVariantIndex = value;
                NotifyPropertyChanged();
            }
        }

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

            if (IsQuitPromptOpen)
            {
                if (command == DeckCommand.Activate)
                {
                    Logger.Info("Deck mode: quitting via quit prompt");
                    App.ShutdownApp();
                }
                else if (command == DeckCommand.Back)
                {
                    IsQuitPromptOpen = false;
                }

                return true;
            }

            if (IsPlayConfirmOpen)
            {
                if (command == DeckCommand.Activate)
                {
                    IsPlayConfirmOpen = false;
                    BeginLaunch(Sys.Settings.GameLaunchSettings.DefaultPlayCommand);
                }
                else if (command == DeckCommand.Back)
                {
                    IsPlayConfirmOpen = false;
                }

                return true;
            }

            if (IsPlayPickerOpen)
            {
                HandlePlayPickerCommand(command);
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

            // section content handles its own navigation first; commands it declines
            // (sections, play, back/up at its root) fall through to the shell
            if (CurrentFocusArea == FocusArea.Content || CatalogSection.IsSearchOverlayOpen || SettingsSection.IsTextOverlayOpen)
            {
                if (CurrentSection == DeckSection.BrowseCatalog && CatalogSection.HandleCommand(command))
                {
                    return true;
                }

                if (CurrentSection == DeckSection.Profiles && ProfilesSection.HandleCommand(command))
                {
                    return true;
                }

                if (CurrentSection == DeckSection.Settings && SettingsSection.HandleCommand(command))
                {
                    return true;
                }
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
                    if (CurrentFocusArea == FocusArea.Content)
                    {
                        // My mods moves within its list; other sections reach here only
                        // when their content declined (top of list) — exit to the top bar
                        if (CurrentSection == DeckSection.MyMods && FocusedModIndex > 0)
                        {
                            MoveModFocus(-1);
                        }
                        else
                        {
                            CurrentFocusArea = FocusArea.TopBar;
                        }
                    }

                    return true;

                case DeckCommand.NavigateDown:
                    if (CurrentFocusArea == FocusArea.TopBar)
                    {
                        TryFocusContent();
                    }
                    else if (CurrentSection == DeckSection.MyMods)
                    {
                        MoveModFocus(1);
                    }

                    return true;

                case DeckCommand.NavigateLeft:
                    if (CurrentFocusArea == FocusArea.TopBar)
                    {
                        MoveSection(-1);
                    }

                    return true;

                case DeckCommand.NavigateRight:
                    if (CurrentFocusArea == FocusArea.TopBar)
                    {
                        MoveSection(1);
                    }

                    return true;

                case DeckCommand.Activate:
                    return HandleActivate();

                case DeckCommand.Back:
                    if (CurrentFocusArea == FocusArea.Content)
                    {
                        CurrentFocusArea = FocusArea.TopBar;
                    }
                    else
                    {
                        IsQuitPromptOpen = true;
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

                case DeckCommand.PlayLong:
                    OpenPlayPicker();
                    return true;

                // deferred to later milestones; consumed so nothing else reacts
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

            CurrentFocusArea = FocusArea.TopBar;
            NotifySectionChanged();
        }

        private bool HandlePageJump(int change)
        {
            if (CurrentFocusArea == FocusArea.Content && CurrentSection == DeckSection.MyMods)
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
            if (CurrentFocusArea == FocusArea.TopBar)
            {
                TryFocusContent();
                return true;
            }

            if (CurrentSection == DeckSection.MyMods)
            {
                ToggleFocusedMod();
            }

            return true;
        }

        private void TryFocusContent()
        {
            if (CurrentSection == DeckSection.MyMods && Main.MyMods.ModList.Any())
            {
                if (FocusedModIndex < 0)
                {
                    FocusedModIndex = 0;
                }

                CurrentFocusArea = FocusArea.Content;
            }
            else if (CurrentSection == DeckSection.BrowseCatalog && CatalogSection.OnFocusEntered())
            {
                CurrentFocusArea = FocusArea.Content;
            }
            else if (CurrentSection == DeckSection.Profiles && ProfilesSection.OnFocusEntered())
            {
                CurrentFocusArea = FocusArea.Content;
            }
            else if (CurrentSection == DeckSection.Settings && SettingsSection.OnFocusEntered())
            {
                CurrentFocusArea = FocusArea.Content;
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
            if (CurrentFocusArea != FocusArea.Content || CurrentSection != DeckSection.MyMods || FocusedMod == null)
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
            if (CurrentFocusArea != FocusArea.Content || CurrentSection != DeckSection.MyMods || FocusedMod == null)
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
            NotifyPropertyChanged(nameof(IsCatalogVisible));
            NotifyPropertyChanged(nameof(IsProfilesVisible));
            NotifyPropertyChanged(nameof(IsSettingsVisible));
            UpdateContentFocusFlags();
            RebuildLegend();
        }

        private void UpdateSectionFlags()
        {
            for (int i = 0; i < Sections.Count; i++)
            {
                Sections[i].IsCurrent = (i == _currentSectionIndex);
                Sections[i].IsFocused = (i == _currentSectionIndex && CurrentFocusArea == FocusArea.TopBar);
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

            // same detail sources the desktop preview pane uses
            Mod details = FocusedMod?.InstallInfo?.CachedDetails;

            FocusedModReleaseNotes = details?.LatestVersion?.ReleaseNotes;
            FocusedModLink = details?.Link;

            string imageUrl = details?.LatestVersion?.PreviewImage;
            string imagePath = string.IsNullOrWhiteSpace(imageUrl)
                ? null
                : Sys.ImageCache.GetImagePath(imageUrl, details.ID); // non-blocking; downloads in the background if uncached

            FocusedModImageSource = imagePath == null ? null : new Uri(imagePath);
        }

        /// <summary>Mouse entry point: clicking a top-bar tab switches to that section.</summary>
        public void SelectSectionViaMouse(DeckSection section)
        {
            if (IsLaunching || IsQuitPromptOpen || ActiveOptionsScreen != null || IsReorderMode)
            {
                return;
            }

            int index = Sections.IndexOf(Sections.FirstOrDefault(s => s.Section == section));

            if (index >= 0)
            {
                _currentSectionIndex = index;
                CurrentFocusArea = FocusArea.TopBar;
                NotifySectionChanged();
            }
        }

        /// <summary>Mouse entry point: clicking inside section content moves navigation focus there.</summary>
        public void FocusContentViaMouse()
        {
            if (IsLaunching || IsQuitPromptOpen || ActiveOptionsScreen != null)
            {
                return;
            }

            TryFocusContent();
        }

        private void RebuildLegend()
        {
            var items = new List<DeckLegendItem>();

            if (ActiveOptionsScreen != null)
            {
                LegendItems = ActiveOptionsScreen.LegendItems;
                return;
            }

            if (IsQuitPromptOpen)
            {
                LegendItems = new List<DeckLegendItem>()
                {
                    DeckGlyphs.Item(DeckLegendInput.Activate, "Quit"),
                    DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"),
                };
                return;
            }

            if (IsPlayConfirmOpen)
            {
                LegendItems = new List<DeckLegendItem>()
                {
                    DeckGlyphs.Item(DeckLegendInput.Activate, "Launch"),
                    DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"),
                };
                return;
            }

            if (IsPlayPickerOpen)
            {
                LegendItems = new List<DeckLegendItem>()
                {
                    DeckGlyphs.Item(DeckLegendInput.Move, "Move"),
                    DeckGlyphs.Item(DeckLegendInput.Activate, "Set default"),
                    DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"),
                };
                return;
            }

            if (!IsLaunching && CurrentSection == DeckSection.BrowseCatalog
                && (CurrentFocusArea == FocusArea.Content || CatalogSection.IsSearchOverlayOpen))
            {
                LegendItems = CatalogSection.LegendItems;
                return;
            }

            if (!IsLaunching && CurrentSection == DeckSection.Profiles
                && CurrentFocusArea == FocusArea.Content)
            {
                LegendItems = ProfilesSection.LegendItems;
                return;
            }

            if (!IsLaunching && CurrentSection == DeckSection.Settings
                && (CurrentFocusArea == FocusArea.Content || SettingsSection.IsTextOverlayOpen))
            {
                LegendItems = SettingsSection.LegendItems;
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
            else if (CurrentFocusArea == FocusArea.Content)
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
                items.Add(DeckGlyphs.Item(DeckLegendInput.LeftRight, "Section"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Select"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Quit"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Play, "Play"));
            }

            LegendItems = items;
        }

        #region Launch flow

        /// <summary>
        /// Play action: launches using the stored default play command, with an inline
        /// confirm first for the debug variants (the desktop pops a dialog for those).
        /// </summary>
        private void StartLaunch()
        {
            if (IsLaunching || IsPlayPickerOpen || IsPlayConfirmOpen)
            {
                return;
            }

            AppCore.DefaultPlayCommandOptions command = Sys.Settings.GameLaunchSettings.DefaultPlayCommand;

            if (command == AppCore.DefaultPlayCommandOptions.PlayWithDebugLog
                || command == AppCore.DefaultPlayCommandOptions.PlayWithVariableDump)
            {
                NotifyPropertyChanged(nameof(PlayConfirmText));
                IsPlayConfirmOpen = true;
                return;
            }

            BeginLaunch(command);
        }

        private void OpenPlayPicker()
        {
            if (IsLaunching)
            {
                return;
            }

            int current = Array.FindIndex(PlayVariantList, v => v.Command == Sys.Settings.GameLaunchSettings.DefaultPlayCommand);
            FocusedPlayVariantIndex = Math.Max(0, current);
            IsPlayPickerOpen = true;
        }

        private void HandlePlayPickerCommand(DeckCommand command)
        {
            switch (command)
            {
                case DeckCommand.NavigateUp:
                    FocusedPlayVariantIndex = Math.Max(0, FocusedPlayVariantIndex - 1);
                    break;

                case DeckCommand.NavigateDown:
                    FocusedPlayVariantIndex = Math.Min(PlayVariantList.Length - 1, FocusedPlayVariantIndex + 1);
                    break;

                case DeckCommand.Activate:
                    var selected = PlayVariantList[FocusedPlayVariantIndex];

                    Logger.Info($"Deck mode: default play command set to {selected.Command}");
                    Sys.Settings.GameLaunchSettings.DefaultPlayCommand = selected.Command;
                    Sys.SaveSettings();

                    IsPlayPickerOpen = false;
                    break;

                case DeckCommand.Back:
                    IsPlayPickerOpen = false;
                    break;
            }
        }

        /// <summary>
        /// Launches the game by driving <see cref="GameLaunchViewModel"/> /
        /// <see cref="GameLauncher"/> directly, showing progress inline. Deliberately does not
        /// call <see cref="MainWindowViewModel.LaunchGame"/> (that opens the desktop window).
        /// </summary>
        private void BeginLaunch(AppCore.DefaultPlayCommandOptions command)
        {
            if (IsLaunching)
            {
                return;
            }

            Logger.Info($"Deck mode: starting game launch ({command})");

            LaunchFailed = false;
            LaunchStatusLog = "Preparing…\n";
            IsLaunching = true;
            RebuildLegend();

            _launchViewModel = new GameLaunchViewModel(
                variableDump: command == AppCore.DefaultPlayCommandOptions.PlayWithVariableDump,
                debugLogging: command == AppCore.DefaultPlayCommandOptions.PlayWithDebugLog)
            {
                IsLaunchingWithNoMods = command == AppCore.DefaultPlayCommandOptions.PlayWithoutMods,
            };

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
