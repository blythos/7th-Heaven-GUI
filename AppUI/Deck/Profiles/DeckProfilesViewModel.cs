using AppCore;
using AppUI.Classes;
using AppUI.Deck.Input;
using AppUI.ViewModels;
using Iros;
using Iros.Workshop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AppUI.Deck.Profiles
{
    /// <summary>
    /// One profile in the Deck profiles list.
    /// </summary>
    public class DeckProfileRowViewModel : ViewModelBase
    {
        private bool _isCurrent;
        private string _detailsText;

        public string Name { get; }

        public bool IsCurrent
        {
            get { return _isCurrent; }
            internal set
            {
                _isCurrent = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>Active-mod summary from the profile xml, loaded lazily on focus.</summary>
        public string DetailsText
        {
            get { return _detailsText; }
            internal set
            {
                _detailsText = value;
                NotifyPropertyChanged();
            }
        }

        public DeckProfileRowViewModel(string name, bool isCurrent)
        {
            Name = name;
            IsCurrent = isCurrent;
        }
    }

    /// <summary>
    /// Profiles section: pick, create, copy and delete loadouts. Listing and
    /// switching go through the existing <see cref="OpenProfileViewModel"/>; the
    /// create/copy file operations are duplicated from it because its versions are
    /// welded to the desktop name-input dialog. New/copy names use the Deck
    /// keyboard-fallback text overlay; delete asks an inline confirm and refuses
    /// the active profile.
    /// </summary>
    public class DeckProfilesViewModel : ViewModelBase, IDeckCommandHandler
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private const int PageJumpSize = 10;

        private enum ProfileAction
        {
            Switch,
            NewFromActive,
            Copy,
            Delete,
        }

        private static readonly (ProfileAction Action, string Label)[] ActionList = new[]
        {
            (ProfileAction.Switch, "Switch to profile"),
            (ProfileAction.NewFromActive, "New profile from active"),
            (ProfileAction.Copy, "Copy this profile"),
            (ProfileAction.Delete, "Delete this profile"),
        };

        private readonly MainWindowViewModel _main;
        private readonly OpenProfileViewModel _profiles;

        private List<DeckProfileRowViewModel> _rows = new List<DeckProfileRowViewModel>();
        private int _focusedProfileIndex = -1;
        private bool _isContentFocused;
        private List<DeckLegendItem> _legendItems = new List<DeckLegendItem>();

        private bool _isActionPanelOpen;
        private int _focusedActionIndex;

        private bool _isTextOverlayOpen;
        private string _textDraft = "";
        private string _textPrompt = "";
        private ProfileAction _pendingTextAction;

        private bool _isDeleteConfirmOpen;

        public DeckProfilesViewModel(MainWindowViewModel main)
        {
            _main = main;
            _profiles = new OpenProfileViewModel();

            ReloadRows(focusCurrent: true);
            RebuildLegend();
        }

        #region Bindable state

        public List<DeckProfileRowViewModel> Rows
        {
            get { return _rows; }
            private set
            {
                _rows = value;
                NotifyPropertyChanged();
            }
        }

        public int FocusedProfileIndex
        {
            get { return _focusedProfileIndex; }
            set
            {
                _focusedProfileIndex = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(FocusedProfile));
                NotifyPropertyChanged(nameof(HasFocusedProfile));
                LoadFocusedProfileDetails();
            }
        }

        public DeckProfileRowViewModel FocusedProfile
        {
            get
            {
                return (_focusedProfileIndex >= 0 && _focusedProfileIndex < _rows.Count)
                    ? _rows[_focusedProfileIndex]
                    : null;
            }
        }

        public bool HasFocusedProfile
        {
            get { return FocusedProfile != null; }
        }

        public bool IsContentFocused
        {
            get { return _isContentFocused; }
            set
            {
                _isContentFocused = value;
                NotifyPropertyChanged();
                RebuildLegend();
            }
        }

        public bool IsActionPanelOpen
        {
            get { return _isActionPanelOpen; }
            private set
            {
                _isActionPanelOpen = value;
                NotifyPropertyChanged();
                RebuildLegend();
            }
        }

        public List<string> ActionLabels
        {
            get { return ActionList.Select(a => a.Label).ToList(); }
        }

        public int FocusedActionIndex
        {
            get { return _focusedActionIndex; }
            set
            {
                _focusedActionIndex = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>True while the profile-name editor is open (raw keys go to the TextBox).</summary>
        public bool IsTextOverlayOpen
        {
            get { return _isTextOverlayOpen; }
            private set
            {
                _isTextOverlayOpen = value;
                NotifyPropertyChanged();
                RebuildLegend();
            }
        }

        public string TextDraft
        {
            get { return _textDraft; }
            set
            {
                _textDraft = value;
                NotifyPropertyChanged();
            }
        }

        public string TextPrompt
        {
            get { return _textPrompt; }
            private set
            {
                _textPrompt = value;
                NotifyPropertyChanged();
            }
        }

        public bool IsDeleteConfirmOpen
        {
            get { return _isDeleteConfirmOpen; }
            private set
            {
                _isDeleteConfirmOpen = value;
                NotifyPropertyChanged();
                RebuildLegend();
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

        #endregion

        /// <summary>Called by the shell when navigation focus enters the profiles content.</summary>
        public bool OnFocusEntered()
        {
            ReloadRows(focusCurrent: FocusedProfileIndex < 0);
            return _rows.Any();
        }

        public bool HandleCommand(DeckCommand command)
        {
            if (IsTextOverlayOpen)
            {
                if (command == DeckCommand.Back)
                {
                    IsTextOverlayOpen = false;
                }
                else if (command == DeckCommand.Activate)
                {
                    ApplyTextAction();
                }

                return true;
            }

            if (IsDeleteConfirmOpen)
            {
                if (command == DeckCommand.Activate)
                {
                    IsDeleteConfirmOpen = false;
                    DeleteFocusedProfile();
                }
                else if (command == DeckCommand.Back)
                {
                    IsDeleteConfirmOpen = false;
                }

                return true;
            }

            if (IsActionPanelOpen)
            {
                HandleActionPanelCommand(command);
                return true;
            }

            switch (command)
            {
                case DeckCommand.NavigateUp:
                    if (FocusedProfileIndex <= 0)
                    {
                        return false; // top of the list: shell moves focus to the top bar
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

                case DeckCommand.Activate:
                    SwitchToFocusedProfile();
                    return true;

                case DeckCommand.OpenOptions:
                    OpenActionPanel();
                    return true;

                default:
                    return false;
            }
        }

        private void MoveFocus(int change)
        {
            if (!_rows.Any())
            {
                return;
            }

            FocusedProfileIndex = Math.Max(0, Math.Min(_rows.Count - 1, FocusedProfileIndex + change));
        }

        #region Actions

        private void OpenActionPanel()
        {
            if (FocusedProfile == null)
            {
                return;
            }

            FocusedActionIndex = 0;
            IsActionPanelOpen = true;
        }

        private void HandleActionPanelCommand(DeckCommand command)
        {
            switch (command)
            {
                case DeckCommand.NavigateUp:
                    FocusedActionIndex = Math.Max(0, FocusedActionIndex - 1);
                    break;

                case DeckCommand.NavigateDown:
                    FocusedActionIndex = Math.Min(ActionList.Length - 1, FocusedActionIndex + 1);
                    break;

                case DeckCommand.Activate:
                    IsActionPanelOpen = false;
                    RunAction(ActionList[FocusedActionIndex].Action);
                    break;

                case DeckCommand.Back:
                    IsActionPanelOpen = false;
                    break;
            }
        }

        private void RunAction(ProfileAction action)
        {
            switch (action)
            {
                case ProfileAction.Switch:
                    SwitchToFocusedProfile();
                    break;

                case ProfileAction.NewFromActive:
                    _pendingTextAction = ProfileAction.NewFromActive;
                    TextPrompt = "Name for the new profile (a copy of the active loadout)";
                    TextDraft = "";
                    IsTextOverlayOpen = true;
                    break;

                case ProfileAction.Copy:
                    if (FocusedProfile != null)
                    {
                        _pendingTextAction = ProfileAction.Copy;
                        TextPrompt = $"Name for the copy of {FocusedProfile.Name}";
                        TextDraft = "";
                        IsTextOverlayOpen = true;
                    }
                    break;

                case ProfileAction.Delete:
                    if (FocusedProfile == null)
                    {
                        break;
                    }

                    if (FocusedProfile.IsCurrent)
                    {
                        Sys.Message(new WMessage("Cannot delete the active profile - switch to another one first", true));
                        break;
                    }

                    IsDeleteConfirmOpen = true;
                    break;
            }
        }

        private void ApplyTextAction()
        {
            string name = ValidateNewProfileName(TextDraft);

            if (name == null)
            {
                return; // ValidateNewProfileName reported why; overlay stays open
            }

            IsTextOverlayOpen = false;

            try
            {
                if (_pendingTextAction == ProfileAction.NewFromActive)
                {
                    // same file operation OpenProfileViewModel.SaveActiveProfileAsNew performs
                    if (!File.Exists(Sys.PathToCurrentProfileFile))
                    {
                        Sys.Message(new WMessage("The active profile file does not exist", true));
                        return;
                    }

                    MainWindowViewModel.SaveActiveProfile();
                    File.Copy(Sys.PathToCurrentProfileFile, ProfilePath(name));
                    Sys.Message(new WMessage($"Created profile {name} from {Sys.Settings.CurrentProfile}", true));
                }
                else if (_pendingTextAction == ProfileAction.Copy && FocusedProfile != null)
                {
                    // same file operation OpenProfileViewModel.CopyProfile performs
                    string source = ProfilePath(FocusedProfile.Name);

                    if (!File.Exists(source))
                    {
                        Sys.Message(new WMessage($"Profile {FocusedProfile.Name} no longer exists", true));
                        ReloadRows(focusCurrent: true);
                        return;
                    }

                    File.Copy(source, ProfilePath(name));
                    Sys.Message(new WMessage($"Copied {FocusedProfile.Name} to {name}", true));
                }

                Logger.Info($"Deck mode: profile {_pendingTextAction} -> {name}");
                ReloadRows(focusCurrent: false);
                FocusProfileByName(name);
            }
            catch (Exception e)
            {
                Logger.Error(e);
                Sys.Message(new WMessage($"Failed to create profile {name}", true) { LoggedException = e });
                ReloadRows(focusCurrent: false);
            }
        }

        /// <summary>Returns the trimmed name, or null (with a status message) when invalid.</summary>
        private string ValidateNewProfileName(string input)
        {
            string name = input?.Trim() ?? "";

            if (name.Length == 0)
            {
                Sys.Message(new WMessage("Profile name cannot be empty", true));
                return null;
            }

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                Sys.Message(new WMessage("Profile name contains invalid characters", true));
                return null;
            }

            if (File.Exists(ProfilePath(name)))
            {
                Sys.Message(new WMessage($"A profile named {name} already exists", true));
                return null;
            }

            return name;
        }

        private static string ProfilePath(string name)
        {
            return Path.Combine(Sys.PathToProfiles, $"{name}.xml");
        }

        private void DeleteFocusedProfile()
        {
            DeckProfileRowViewModel row = FocusedProfile;

            if (row == null || row.IsCurrent)
            {
                return;
            }

            Logger.Info($"Deck mode: deleting profile {row.Name}");
            _profiles.DeleteProfile(row.Name);
            ReloadRows(focusCurrent: false);
        }

        private void SwitchToFocusedProfile()
        {
            DeckProfileRowViewModel row = FocusedProfile;

            if (row == null)
            {
                return;
            }

            if (row.IsCurrent)
            {
                Sys.Message(new WMessage($"{row.Name} is already the active profile", true));
                return;
            }

            Logger.Info($"Deck mode: switching to profile {row.Name}");

            // SwitchToProfile persists the current profile first and reads SelectedProfile internally
            _profiles.SelectedProfile = row.Name;

            if (_profiles.SwitchToProfile(row.Name))
            {
                _main.RefreshProfile();
                ReloadRows(focusCurrent: true);
            }
        }

        #endregion

        private void ReloadRows(bool focusCurrent)
        {
            _profiles.ReloadProfiles();

            Rows = _profiles.Profiles
                .Select(name => new DeckProfileRowViewModel(name, name.Equals(Sys.Settings.CurrentProfile, StringComparison.InvariantCultureIgnoreCase)))
                .ToList();

            if (focusCurrent)
            {
                int currentIndex = _rows.FindIndex(r => r.IsCurrent);
                FocusedProfileIndex = currentIndex >= 0 ? currentIndex : (_rows.Any() ? 0 : -1);
            }
            else
            {
                FocusedProfileIndex = Math.Max(0, Math.Min(_rows.Count - 1, FocusedProfileIndex));
            }
        }

        private void FocusProfileByName(string name)
        {
            int index = _rows.FindIndex(r => r.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

            if (index >= 0)
            {
                FocusedProfileIndex = index;
            }
        }

        private void LoadFocusedProfileDetails()
        {
            DeckProfileRowViewModel row = FocusedProfile;

            if (row == null || row.DetailsText != null)
            {
                return;
            }

            try
            {
                Profile profile = Util.Deserialize<Profile>(ProfilePath(row.Name));
                row.DetailsText = string.Join("\n", profile.GetDetails());
            }
            catch (Exception e)
            {
                Logger.Warn(e, $"Failed to read profile details for {row.Name}");
                row.DetailsText = "Could not read profile details.";
            }
        }

        private void RebuildLegend()
        {
            var items = new List<DeckLegendItem>();

            if (IsTextOverlayOpen)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Create"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"));
            }
            else if (IsDeleteConfirmOpen)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Delete"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"));
            }
            else if (IsActionPanelOpen)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Select"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"));
            }
            else
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Switch profile"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Options, "Actions"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Back"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Sections, "Section"));
            }

            LegendItems = items;
        }
    }
}
