using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace AppUI.Deck.Input
{
    /// <summary>
    /// v1 input source: translates keyboard input on a window into logical
    /// <see cref="DeckCommand"/>s. On the Steam Deck a Steam Input keystroke
    /// layout emits these same keys, so this source drives both platforms.
    /// </summary>
    /// <remarks>
    /// Default keymap (Xbox pad equivalent in parentheses):
    ///   Arrows        → NavigateUp/Down/Left/Right (d-pad / left stick)
    ///   Enter / Space → Activate (A)
    ///   Esc / Backspace → Back (B)
    ///   R             → ReorderToggle (X)
    ///   O             → OpenOptions (Y)
    ///   F             → Search (Y in catalog)
    ///   Q             → SectionPrev (LB)
    ///   E             → SectionNext (RB)
    ///   Delete        → Delete (View/Select)
    ///   PageUp/PageDown → PageUp/PageDown (LT/RT)
    ///   P             → PlayShort on tap, PlayLong on hold (Menu short/long)
    /// </remarks>
    public class KeyboardInputSource : IDeckInputSource
    {
        /// <summary>How long the play key must be held before release counts as a long press.</summary>
        public static readonly TimeSpan LongPressThreshold = TimeSpan.FromMilliseconds(600);

        private static readonly Dictionary<Key, DeckCommand> KeyMap = new Dictionary<Key, DeckCommand>()
        {
            { Key.Up, DeckCommand.NavigateUp },
            { Key.Down, DeckCommand.NavigateDown },
            { Key.Left, DeckCommand.NavigateLeft },
            { Key.Right, DeckCommand.NavigateRight },
            { Key.Enter, DeckCommand.Activate },
            { Key.Space, DeckCommand.Activate },
            { Key.Escape, DeckCommand.Back },
            { Key.Back, DeckCommand.Back },
            { Key.R, DeckCommand.ReorderToggle },
            { Key.O, DeckCommand.OpenOptions },
            { Key.F, DeckCommand.Search },
            { Key.Q, DeckCommand.SectionPrev },
            { Key.E, DeckCommand.SectionNext },
            { Key.PageUp, DeckCommand.PageUp },
            { Key.PageDown, DeckCommand.PageDown },
            { Key.Delete, DeckCommand.Delete },
        };

        private const Key PlayKey = Key.P;

        public event Action<DeckCommand> CommandRaised;

        /// <summary>
        /// While a free-text field is active only Esc (Back) and Enter (Activate) are
        /// intercepted; every other key flows through to the focused control.
        /// </summary>
        public bool TextEntryMode { get; set; }

        private readonly Window _window;
        private DateTime? _playKeyDownAt;
        private DispatcherTimer _longPressTimer;
        private bool _longPressRaised;

        public KeyboardInputSource(Window window)
        {
            _window = window;
            _window.PreviewKeyDown += Window_PreviewKeyDown;
            _window.PreviewKeyUp += Window_PreviewKeyUp;
        }

        public void Detach()
        {
            _window.PreviewKeyDown -= Window_PreviewKeyDown;
            _window.PreviewKeyUp -= Window_PreviewKeyUp;
            StopLongPressTimer();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (TextEntryMode)
            {
                if (e.Key == Key.Escape)
                {
                    RaiseCommand(DeckCommand.Back);
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter)
                {
                    RaiseCommand(DeckCommand.Activate);
                    e.Handled = true;
                }

                return;
            }

            if (e.Key == PlayKey)
            {
                if (!e.IsRepeat)
                {
                    _playKeyDownAt = DateTime.UtcNow;
                    _longPressRaised = false;
                    StartLongPressTimer();
                }

                e.Handled = true;
                return;
            }

            if (KeyMap.TryGetValue(e.Key, out DeckCommand command))
            {
                RaiseCommand(command);
                e.Handled = true;
            }
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (TextEntryMode || e.Key != PlayKey)
            {
                return;
            }

            StopLongPressTimer();

            // long press already fired at the threshold; the release is a no-op then
            if (_playKeyDownAt.HasValue && !_longPressRaised)
            {
                RaiseCommand(DeckCommand.PlayShort);
            }

            _playKeyDownAt = null;
            e.Handled = true;
        }

        private void RaiseCommand(DeckCommand command)
        {
            DeckGlyphs.SetCurrentSet(DeckGlyphSet.Keyboard);
            CommandRaised?.Invoke(command);
        }

        private void StartLongPressTimer()
        {
            StopLongPressTimer();

            _longPressTimer = new DispatcherTimer() { Interval = LongPressThreshold };
            _longPressTimer.Tick += (s, args) =>
            {
                StopLongPressTimer();
                _longPressRaised = true;
                RaiseCommand(DeckCommand.PlayLong);
            };
            _longPressTimer.Start();
        }

        private void StopLongPressTimer()
        {
            if (_longPressTimer != null)
            {
                _longPressTimer.Stop();
                _longPressTimer = null;
            }
        }
    }
}
