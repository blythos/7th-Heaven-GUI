using AppUI.Classes;
using AppUI.Deck.Input;
using AppUI.ViewModels;
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

        public MainWindowViewModel Main { get; }

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

            UpdateSectionFlags();
            RebuildLegend();
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
                RebuildLegend();
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

        public bool IsPlaceholderVisible
        {
            get { return !IsModListVisible && !IsPlaySectionVisible; }
        }

        public string PlaceholderText
        {
            get
            {
                switch (CurrentSection)
                {
                    case DeckSection.BrowseCatalog: return "Browse catalog is coming in a later milestone.";
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

                // deferred to later milestones; consumed so nothing else reacts
                case DeckCommand.PlayLong:
                case DeckCommand.OpenOptions:
                case DeckCommand.Search:
                case DeckCommand.ReorderToggle:
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
                else if (CurrentSection == DeckSection.MyMods)
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
            if (CurrentSection != DeckSection.MyMods || !Main.MyMods.ModList.Any())
            {
                return;
            }

            if (FocusedModIndex < 0)
            {
                FocusedModIndex = 0;
            }

            CurrentFocusArea = FocusArea.ModList;
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

        private void NotifySectionChanged()
        {
            NotifyPropertyChanged(nameof(CurrentSection));
            NotifyPropertyChanged(nameof(IsModListVisible));
            NotifyPropertyChanged(nameof(IsPlaySectionVisible));
            NotifyPropertyChanged(nameof(IsPlaceholderVisible));
            NotifyPropertyChanged(nameof(PlaceholderText));
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

            if (IsLaunching)
            {
                if (LaunchFailed)
                {
                    items.Add(new DeckLegendItem("B", "Dismiss"));
                }
            }
            else if (CurrentFocusArea == FocusArea.ModList)
            {
                items.Add(new DeckLegendItem("↑↓", "Move"));
                items.Add(new DeckLegendItem("A", "Toggle mod"));
                items.Add(new DeckLegendItem("B", "Back"));
                items.Add(new DeckLegendItem("LB RB", "Section"));
                items.Add(new DeckLegendItem("☰", "Play"));
            }
            else
            {
                items.Add(new DeckLegendItem("↑↓", "Move"));
                items.Add(new DeckLegendItem("A", CurrentSection == DeckSection.Play ? "Play" : "Select"));
                items.Add(new DeckLegendItem("LB RB", "Section"));
                items.Add(new DeckLegendItem("☰", "Play"));
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
