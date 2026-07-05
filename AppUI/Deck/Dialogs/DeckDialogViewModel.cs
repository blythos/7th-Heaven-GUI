using AppUI.Deck.Input;
using AppUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AppUI.Deck.Dialogs
{
    public class DeckDialogButtonViewModel : ViewModelBase
    {
        private bool _isFocused;

        public string Label { get; }
        public MessageBoxResult Result { get; }

        public bool IsFocused
        {
            get { return _isFocused; }
            internal set
            {
                _isFocused = value;
                NotifyPropertyChanged();
            }
        }

        public DeckDialogButtonViewModel(string label, MessageBoxResult result)
        {
            Label = label;
            Result = result;
        }
    }

    /// <summary>
    /// One Deck-native modal dialog: the controller-navigable replacement for
    /// <c>MessageDialogWindow</c> (see <see cref="DeckDialogService"/>). Mirrors the
    /// desktop dialog's shapes — message-only or details-only text, OK / OK-Cancel /
    /// Yes-No button rows — and its result semantics: <see cref="Result"/> starts as
    /// Cancel and stays Cancel when dismissed with Back, exactly like Esc on the
    /// desktop window.
    /// </summary>
    public class DeckDialogViewModel : ViewModelBase, IDeckCommandHandler
    {
        private int _focusedIndex;
        private bool _completed;

        public string Title { get; }
        public string Message { get; }
        public string Details { get; }

        /// <summary>Desktop parity: the details overload hides the message text.</summary>
        public bool ShowMessage { get; }
        public bool ShowDetails { get; }

        public List<DeckDialogButtonViewModel> Buttons { get; }

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

        /// <summary>Raised exactly once when a button is chosen or the dialog dismissed.</summary>
        public event Action Completed;

        public string Hint
        {
            get
            {
                // a single-button (OK) dialog is just an acknowledgement — one button
                // closes it, so don't advertise a separate dismiss
                if (Buttons.Count <= 1)
                {
                    return $"{DeckGlyphs.Get(DeckLegendInput.Activate)} to close";
                }

                return $"{DeckGlyphs.Get(DeckLegendInput.Activate)} selects · {DeckGlyphs.Get(DeckLegendInput.Back)} cancels";
            }
        }

        public DeckDialogViewModel(string title, string message, string details, MessageBoxButton buttons)
        {
            Title = title ?? "";
            Message = message ?? "";
            Details = details ?? "";

            ShowDetails = !string.IsNullOrEmpty(details);
            ShowMessage = !ShowDetails; // desktop details variant collapses the message

            int defaultIndex;

            switch (buttons)
            {
                case MessageBoxButton.YesNo:
                    Buttons = new List<DeckDialogButtonViewModel>()
                    {
                        new DeckDialogButtonViewModel("Yes", MessageBoxResult.Yes),
                        new DeckDialogButtonViewModel("No", MessageBoxResult.No),
                    };
                    defaultIndex = 1; // desktop focuses No
                    break;

                case MessageBoxButton.YesNoCancel:
                    Buttons = new List<DeckDialogButtonViewModel>()
                    {
                        new DeckDialogButtonViewModel("Yes", MessageBoxResult.Yes),
                        new DeckDialogButtonViewModel("No", MessageBoxResult.No),
                        new DeckDialogButtonViewModel("Cancel", MessageBoxResult.Cancel),
                    };
                    defaultIndex = 1;
                    break;

                case MessageBoxButton.OKCancel:
                    Buttons = new List<DeckDialogButtonViewModel>()
                    {
                        new DeckDialogButtonViewModel("OK", MessageBoxResult.OK),
                        new DeckDialogButtonViewModel("Cancel", MessageBoxResult.Cancel),
                    };
                    defaultIndex = 0;
                    break;

                default: // MessageBoxButton.OK
                    Buttons = new List<DeckDialogButtonViewModel>()
                    {
                        new DeckDialogButtonViewModel("OK", MessageBoxResult.OK),
                    };
                    defaultIndex = 0;
                    break;
            }

            FocusButton(defaultIndex);
            DeckGlyphs.GlyphSetChanged += OnGlyphSetChanged;
        }

        private void OnGlyphSetChanged()
        {
            NotifyPropertyChanged(nameof(Hint));
        }

        /// <summary>Fully modal: every command is consumed while the dialog is open.</summary>
        public bool HandleCommand(DeckCommand command)
        {
            switch (command)
            {
                case DeckCommand.NavigateLeft:
                case DeckCommand.NavigateUp:
                    FocusButton(Math.Max(0, _focusedIndex - 1));
                    break;

                case DeckCommand.NavigateRight:
                case DeckCommand.NavigateDown:
                    FocusButton(Math.Min(Buttons.Count - 1, _focusedIndex + 1));
                    break;

                case DeckCommand.Activate:
                    Complete(Buttons[_focusedIndex].Result);
                    break;

                case DeckCommand.Back:
                    Complete(null); // dismiss: Result stays Cancel, like Esc on the desktop
                    break;

                // everything else (sections, play, search…) is inert while modal
            }

            return true;
        }

        /// <summary>Mouse entry point: clicking a button chooses it directly.</summary>
        public void ActivateButtonViaMouse(DeckDialogButtonViewModel button)
        {
            if (Buttons.Contains(button))
            {
                Complete(button.Result);
            }
        }

        private void FocusButton(int index)
        {
            _focusedIndex = index;

            for (int i = 0; i < Buttons.Count; i++)
            {
                Buttons[i].IsFocused = (i == index);
            }
        }

        private void Complete(MessageBoxResult? result)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;

            if (result.HasValue)
            {
                Result = result.Value;
            }

            DeckGlyphs.GlyphSetChanged -= OnGlyphSetChanged;
            Completed?.Invoke();
        }
    }
}
