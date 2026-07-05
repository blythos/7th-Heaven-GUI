using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace AppUI.Deck.Input
{
    /// <summary>
    /// Controller input source: polls XInput directly (xinput1_4.dll) and raises the
    /// same logical <see cref="DeckCommand"/>s the keyboard source does. XInput covers
    /// Xbox pads, 8BitDo-style pads in X mode, and anything routed through Steam Input
    /// on desktop; DirectInput was abandoned because modern Xbox-protocol pads do not
    /// reliably expose state through it. Raising a command switches the legend to pad
    /// glyphs; keyboard input switches it back.
    /// </summary>
    public class ControllerInputSource : IDeckInputSource, IDisposable
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public event Action<DeckCommand> CommandRaised;

        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(33);
        private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(350);
        private static readonly TimeSpan RepeatRate = TimeSpan.FromMilliseconds(120);
        private static readonly TimeSpan LongPressThreshold = TimeSpan.FromMilliseconds(600);

        private const int StickDeadZone = 12000; // axis range is -32768..32767
        private const byte TriggerThreshold = 64; // trigger range is 0..255

        #region XInput interop

        private const uint ErrorSuccess = 0;
        private const int MaxUserIndex = 4;

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputGamepad
        {
            public ushort Buttons;
            public byte LeftTrigger;
            public byte RightTrigger;
            public short ThumbLX;
            public short ThumbLY;
            public short ThumbRX;
            public short ThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XInputState
        {
            public uint PacketNumber;
            public XInputGamepad Gamepad;
        }

        private static class Buttons
        {
            public const ushort DPadUp = 0x0001;
            public const ushort DPadDown = 0x0002;
            public const ushort DPadLeft = 0x0004;
            public const ushort DPadRight = 0x0008;
            public const ushort Start = 0x0010;
            public const ushort Back = 0x0020;
            public const ushort LeftShoulder = 0x0100;
            public const ushort RightShoulder = 0x0200;
            public const ushort A = 0x1000;
            public const ushort B = 0x2000;
            public const ushort X = 0x4000;
            public const ushort Y = 0x8000;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern uint XInputGetState(uint userIndex, out XInputState state);

        #endregion

        private readonly System.Threading.Timer _timer;
        private readonly Dispatcher _dispatcher;
        private int _isPolling; // overlap guard for the threadpool timer

        private int _userIndex = -1;
        private DateTime _lastReconnectAttempt = DateTime.MinValue;

        private ushort _previousButtons;
        private bool _previousLeftTrigger;
        private bool _previousRightTrigger;
        private DeckCommand? _heldDirection;
        private DateTime _nextRepeatAt;
        private DateTime? _startDownAt;
        private bool _longPressRaised;

        public ControllerInputSource()
        {
            // Poll on a background (threadpool) timer, not a DispatcherTimer. A
            // DispatcherTimer delivers its Tick on the UI thread and will not fire again
            // while its current tick is still on the stack — and a controller-triggered
            // action that opens a modal runs the modal's nested Dispatcher.PushFrame on
            // exactly that tick, freezing the poller for the whole modal (the keyboard
            // kept working because it arrives as independent WPF routed input). Polling
            // off-thread and marshalling each command via BeginInvoke keeps the poller
            // alive; the marshalled command is a fresh dispatcher operation that the
            // nested frame pumps, so dialogs stay controller-dismissable.
            _dispatcher = Dispatcher.CurrentDispatcher;
            _timer = new System.Threading.Timer(_ => Poll(), null, PollInterval, PollInterval);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private void Poll()
        {
            // the threadpool timer can re-enter if a tick runs long; keep polls serial
            if (System.Threading.Interlocked.Exchange(ref _isPolling, 1) == 1)
            {
                return;
            }

            try
            {
                PollCore();
            }
            catch (Exception e)
            {
                Logger.Warn(e, "Deck controller poll failed");
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _isPolling, 0);
            }
        }

        private void PollCore()
        {
            XInputState state;

            if (_userIndex < 0)
            {
                if (DateTime.UtcNow - _lastReconnectAttempt < ReconnectInterval)
                {
                    return;
                }

                _lastReconnectAttempt = DateTime.UtcNow;

                for (uint i = 0; i < MaxUserIndex; i++)
                {
                    if (TryGetState(i, out state))
                    {
                        _userIndex = (int)i;
                        _previousButtons = state.Gamepad.Buttons;
                        Logger.Info($"Deck mode: controller connected (XInput slot {i})");
                        return; // start reacting from the next poll
                    }
                }

                return;
            }

            if (!TryGetState((uint)_userIndex, out state))
            {
                Logger.Info($"Deck mode: controller disconnected (XInput slot {_userIndex})");
                _userIndex = -1;
                _heldDirection = null;
                _startDownAt = null;
                _previousButtons = 0;
                return;
            }

            ushort buttons = state.Gamepad.Buttons;

            RaiseOnPress(buttons, Buttons.A, DeckCommand.Activate);
            RaiseOnPress(buttons, Buttons.B, DeckCommand.Back);
            RaiseOnPress(buttons, Buttons.X, DeckCommand.ReorderToggle);
            RaiseOnPress(buttons, Buttons.Y, DeckCommand.OpenOptions);
            RaiseOnPress(buttons, Buttons.LeftShoulder, DeckCommand.SectionPrev);
            RaiseOnPress(buttons, Buttons.RightShoulder, DeckCommand.SectionNext);
            // View/Select is Delete, not Search: catalog search already lives on Y
            // (OpenOptions opens the overlay there), matching the legend
            RaiseOnPress(buttons, Buttons.Back, DeckCommand.Delete);

            HandleStartButton(buttons);
            HandleDirections(state.Gamepad, buttons);
            HandleTriggers(state.Gamepad);

            _previousButtons = buttons;
        }

        private static bool TryGetState(uint userIndex, out XInputState state)
        {
            try
            {
                return XInputGetState(userIndex, out state) == ErrorSuccess;
            }
            catch (DllNotFoundException)
            {
                state = default;
                return false;
            }
        }

        private void RaiseOnPress(ushort buttons, ushort mask, DeckCommand command)
        {
            if ((buttons & mask) != 0 && (_previousButtons & mask) == 0)
            {
                Raise(command);
            }
        }

        /// <summary>Start/Menu: short press plays, holding past the threshold raises PlayLong.</summary>
        private void HandleStartButton(ushort buttons)
        {
            bool isDown = (buttons & Buttons.Start) != 0;
            bool wasDown = (_previousButtons & Buttons.Start) != 0;

            if (isDown && !wasDown)
            {
                _startDownAt = DateTime.UtcNow;
                _longPressRaised = false;
            }
            else if (isDown && _startDownAt.HasValue && !_longPressRaised
                     && DateTime.UtcNow - _startDownAt.Value >= LongPressThreshold)
            {
                _longPressRaised = true;
                Raise(DeckCommand.PlayLong);
            }
            else if (!isDown && wasDown)
            {
                if (_startDownAt.HasValue && !_longPressRaised)
                {
                    Raise(DeckCommand.PlayShort);
                }

                _startDownAt = null;
            }
        }

        /// <summary>D-pad and left stick move focus, with initial-delay-then-repeat while held.</summary>
        private void HandleDirections(XInputGamepad gamepad, ushort buttons)
        {
            DeckCommand? direction = ResolveDirection(gamepad, buttons);

            if (direction != _heldDirection)
            {
                _heldDirection = direction;

                if (direction.HasValue)
                {
                    Raise(direction.Value);
                    _nextRepeatAt = DateTime.UtcNow + RepeatDelay;
                }
            }
            else if (direction.HasValue && DateTime.UtcNow >= _nextRepeatAt)
            {
                Raise(direction.Value);
                _nextRepeatAt = DateTime.UtcNow + RepeatRate;
            }
        }

        private static DeckCommand? ResolveDirection(XInputGamepad gamepad, ushort buttons)
        {
            // d-pad wins over the stick
            if ((buttons & Buttons.DPadUp) != 0) return DeckCommand.NavigateUp;
            if ((buttons & Buttons.DPadDown) != 0) return DeckCommand.NavigateDown;
            if ((buttons & Buttons.DPadLeft) != 0) return DeckCommand.NavigateLeft;
            if ((buttons & Buttons.DPadRight) != 0) return DeckCommand.NavigateRight;

            int x = gamepad.ThumbLX;
            int y = gamepad.ThumbLY;

            if (Math.Abs(x) <= StickDeadZone && Math.Abs(y) <= StickDeadZone)
            {
                return null;
            }

            // dominant axis wins (XInput Y is positive-up)
            if (Math.Abs(y) >= Math.Abs(x))
            {
                return y > 0 ? DeckCommand.NavigateUp : DeckCommand.NavigateDown;
            }

            return x < 0 ? DeckCommand.NavigateLeft : DeckCommand.NavigateRight;
        }

        /// <summary>Triggers page through lists.</summary>
        private void HandleTriggers(XInputGamepad gamepad)
        {
            bool leftTrigger = gamepad.LeftTrigger > TriggerThreshold;
            bool rightTrigger = gamepad.RightTrigger > TriggerThreshold;

            if (leftTrigger && !_previousLeftTrigger)
            {
                Raise(DeckCommand.PageUp);
            }

            if (rightTrigger && !_previousRightTrigger)
            {
                Raise(DeckCommand.PageDown);
            }

            _previousLeftTrigger = leftTrigger;
            _previousRightTrigger = rightTrigger;
        }

        /// <summary>
        /// Marshals the command to the UI thread. Polling runs off-thread (see the
        /// constructor), so commands must hop over — and going through BeginInvoke is
        /// also what makes them reach a modal's nested dispatcher frame.
        /// </summary>
        private void Raise(DeckCommand command)
        {
            _dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                DeckGlyphs.SetCurrentSet(DeckGlyphs.PadSet); // brand comes from the Settings override
                CommandRaised?.Invoke(command);
            }));
        }
    }
}
