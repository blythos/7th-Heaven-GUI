using AppUI.Classes;
using AppUI.ViewModels;
using AppWrapper;
using System.Collections.Generic;
using System.Linq;

namespace AppUI.Deck.Options
{
    /// <summary>
    /// One row on the Deck mod options screen: a group header to drill into,
    /// a Bool toggle, or a List value. Holds a display mirror of the option's
    /// current value; the authoritative value lives in <see cref="ConfigureModViewModel"/>.
    /// </summary>
    public class DeckOptionRowViewModel : ViewModelBase
    {
        private int _currentValue;
        private bool _isLocked;

        internal ConfigOptionViewModel Source { get; }

        /// <summary>Constraint-filtered selectable values (List options only).</summary>
        internal List<OptionValue> AvailableValues { get; private set; }

        public string Name { get { return Source.OptionName; } }

        /// <summary>Tree-mode header ("=== Name ===") that drills into its children.</summary>
        public bool IsGroup { get { return Source.Option.Name.StartsWith("==="); } }

        public bool IsBool { get { return !IsGroup && Source.Option.Type == OptionType.Bool; } }

        public bool IsList { get { return !IsGroup && Source.Option.Type == OptionType.List; } }

        /// <summary>List options at or under the threshold cycle in place with left/right.</summary>
        public bool IsSmallList { get { return IsList && AvailableValues.Count <= DeckModOptionsViewModel.InlineCycleThreshold; } }

        public bool IsLargeList { get { return IsList && AvailableValues.Count > DeckModOptionsViewModel.InlineCycleThreshold; } }

        public bool IsOn { get { return IsBool && _currentValue == 1; } }

        public bool IsLocked
        {
            get { return _isLocked; }
            internal set
            {
                _isLocked = value;
                NotifyPropertyChanged();
            }
        }

        public int CurrentValue
        {
            get { return _currentValue; }
            internal set
            {
                _currentValue = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(IsOn));
                NotifyPropertyChanged(nameof(DisplayValue));
            }
        }

        public string DisplayValue
        {
            get
            {
                if (IsBool)
                {
                    return _currentValue == 1 ? "On" : "Off";
                }

                if (IsList)
                {
                    return AvailableValues.FirstOrDefault(v => v.Value == _currentValue)?.Name ?? "";
                }

                return "";
            }
        }

        internal DeckOptionRowViewModel(ConfigOptionViewModel source, Dictionary<string, int> initialValues, List<Constraint> constraints)
        {
            Source = source;

            ConfigOption option = source.Option;

            int value;
            if (!initialValues.TryGetValue(option.ID, out value))
            {
                value = option.Default;
            }

            AvailableValues = option.Values?.ToList() ?? new List<OptionValue>();

            // same coercion ConfigureModViewModel applies when an option is selected
            Constraint ct = constraints.Find(c => c.Setting.Equals(option.ID, System.StringComparison.InvariantCultureIgnoreCase));

            if (ct != null)
            {
                if (ct.Require.Any())
                {
                    value = ct.Require[0];
                    IsLocked = true;
                }
                else if (ct.Forbid.Any())
                {
                    if (option.Type == OptionType.Bool)
                    {
                        value = new[] { 0, 1 }.Except(ct.Forbid).FirstOrDefault();
                        IsLocked = true;
                    }
                    else
                    {
                        AvailableValues = AvailableValues.Where(v => !ct.Forbid.Contains(v.Value)).ToList();

                        if (!AvailableValues.Any(v => v.Value == value) && AvailableValues.Any())
                        {
                            value = AvailableValues.First().Value;
                        }
                    }
                }
            }

            CurrentValue = value;
        }
    }
}
