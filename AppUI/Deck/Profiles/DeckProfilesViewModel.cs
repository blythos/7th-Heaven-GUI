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
    /// Profiles section: pick a loadout from the top bar. Wraps the existing
    /// <see cref="OpenProfileViewModel"/> for listing and switching; create/copy/delete
    /// need the text-input pattern and come later.
    /// </summary>
    public class DeckProfilesViewModel : ViewModelBase, IDeckCommandHandler
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private const int PageJumpSize = 10;

        private readonly MainWindowViewModel _main;
        private readonly OpenProfileViewModel _profiles;

        private List<DeckProfileRowViewModel> _rows = new List<DeckProfileRowViewModel>();
        private int _focusedProfileIndex = -1;
        private bool _isContentFocused;
        private List<DeckLegendItem> _legendItems = new List<DeckLegendItem>();

        public DeckProfilesViewModel(MainWindowViewModel main)
        {
            _main = main;
            _profiles = new OpenProfileViewModel();

            ReloadRows(focusCurrent: true);
            RebuildLegend();
        }

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

        public List<DeckLegendItem> LegendItems
        {
            get { return _legendItems; }
            private set
            {
                _legendItems = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>Called by the shell when navigation focus enters the profiles content.</summary>
        public bool OnFocusEntered()
        {
            ReloadRows(focusCurrent: FocusedProfileIndex < 0);
            return _rows.Any();
        }

        public bool HandleCommand(DeckCommand command)
        {
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

        private void LoadFocusedProfileDetails()
        {
            DeckProfileRowViewModel row = FocusedProfile;

            if (row == null || row.DetailsText != null)
            {
                return;
            }

            try
            {
                string pathToProfile = Path.Combine(Sys.PathToProfiles, $"{row.Name}.xml");
                Profile profile = Util.Deserialize<Profile>(pathToProfile);
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
            LegendItems = new List<DeckLegendItem>()
            {
                DeckGlyphs.Item(DeckLegendInput.Move, "Move"),
                DeckGlyphs.Item(DeckLegendInput.Activate, "Switch profile"),
                DeckGlyphs.Item(DeckLegendInput.Back, "Back"),
                DeckGlyphs.Item(DeckLegendInput.Sections, "Section"),
            };
        }
    }
}
