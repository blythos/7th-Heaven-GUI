using AppUI.Classes;
using AppUI.Deck.Input;
using AppUI.ViewModels;
using Iros.Workshop;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AppUI.Deck.Options
{
    /// <summary>
    /// Full-screen takeover that renders a mod's options tree over the existing
    /// <see cref="ConfigureModViewModel"/>. Bool rows toggle with A; List rows cycle
    /// in place (at or under <see cref="InlineCycleThreshold"/> choices) or expand a
    /// right-hand choice panel; group headers drill down a level. Every change is
    /// applied to the profile item immediately (Deck live-apply).
    /// </summary>
    public class DeckModOptionsViewModel : ViewModelBase, IDeckCommandHandler
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>List options with more choices than this open a picker panel instead of cycling in place.</summary>
        public const int InlineCycleThreshold = 5;

        private const int PageJumpSize = 10;

        private readonly ProfileItem _activeModInfo;
        private readonly List<Constraint> _constraints;
        private readonly DeckModOptionAccess _access;
        private readonly Action _onClosed;

        // drill-down stack: each level is a row list plus the focus position to restore
        private readonly Stack<(List<DeckOptionRowViewModel> rows, int focusIndex, string title)> _levelStack
            = new Stack<(List<DeckOptionRowViewModel>, int, string)>();

        private List<DeckOptionRowViewModel> _rows;
        private int _focusedRowIndex;
        private string _levelTitle;
        private bool _isValuePanelOpen;
        private List<OptionValueViewModel> _valueChoices = new List<OptionValueViewModel>();
        private int _focusedValueIndex;
        private List<DeckLegendItem> _legendItems = new List<DeckLegendItem>();

        /// <summary>The wrapped viewmodel; the view binds its Description/CompatibilityNote/ImageOptionSource.</summary>
        public ConfigureModViewModel Config { get; }

        public string ModName { get; }

        public DeckModOptionsViewModel(InstalledModViewModel mod, DeckModOptionAccess access, Action onClosed)
        {
            _access = access;
            _activeModInfo = mod.ActiveModInfo;
            _onClosed = onClosed;
            _constraints = access.Constraints;
            ModName = mod.Name;

            Config = new ConfigureModViewModel();
            Config.Init(access.Info, access.ImageReader, access.AudioReader, _activeModInfo, access.Constraints, access.PathToModXml);

            var initialValues = _activeModInfo.Settings.ToDictionary(s => s.ID, s => s.Value, StringComparer.InvariantCultureIgnoreCase);
            _rows = Config.ModOptions.Select(o => new DeckOptionRowViewModel(o, initialValues, _constraints)).ToList();
            _levelTitle = mod.Name;

            NotifyPropertyChanged(nameof(Rows));

            if (_rows.Any())
            {
                FocusedRowIndex = 0;
            }

            RebuildLegend();
        }

        public List<DeckOptionRowViewModel> Rows
        {
            get { return _rows; }
        }

        public string LevelTitle
        {
            get { return _levelTitle; }
            private set
            {
                _levelTitle = value;
                NotifyPropertyChanged();
            }
        }

        public int FocusedRowIndex
        {
            get { return _focusedRowIndex; }
            set
            {
                _focusedRowIndex = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(FocusedRow));
                OnRowFocused();
            }
        }

        public DeckOptionRowViewModel FocusedRow
        {
            get
            {
                return (_focusedRowIndex >= 0 && _focusedRowIndex < _rows.Count)
                    ? _rows[_focusedRowIndex]
                    : null;
            }
        }

        public bool IsValuePanelOpen
        {
            get { return _isValuePanelOpen; }
            private set
            {
                _isValuePanelOpen = value;
                NotifyPropertyChanged();
                RebuildLegend();
            }
        }

        public List<OptionValueViewModel> ValueChoices
        {
            get { return _valueChoices; }
            private set
            {
                _valueChoices = value;
                NotifyPropertyChanged();
            }
        }

        public int FocusedValueIndex
        {
            get { return _focusedValueIndex; }
            set
            {
                _focusedValueIndex = value;
                NotifyPropertyChanged();
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

        public bool HandleCommand(DeckCommand command)
        {
            if (IsValuePanelOpen)
            {
                HandleValuePanelCommand(command);
                return true;
            }

            switch (command)
            {
                case DeckCommand.NavigateUp:
                    MoveRowFocus(-1);
                    break;

                case DeckCommand.NavigateDown:
                    MoveRowFocus(1);
                    break;

                case DeckCommand.PageUp:
                    MoveRowFocus(-PageJumpSize);
                    break;

                case DeckCommand.PageDown:
                    MoveRowFocus(PageJumpSize);
                    break;

                case DeckCommand.NavigateLeft:
                    CycleFocusedValue(-1);
                    break;

                case DeckCommand.NavigateRight:
                    CycleFocusedValue(1);
                    break;

                case DeckCommand.Activate:
                    ActivateFocusedRow();
                    break;

                case DeckCommand.Back:
                    GoBack();
                    break;

                default:
                    // the takeover swallows everything else (sections, play, search…)
                    break;
            }

            return true;
        }

        private void MoveRowFocus(int change)
        {
            if (!_rows.Any())
            {
                return;
            }

            FocusedRowIndex = Math.Max(0, Math.Min(_rows.Count - 1, FocusedRowIndex + change));
        }

        private void OnRowFocused()
        {
            DeckOptionRowViewModel row = FocusedRow;

            if (row == null)
            {
                return;
            }

            // selecting loads description/preview and (via existing logic) normalizes
            // constraint-coerced values into the config's value store
            if (Config.SelectedOption != row.Source)
            {
                Config.SelectedOption = row.Source;
            }

            SyncRowFromConfig(row);
        }

        /// <summary>
        /// Pulls the focused row's display state back out of <see cref="Config"/>,
        /// which owns the authoritative value (including constraint coercion).
        /// </summary>
        private void SyncRowFromConfig(DeckOptionRowViewModel row)
        {
            if (row.IsBool)
            {
                row.CurrentValue = Config.IsOptionChecked ? 1 : 0;
                row.IsLocked = !Config.CheckBoxIsEnabled;
            }
            else if (row.IsList)
            {
                row.IsLocked = !Config.ComboBoxIsEnabled;

                if (Config.DropdownSelectedIndex >= 0 && Config.DropdownSelectedIndex < Config.DropdownOptions.Count)
                {
                    row.CurrentValue = Config.DropdownOptions[Config.DropdownSelectedIndex].OptionValue.Value;
                }
            }
        }

        private void CycleFocusedValue(int direction)
        {
            DeckOptionRowViewModel row = FocusedRow;

            if (row == null || !row.IsSmallList || row.IsLocked)
            {
                return;
            }

            int count = Config.DropdownOptions.Count;

            if (count == 0)
            {
                return;
            }

            Config.DropdownSelectedIndex = (Config.DropdownSelectedIndex + direction + count) % count;
            SyncRowFromConfig(row);
            ApplyLive();
        }

        private void ActivateFocusedRow()
        {
            DeckOptionRowViewModel row = FocusedRow;

            if (row == null)
            {
                return;
            }

            if (row.IsGroup)
            {
                DrillInto(row);
            }
            else if (row.IsBool)
            {
                ToggleFocusedBool(row);
            }
            else if (row.IsSmallList)
            {
                CycleFocusedValue(1);
            }
            else if (row.IsLargeList && !row.IsLocked)
            {
                OpenValuePanel();
            }
        }

        private void ToggleFocusedBool(DeckOptionRowViewModel row)
        {
            if (row.IsLocked || !Config.CheckBoxIsEnabled)
            {
                return;
            }

            Config.IsOptionChecked = !Config.IsOptionChecked;
            SyncRowFromConfig(row);
            ApplyLive();
        }

        private void DrillInto(DeckOptionRowViewModel group)
        {
            if (!group.Source.Children.Any())
            {
                return;
            }

            var initialValues = _activeModInfo.Settings.ToDictionary(s => s.ID, s => s.Value, StringComparer.InvariantCultureIgnoreCase);

            _levelStack.Push((_rows, FocusedRowIndex, LevelTitle));

            _rows = group.Source.Children.Select(o => new DeckOptionRowViewModel(o, initialValues, _constraints)).ToList();
            NotifyPropertyChanged(nameof(Rows));
            LevelTitle = $"{ModName} › {group.Name}";
            FocusedRowIndex = 0;
            RebuildLegend();
        }

        private void GoBack()
        {
            if (_levelStack.Any())
            {
                var (rows, focusIndex, title) = _levelStack.Pop();

                _rows = rows;
                NotifyPropertyChanged(nameof(Rows));
                LevelTitle = title;
                FocusedRowIndex = focusIndex;
                RebuildLegend();
                return;
            }

            Close();
        }

        #region Value panel (List options above the threshold)

        private void OpenValuePanel()
        {
            ValueChoices = Config.DropdownOptions;
            FocusedValueIndex = Config.DropdownSelectedIndex;
            IsValuePanelOpen = true;
        }

        private void HandleValuePanelCommand(DeckCommand command)
        {
            switch (command)
            {
                case DeckCommand.NavigateUp:
                    MoveValueFocus(-1);
                    break;

                case DeckCommand.NavigateDown:
                    MoveValueFocus(1);
                    break;

                case DeckCommand.PageUp:
                    MoveValueFocus(-PageJumpSize);
                    break;

                case DeckCommand.PageDown:
                    MoveValueFocus(PageJumpSize);
                    break;

                case DeckCommand.Activate:
                    if (FocusedValueIndex >= 0 && FocusedValueIndex < Config.DropdownOptions.Count)
                    {
                        Config.DropdownSelectedIndex = FocusedValueIndex;
                        SyncRowFromConfig(FocusedRow);
                        ApplyLive();
                    }

                    IsValuePanelOpen = false;
                    break;

                case DeckCommand.Back:
                    IsValuePanelOpen = false;
                    break;
            }
        }

        private void MoveValueFocus(int change)
        {
            if (!ValueChoices.Any())
            {
                return;
            }

            FocusedValueIndex = Math.Max(0, Math.Min(ValueChoices.Count - 1, FocusedValueIndex + change));
        }

        #endregion

        /// <summary>
        /// Persists the current values onto the profile item straight away —
        /// the desktop batches this behind the dialog's OK button, Deck applies live.
        /// </summary>
        private void ApplyLive()
        {
            _activeModInfo.Settings = Config.GetSettings();
        }

        private void Close()
        {
            ApplyLive();
            GameLauncher.SanityCheckSettings();

            Config.CleanUp();
            _access.Dispose();

            Logger.Info($"Deck mode: closed options for {ModName}");
            _onClosed?.Invoke();
        }

        private void RebuildLegend()
        {
            var items = new List<DeckLegendItem>();

            if (IsValuePanelOpen)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Select"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"));
            }
            else
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Change"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.LeftRight, "Cycle value"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, _levelStack.Any() ? "Back" : "Done"));
            }

            LegendItems = items;
        }
    }
}
