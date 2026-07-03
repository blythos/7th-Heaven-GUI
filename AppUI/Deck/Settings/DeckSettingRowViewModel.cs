using AppUI.ViewModels;
using System;
using System.Collections.Generic;

namespace AppUI.Deck.Settings
{
    public enum DeckSettingRowKind
    {
        /// <summary>Non-focusable group caption.</summary>
        Header,
        /// <summary>Navigates or performs an action on activate.</summary>
        Action,
        Toggle,
        /// <summary>Fixed set of values: cycle in place or open a picker panel.</summary>
        Choice,
        /// <summary>Numeric value adjusted with left/right.</summary>
        Stepper,
        /// <summary>Free text with the keyboard-fallback overlay.</summary>
        Text,
    }

    /// <summary>
    /// One row on a Deck settings screen. The interaction patterns (toggle, choice,
    /// stepper, text) are generic; adapters supply the getters/setters, and setters
    /// are expected to live-apply (persist) the change themselves.
    /// </summary>
    public class DeckSettingRowViewModel : ViewModelBase
    {
        public DeckSettingRowKind Kind { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }

        internal Func<bool> GetToggle { get; private set; }
        internal Action<bool> SetToggle { get; private set; }

        internal List<string> Choices { get; private set; }
        internal Func<int> GetChoiceIndex { get; private set; }
        internal Action<int> SetChoiceIndex { get; private set; }

        internal Func<string> GetValue { get; private set; }
        internal Action<string> SetValue { get; private set; }

        internal Action OnActivated { get; private set; }

        public bool IsHeader { get { return Kind == DeckSettingRowKind.Header; } }
        public bool IsAction { get { return Kind == DeckSettingRowKind.Action; } }
        public bool IsToggle { get { return Kind == DeckSettingRowKind.Toggle; } }
        public bool IsChoice { get { return Kind == DeckSettingRowKind.Choice; } }
        public bool IsStepper { get { return Kind == DeckSettingRowKind.Stepper; } }
        public bool IsText { get { return Kind == DeckSettingRowKind.Text; } }

        public bool IsSmallChoice
        {
            get { return IsChoice && Choices.Count <= DeckSettingsViewModel.InlineCycleThreshold; }
        }

        public bool IsLargeChoice
        {
            get { return IsChoice && Choices.Count > DeckSettingsViewModel.InlineCycleThreshold; }
        }

        public bool IsOn
        {
            get { return IsToggle && GetToggle(); }
        }

        public string DisplayValue
        {
            get
            {
                switch (Kind)
                {
                    case DeckSettingRowKind.Toggle:
                        return IsOn ? "On" : "Off";

                    case DeckSettingRowKind.Choice:
                        int index = GetChoiceIndex();
                        return (index >= 0 && index < Choices.Count) ? Choices[index] : (GetValue?.Invoke() ?? "");

                    case DeckSettingRowKind.Stepper:
                    case DeckSettingRowKind.Text:
                        return GetValue() ?? "";

                    default:
                        return "";
                }
            }
        }

        internal void Refresh()
        {
            NotifyPropertyChanged(nameof(IsOn));
            NotifyPropertyChanged(nameof(DisplayValue));
        }

        private DeckSettingRowViewModel(DeckSettingRowKind kind, string name, string description)
        {
            Kind = kind;
            Name = name;
            Description = description ?? "";
        }

        public static DeckSettingRowViewModel Header(string name)
        {
            return new DeckSettingRowViewModel(DeckSettingRowKind.Header, name, null);
        }

        public static DeckSettingRowViewModel Action(string name, string description, Action onActivated)
        {
            return new DeckSettingRowViewModel(DeckSettingRowKind.Action, name, description) { OnActivated = onActivated };
        }

        public static DeckSettingRowViewModel Toggle(string name, string description, Func<bool> get, Action<bool> set)
        {
            return new DeckSettingRowViewModel(DeckSettingRowKind.Toggle, name, description) { GetToggle = get, SetToggle = set };
        }

        public static DeckSettingRowViewModel Choice(string name, string description, List<string> choices, Func<int> getIndex, Action<int> setIndex, Func<string> getCustomValue = null)
        {
            return new DeckSettingRowViewModel(DeckSettingRowKind.Choice, name, description)
            {
                Choices = choices,
                GetChoiceIndex = getIndex,
                SetChoiceIndex = setIndex,
                GetValue = getCustomValue,
            };
        }

        public static DeckSettingRowViewModel Stepper(string name, string description, Func<string> get, Action<string> set)
        {
            return new DeckSettingRowViewModel(DeckSettingRowKind.Stepper, name, description) { GetValue = get, SetValue = set };
        }

        public static DeckSettingRowViewModel Text(string name, string description, Func<string> get, Action<string> set)
        {
            return new DeckSettingRowViewModel(DeckSettingRowKind.Text, name, description) { GetValue = get, SetValue = set };
        }
    }
}
