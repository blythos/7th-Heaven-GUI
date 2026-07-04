using AppUI.Deck.Input;
using AppUI.ViewModels;
using Iros.Workshop;
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
        private readonly Action _onCatalogChanged;

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
        private string _textOverlayStatus = "";

        // catalog subscriptions level (non-null while it is the current level)
        private GeneralSettingsViewModel _subsVm;
        private bool _isAddingSubscription;
        private bool _isRowLifted;
        private SubscriptionSettingViewModel _liftedSub;
        private int _liftOriginalIndex;
        private bool _isConfirmOpen;
        private string _confirmText = "";
        private SubscriptionSettingViewModel _pendingRemoveSub;

        private List<DeckLegendItem> _legendItems = new List<DeckLegendItem>();

        public DeckSettingsViewModel(Action onQuitRequested, Action onCatalogChanged)
        {
            _onQuitRequested = onQuitRequested;
            _onCatalogChanged = onCatalogChanged;

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
                TextOverlayStatus = ""; // typing clears the last validation message
            }
        }

        /// <summary>Inline validation feedback under the text editor (e.g. a bad catalog URL).</summary>
        public string TextOverlayStatus
        {
            get { return _textOverlayStatus; }
            private set
            {
                _textOverlayStatus = value;
                NotifyPropertyChanged();
            }
        }

        /// <summary>True while a subscription row is "lifted" and up/down reorder it.</summary>
        public bool IsRowLifted
        {
            get { return _isRowLifted; }
            private set
            {
                _isRowLifted = value;
                NotifyPropertyChanged();
                RebuildLegend();
            }
        }

        public bool IsConfirmOpen
        {
            get { return _isConfirmOpen; }
            private set
            {
                _isConfirmOpen = value;
                NotifyPropertyChanged();
                RebuildLegend();
            }
        }

        public string ConfirmText
        {
            get { return _confirmText; }
            private set
            {
                _confirmText = value;
                NotifyPropertyChanged();
            }
        }

        public string ConfirmHint
        {
            get { return DeckGlyphs.ConfirmHint("confirms"); }
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
                    CloseTextOverlay();
                }
                else if (command == DeckCommand.Activate)
                {
                    if (_isAddingSubscription)
                    {
                        TryAddSubscription(); // stays open with inline feedback when invalid
                    }
                    else
                    {
                        FocusedRow?.SetValue(TextDraft ?? "");
                        FocusedRow?.Refresh();
                        CloseTextOverlay();
                    }
                }

                return true;
            }

            if (IsConfirmOpen)
            {
                if (command == DeckCommand.Activate)
                {
                    IsConfirmOpen = false;
                    RemoveConfirmedSubscription();
                }
                else if (command == DeckCommand.Back)
                {
                    IsConfirmOpen = false;
                    _pendingRemoveSub = null;
                }

                return true;
            }

            if (IsRowLifted)
            {
                HandleLiftedRowCommand(command);
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

                case DeckCommand.ReorderToggle:
                    if (IsSubscriptionsLevel)
                    {
                        LiftFocusedSubscription();
                        return true;
                    }

                    return false;

                case DeckCommand.OpenOptions:
                    if (IsSubscriptionsLevel)
                    {
                        BeginRemoveFocusedSubscription();
                        return true;
                    }

                    return false;

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
            if (IsSubscriptionsLevel)
            {
                CloseSubscriptionsLevel();
            }

            var (rows, focusIndex, title) = _levelStack.Pop();

            Rows = rows;
            LevelTitle = title;
            FocusedRowIndex = focusIndex;
            RebuildLegend();
        }

        #endregion

        #region Row interaction

        /// <summary>Mouse entry point: focuses the clicked row and performs its primary action.</summary>
        public void ActivateRowViaMouse(DeckSettingRowViewModel row)
        {
            if (IsRowLifted)
            {
                DropLiftedRow(); // a click while lifted just drops the row where it is
                return;
            }

            int index = _rows.IndexOf(row);

            if (index < 0 || row.IsHeader || IsTextOverlayOpen || IsValuePanelOpen || IsConfirmOpen)
            {
                return;
            }

            FocusedRowIndex = index;
            ActivateFocusedRow();
        }

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
                DeckSettingRowViewModel.Action("Appearance", "Theme, UI scale and controller button glyphs", OpenAppearance),
                DeckSettingRowViewModel.Action("Catalog subscriptions", "Add, remove and prioritise the mod catalogs behind Browse catalog", OpenSubscriptions),
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

        private void OpenAppearance()
        {
            Logger.Info("Deck mode: opened appearance settings");
            PushLevel("Appearance", DeckAppearanceAdapter.BuildRows());
        }

        #region Catalog subscriptions

        private bool IsSubscriptionsLevel
        {
            get { return _subsVm != null; }
        }

        /// <summary>
        /// List screen over <see cref="GeneralSettingsViewModel"/>'s subscription
        /// management: rows are catalogs in priority order, lift-to-reorder like the
        /// Installed-mods list, remove behind an inline confirm, add by iros:// URL
        /// through the keyboard-fallback overlay (the name resolves from the catalog
        /// download, as on the desktop). Every change persists immediately.
        /// </summary>
        private void OpenSubscriptions()
        {
            var vm = new GeneralSettingsViewModel();
            vm.LoadSettings(Sys.Settings);

            // fires after an add resolves (on the dispatcher) and after a remove;
            // closure-bound so a resolution finishing after the level closed still persists
            vm.ListDataChanged += () =>
            {
                PersistSubscriptions(vm);

                if (_subsVm == vm)
                {
                    RefreshSubscriptionRows();
                }
            };

            // shows/hides the "Resolving catalog name…" placeholder row
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(GeneralSettingsViewModel.IsResolvingName) && _subsVm == vm)
                {
                    RefreshSubscriptionRows();
                }
            };

            _subsVm = vm;

            Logger.Info("Deck mode: opened catalog subscriptions");
            PushLevel("Catalog subscriptions", BuildSubscriptionRows());
        }

        private void CloseSubscriptionsLevel()
        {
            _isRowLifted = false;
            _liftedSub = null;
            _pendingRemoveSub = null;
            _isConfirmOpen = false;
            _isAddingSubscription = false;
            _subsVm = null; // a pending name resolution keeps running; its closure still persists
        }

        private List<DeckSettingRowViewModel> BuildSubscriptionRows()
        {
            var rows = new List<DeckSettingRowViewModel>();

            foreach (SubscriptionSettingViewModel sub in _subsVm.SubscriptionList)
            {
                SubscriptionSettingViewModel captured = sub;

                var row = DeckSettingRowViewModel.Action(
                    string.IsNullOrWhiteSpace(sub.Name) ? "(unnamed catalog)" : sub.Name,
                    sub.Url,
                    () => ToggleLiftSubscription(captured));
                row.Tag = captured;

                rows.Add(row);
            }

            if (_subsVm.IsResolvingName)
            {
                rows.Add(DeckSettingRowViewModel.Header("Resolving catalog name…"));
            }

            rows.Add(DeckSettingRowViewModel.Action("Add catalog", "Subscribe to a mod catalog by its iros:// URL", OpenAddSubscription));

            return rows;
        }

        /// <summary>Rebuilds the subscription rows in place, keeping focus sensible.</summary>
        private void RefreshSubscriptionRows()
        {
            if (!IsSubscriptionsLevel)
            {
                return;
            }

            int focus = FocusedRowIndex;
            Rows = BuildSubscriptionRows();

            int target = Math.Max(0, Math.Min(focus, _rows.Count - 1));

            if (_rows[target].IsHeader)
            {
                int below = FindFocusable(target, 1);
                target = below >= 0 ? below : FindFocusable(target, -1);
            }

            FocusedRowIndex = target;
        }

        private void FocusSubscription(SubscriptionSettingViewModel sub)
        {
            int index = _rows.FindIndex(r => ReferenceEquals(r.Tag, sub));

            if (index >= 0)
            {
                FocusedRowIndex = index;
            }
        }

        #region Reorder (lift)

        private void ToggleLiftSubscription(SubscriptionSettingViewModel sub)
        {
            if (IsRowLifted)
            {
                DropLiftedRow();
            }
            else
            {
                _liftedSub = sub;
                _liftOriginalIndex = _subsVm.SubscriptionList.IndexOf(sub);
                IsRowLifted = true;
            }
        }

        private void LiftFocusedSubscription()
        {
            if (FocusedRow?.Tag is SubscriptionSettingViewModel sub)
            {
                ToggleLiftSubscription(sub);
            }
        }

        private void HandleLiftedRowCommand(DeckCommand command)
        {
            switch (command)
            {
                case DeckCommand.NavigateUp:
                    MoveLiftedSubscription(-1);
                    break;

                case DeckCommand.NavigateDown:
                    MoveLiftedSubscription(1);
                    break;

                case DeckCommand.Activate:
                case DeckCommand.ReorderToggle:
                    DropLiftedRow();
                    break;

                case DeckCommand.Back:
                    CancelLiftedRow();
                    break;

                // everything else is inert while a row is lifted
            }
        }

        private void MoveLiftedSubscription(int direction)
        {
            _subsVm.MoveSelectedSubscription(_liftedSub, direction);
            RefreshSubscriptionRows();
            FocusSubscription(_liftedSub);
        }

        private void DropLiftedRow()
        {
            bool moved = _subsVm.SubscriptionList.IndexOf(_liftedSub) != _liftOriginalIndex;

            IsRowLifted = false;
            _liftedSub = null;

            if (moved)
            {
                Logger.Info("Deck mode: catalog subscription order changed");
                PersistSubscriptions(_subsVm);
            }
        }

        private void CancelLiftedRow()
        {
            int currentIndex = _subsVm.SubscriptionList.IndexOf(_liftedSub);

            if (currentIndex >= 0 && currentIndex != _liftOriginalIndex)
            {
                _subsVm.MoveSelectedSubscription(_liftedSub, _liftOriginalIndex - currentIndex);
                RefreshSubscriptionRows();
                FocusSubscription(_liftedSub);
            }

            IsRowLifted = false;
            _liftedSub = null;
        }

        #endregion

        #region Remove

        private void BeginRemoveFocusedSubscription()
        {
            if (!(FocusedRow?.Tag is SubscriptionSettingViewModel sub))
            {
                return; // the Add row and headers are not removable
            }

            _pendingRemoveSub = sub;
            ConfirmText = $"Remove the catalog {(string.IsNullOrWhiteSpace(sub.Name) ? sub.Url : sub.Name)}? Its mods will no longer appear in Browse catalog.";
            IsConfirmOpen = true;
        }

        private void RemoveConfirmedSubscription()
        {
            if (_pendingRemoveSub == null || _subsVm == null)
            {
                return;
            }

            Logger.Info($"Deck mode: removing catalog subscription {_pendingRemoveSub.Url}");

            // fires ListDataChanged, which persists and rebuilds the rows
            _subsVm.RemoveSelectedSubscription(_pendingRemoveSub);
            _pendingRemoveSub = null;
        }

        #endregion

        #region Add

        private void OpenAddSubscription()
        {
            _subsVm.NewUrlText = ""; // discard any prefill left by a cancelled attempt
            _subsVm.AddNewSubscription(); // prefills NewUrlText when the clipboard holds an iros:// link

            _isAddingSubscription = true;
            TextDraft = _subsVm.NewUrlText ?? "";
            IsTextOverlayOpen = true;
        }

        private void TryAddSubscription()
        {
            string url = (TextDraft ?? "").Trim();

            if (!url.StartsWith("iros://"))
            {
                TextOverlayStatus = "The URL must start with iros://";
                return;
            }

            if (_subsVm.SubscriptionList.Any(s => s.Url == url))
            {
                TextOverlayStatus = "Already subscribed to this catalog";
                return;
            }

            _subsVm.NewUrlText = url;

            if (_subsVm.SaveSubscription())
            {
                // name resolution is queued; ListDataChanged persists and adds the row when it lands
                Logger.Info($"Deck mode: adding catalog subscription {url}");
                CloseTextOverlay();
                RefreshSubscriptionRows(); // shows the resolving placeholder
            }
            else
            {
                TextOverlayStatus = string.IsNullOrWhiteSpace(_subsVm.StatusMessage) ? "Could not add the catalog" : _subsVm.StatusMessage;
            }
        }

        private void CloseTextOverlay()
        {
            IsTextOverlayOpen = false;
            _isAddingSubscription = false;
            TextOverlayStatus = "";
        }

        #endregion

        /// <summary>
        /// Live-apply for subscription changes: push the list back through the desktop
        /// save path, persist, and let the catalog re-check its sources.
        /// </summary>
        private void PersistSubscriptions(GeneralSettingsViewModel vm)
        {
            // SaveSettings pops a modal validation dialog when paths are missing;
            // pre-check so Deck mode degrades to a status message instead
            if (string.IsNullOrWhiteSpace(vm.FF7ExePathInput) || string.IsNullOrWhiteSpace(vm.LibraryPathInput))
            {
                Sys.Message(new WMessage("Cannot save - set the game and library paths in the desktop settings first", true));
                return;
            }

            try
            {
                if (vm.SaveSettings())
                {
                    Sys.SaveSettings();
                    _onCatalogChanged?.Invoke();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
                Sys.Message(new WMessage("Failed to save catalog subscriptions", true));
            }
        }

        #endregion

        /// <summary>Rebuilds legend glyphs after the active input device changes.</summary>
        public void RefreshLegend()
        {
            RebuildLegend();
            NotifyPropertyChanged(nameof(ConfirmHint));
        }

        private void RebuildLegend()
        {
            var items = new List<DeckLegendItem>();

            if (IsTextOverlayOpen)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, _isAddingSubscription ? "Add" : "Save"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"));
            }
            else if (IsConfirmOpen)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Remove"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Back, "Cancel"));
            }
            else if (IsRowLifted)
            {
                items.Add(DeckGlyphs.Item(DeckLegendInput.Move, "Move"));
                items.Add(DeckGlyphs.Item(DeckLegendInput.Activate, "Drop"));
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

                if (row?.Tag is SubscriptionSettingViewModel)
                {
                    items.Add(DeckGlyphs.Item(DeckLegendInput.Reorder, "Reorder"));
                    items.Add(DeckGlyphs.Item(DeckLegendInput.Options, "Remove"));
                }
                else if (row != null)
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
