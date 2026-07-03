using AppUI.Deck.Input;
using AppUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AppUI.Deck.Settings
{
    /// <summary>
    /// Settings section: a root menu (Game driver, General, Quit) with drill-down
    /// screens built from <see cref="DeckSettingRowViewModel"/> rows. All interaction
    /// patterns live here once — toggle, choice (cycle or picker panel), stepper, and
    /// the keyboard-fallback text overlay; the adapters only supply rows.
    /// </summary>
    public class DeckSettingsViewModel : ViewModelBase, IDeckCommandHandler
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public const int InlineCycleThreshold = 5;
        private const int PageJumpSize = 10;

        private readonly Action _onQuitRequested;

        private readonly Stack<(List<DeckSettingRowViewModel> rows, int focusIndex, string title)> _levelStack
            = new Stack<(List<DeckSettingRowViewModel>, int, string)>();

        private List<DeckSettingRowViewModel> _rows = new List<DeckSettingRowViewModel>();
        private int _focusedRowIndex = -1;
        private string _levelTitle = "Settings";
        private bool _isContentFocused;

        private bool _isValuePanelOpen;
        private List<string> _valueChoices = new List<string>();
        private int _focusedValueIndex;

        private bool _isTextOverlayOpen;
        private string _textDraft = "";

        private List<DeckLegendItem> _legendItems = new List<DeckLegendItem>();

        public DeckSettingsViewModel(Action onQuitRequested)
        {
            _onQuitRequested = onQuitRequested;

            _rows = BuildRootRows();
            RebuildLegend();
        }

        #region Bindable state

        public List<DeckSettingRowViewModel> Rows
        {
            get { return _rows; }
            private set
            {
                _rows = value;
                NotifyPropertyChanged();
            }
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
                RebuildLegend();
            }
        }

        public DeckSettingRowViewModel FocusedRow
        {
            get
            {
                return (_focusedRowIndex >= 0 && _focusedRowIndex < _rows.Count)
                    ? _rows[_focusedRowIndex]
                    : null;
            }
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

        public List<string> ValueChoices
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

        /// <summary>True while the keyboard-fallback text editor is open (raw keys go to the TextBox).</summary>
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

        /// <summary>Called by the shell when navigation focus enters the settings content.</summary>
        public bool OnFocusEntered()
        {
            if (FocusedRowIndex < 0)
            {
                FocusedRowIndex = FindFocusable(0, 1);
            }

            return true;
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
                    FocusedRow?.SetValue(TextDraft ?? "");
                    FocusedRow?.Refresh();
                    IsTextOverlayOpen = false;
                }

                return true;
            }

            if (IsValuePanelOpen)
            {
                HandleValuePanelCommand(command);
                return true;
            }

            switch (command)
            {
                case DeckCommand.NavigateUp:
                    int upTarget = FindFocusable(FocusedRowIndex - 1, -1);

                    if (upTarget < 0)
                    {
                        return false; // top of the list: shell moves focus to the top bar
                    }

                    FocusedRowIndex = upTarget;
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

                case DeckCommand.NavigateLeft:
                    AdjustFocusedRow(-1);
                    return true;

                case DeckCommand.NavigateRight:
                    AdjustFocusedRow(1);
                    return true;

                case DeckCommand.Activate:
                    ActivateFocusedRow();
                    return true;

                case DeckCommand.Back:
                    if (_levelStack.Any())
                    {
                        PopLevel();
                        return true;
                    }

                    return false; // at the root menu: shell takes over

                default:
                    return false;
            }
        }

        #region Navigation

        private int FindFocusable(int start, int direction)
        {
            for (int i = start; i >= 0 && i < _rows.Count; i += direction)
            {
                if (!_rows[i].IsHeader)
                {
                    return i;
                }
            }

            return -1;
        }

        private void MoveFocus(int change)
        {
            if (!_rows.Any())
            {
                return;
            }

            int direction = Math.Sign(change);
            int remaining = Math.Abs(change);
            int index = FocusedRowIndex;

            while (remaining > 0)
            {
                int next = FindFocusable(index + direction, direction);

                if (next < 0)
                {
                    break;
                }

                index = next;
                remaining--;
            }

            if (index != FocusedRowIndex && index >= 0)
            {
                FocusedRowIndex = index;
            }
        }

        private void PushLevel(string title, List<DeckSettingRowViewModel> rows)
        {
            _levelStack.Push((_rows, FocusedRowIndex, LevelTitle));

            Rows = rows;
            LevelTitle = title;
            FocusedRowIndex = FindFocusable(0, 1);
            RebuildLegend();
        }

        private void PopLevel()
        {
            var (rows, focusIndex, title) = _levelStack.Pop();

            Rows = rows;
            LevelTitle = title;
            FocusedRowIndex = focusIndex;
            RebuildLegend();
        }

        #endregion

        #region Row interaction

        private void ActivateFocusedRow()
        {
            DeckSettingRowViewModel row = FocusedRow;

            if (row == null)
            {
                return;
            }

            switch (row.Kind)
            {
                case DeckSettingRowKind.Action:
                    row.OnActivated?.Invoke();
                    break;

                case DeckSettingRowKind.Toggle:
                    row.SetToggle(!row.GetToggle());
                    row.Refresh();
                    break;

                case DeckSettingRowKind.Choice:
                    if (row.IsSmallChoice)
                    {
                        CycleChoice(row, 1);
                    }
                    else
                    {
                        OpenValuePanel(row);
                    }
                    break;

                case DeckSettingRowKind.Text:
                    TextDraft = row.GetValue() ?? "";
                    IsTextOverlayOpen = true;
                    break;
            }
        }

        private void AdjustFocusedRow(int direction)
        {
            DeckSettingRowViewModel row = FocusedRow;

            if (row == null)
            {
                return;
            }

            if (row.Kind == DeckSettingRowKind.Choice && row.IsSmallChoice)
            {
                CycleChoice(row, direction);
            }
            else if (row.Kind == DeckSettingRowKind.Stepper)
            {
                StepValue(row, direction);
            }
        }

        private void CycleChoice(DeckSettingRowViewModel row, int direction)
        {
            int count = row.Choices.Count;

            if (count == 0)
            {
                return;
            }

            int index = row.GetChoiceIndex();
            index = index < 0 ? 0 : (index + direction + count) % count;

            row.SetChoiceIndex(index);
            row.Refresh();
        }

        private void StepValue(DeckSettingRowViewModel row, int direction)
        {
            string current = row.GetValue() ?? "";

            if (long.TryParse(current, NumberStyles.Integer, CultureInfo.InvariantCulture, out long intValue))
            {
                row.SetValue((intValue + direction).ToString(CultureInfo.InvariantCulture));
            }
            else if (double.TryParse(current, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
            {
                row.SetValue((doubleValue + (0.1 * direction)).ToString("0.###", CultureInfo.InvariantCulture));
            }
            else
            {
                return;
            }

            row.Refresh();
        }

        #endregion

        #region Value panel

        private void OpenValuePanel(DeckSettingRowViewModel row)
        {
            ValueChoices = row.Choices;
            FocusedValueIndex = Math.Max(0, row.GetChoiceIndex());
            IsValuePanelOpen = true;
        }

        private void HandleValuePanelCommand(DeckCommand command)
        {
            switch (command)
            {
                case DeckCommand.NavigateUp:
                    FocusedValueIndex = Math.Max(0, FocusedValueIndex - 1);
                    break;

                case DeckCommand.NavigateDown:
                    FocusedValueIndex = Math.Min(ValueChoices.Count - 1, FocusedValueIndex + 1);
                    break;

                case DeckCommand.PageUp:
                    FocusedValueIndex = Math.Max(0, FocusedValueIndex - PageJumpSize);
                    break;

                case DeckCommand.PageDown:
                    FocusedValueIndex = Math.Min(ValueChoices.Count - 1, FocusedValueIndex + PageJumpSize);
                    break;

                case DeckCommand.Activate:
                    DeckSettingRowViewModel row = FocusedRow;

                    if (row != null && FocusedValueIndex >= 0 && FocusedValueIndex < row.Choices.Count)
                    {
                        row.SetChoiceIndex(FocusedValueIndex);
                        row.Refresh();
                    }

                    IsValuePanelOpen = false;
                    break;

                case DeckCommand.Back:
                    IsValuePanelOpen = false;
                    break;
            }
        }

        #endregion

        private List<DeckSettingRowViewModel> BuildRootRows()
        {
            return new List<DeckSettingRowViewModel>()
            {
                DeckSettingRowViewModel.Action("Game driver", "Graphics, controls, cheats and advanced FFNx settings", OpenGameDriver),
                DeckSettingRowViewModel.Action("General", "App behaviour, update channels and library options", OpenGeneral),
                DeckSettingRowViewModel.Action("Quit 7th Heaven", "Exit the app", () => _onQuitRequested?.Invoke()),
            };
        }

        private void OpenGameDriver()
        {
            List<DeckSettingRowViewModel> rows = DeckGameDriverAdapter.TryBuildRows();

            if (rows == null)
            {
                return; // adapter reported why via Sys.Message
            }

            Logger.Info("Deck mode: opened game driver settings");
            PushLevel("Game driver", rows);
        }

        private void OpenGeneral()
        {
            Logger.Info("Deck mode: opened general settings");
            PushLevel("General", DeckGeneralSettingsAdapter.BuildRows());
        }

        private void RebuildLegend()
        {
            var items = new List<DeckLegendItem>();

            if (IsTextOverlayOpen)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Save"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"));
            }
            else if (IsValuePanelOpen)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Select"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"));
            }
            else
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move"));

                DeckSettingRowViewModel row = FocusedRow;

                if (row != null)
                {
                    switch (row.Kind)
                    {
                        case DeckSettingRowKind.Action:
                            items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Select"));
                            break;
                        case DeckSettingRowKind.Toggle:
                            items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Toggle"));
                            break;
                        case DeckSettingRowKind.Choice:
                            items.Add(row.IsSmallChoice
                                ? DeckGlyphs.Item(DeckLegendInput.LeftRight, "Cycle value")
                                : DeckGlyphs.Item(DeckLegendInput.Activate, "Choose"));
                            break;
                        case DeckSettingRowKind.Stepper:
                            items.Add(DeckGlyphs.Item(DeckLegendInput.LeftRight, "Adjust"));
                            break;
                        case DeckSettingRowKind.Text:
                            items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Edit"));
                            break;
                    }
                }

                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Back"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Sections, "Section"));
            }

            LegendItems = items;
        }
    }
}
