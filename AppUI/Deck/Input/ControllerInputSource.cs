using AppUI.Classes;
using SharpDX.DirectInput;
using System;
using System.Windows.Threading;

namespace AppUI.Deck.Input
{
    /// <summary>
    /// Controller input source: polls a physical pad through the existing
    /// <see cref="GameController"/> (SharpDX DirectInput) and raises the same logical
    /// <see cref="DeckCommand"/>s the keyboard source does. Button indices follow the
    /// XInput-style layout DirectInput reports for Xbox-class pads. Raising a command
    /// switches the legend to pad glyphs; keyboard input switches it back.
    /// </summary>
    public class ControllerInputSource : IDeckInputSource, IDisposable
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public event Action<DeckCommand> CommandRaised;

        private const int DeadZone = 350; // axis range is -1000..1000

        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(33);
        private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(350);
        private static readonly TimeSpan RepeatRate = TimeSpan.FromMilliseconds(120);
        private static readonly TimeSpan LongPressThreshold = TimeSpan.FromMilliseconds(600);

        // XInput-style button indices as DirectInput reports them
        private const int ButtonA = 0;
        private const int ButtonB = 1;
        private const int ButtonX = 2;
        private const int ButtonY = 3;
        private const int ButtonLB = 4;
        private const int ButtonRB = 5;
        private const int ButtonSelect = 6;
        private const int ButtonStart = 7;

        private readonly GameController _controller = new GameController();
        private readonly DispatcherTimer _timer;

        private DateTime _lastReconnectAttempt = DateTime.MinValue;
        private bool _wasConnected;

        private bool[] _previousButtons = new bool[0];
        private DeckCommand? _heldDirection;
        private DateTime _nextRepeatAt;
        private DateTime? _startDownAt;
        private bool _longPressRaised;
        private bool _previousPageUp;
        private bool _previousPageDown;

        public ControllerInputSource()
        {
            _timer = new DispatcherTimer() { Interval = PollInterval };
            _timer.Tick += (s, e) => Poll();
            _timer.Start();
        }

        public void Dispose()
        {
            _timer.Stop();
            _controller.ReleaseDevice();
        }

        private void Poll()
        {
            if (!_controller.IsConnected)
            {
                if (DateTime.UtcNow - _lastReconnectAttempt < ReconnectInterval)
                {
                    return;
                }

                _lastReconnectAttempt = DateTime.UtcNow;
                _controller.CreateDevice();

                if (!_controller.IsConnected)
                {
                    return;
                }

                Logger.Info("Deck mode: controller connected");
                _wasConnected = true;
                _previousButtons = new bool[0];
            }

            JoystickState state;

            try
            {
                state = _controller.ReadState();
            }
            catch
            {
                state = null;
            }

            if (state == null)
            {
                if (_wasConnected)
                {
                    Logger.Info("Deck mode: controller disconnected");
                    _wasConnected = false;
                }

                _controller.ReleaseDevice();
                _heldDirection = null;
                _startDownAt = null;
                return;
            }

            bool[] buttons = state.Buttons;

            RaiseOnPress(buttons, ButtonA, DeckCommand.Activate);
            RaiseOnPress(buttons, ButtonB, DeckCommand.Back);
            RaiseOnPress(buttons, ButtonX, DeckCommand.ReorderToggle);
            RaiseOnPress(buttons, ButtonY, DeckCommand.OpenOptions);
            RaiseOnPress(buttons, ButtonLB, DeckCommand.SectionPrev);
            RaiseOnPress(buttons, ButtonRB, DeckCommand.SectionNext);
            RaiseOnPress(buttons, ButtonSelect, DeckCommand.Search);

            HandleStartButton(buttons);
            HandleDirections(state);
            HandleTriggers(state);

            _previousButtons = (bool[])buttons.Clone();
        }

        private void RaiseOnPress(bool[] buttons, int index, DeckCommand command)
        {
            if (IsDown(buttons, index) && !IsDown(_previousButtons, index))
            {
                Raise(command);
            }
        }

        private static bool IsDown(bool[] buttons, int index)
        {
            return index < buttons.Length && buttons[index];
        }

        /// <summary>Start/Menu: short press plays, holding past the threshold raises PlayLong.</summary>
        private void HandleStartButton(bool[] buttons)
        {
            bool isDown = IsDown(buttons, ButtonStart);
            bool wasDown = IsDown(_previousButtons, ButtonStart);

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
        private void HandleDirections(JoystickState state)
        {
            DeckCommand? direction = ResolveDirection(state);

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

        private static DeckCommand? ResolveDirection(JoystickState state)
        {
            // d-pad first (diagonals resolve to the nearest cardinal, vertical wins)
            int pov = -1;

            foreach (int value in state.PointOfViewControllers)
            {
                if (value != -1)
                {
                    pov = value;
                    break;
                }
            }

            if (pov != -1)
            {
                if (pov >= 31500 || pov <= 4500) return DeckCommand.NavigateUp;
                if (pov >= 13500 && pov <= 22500) return DeckCommand.NavigateDown;
                if (pov > 4500 && pov < 13500) return DeckCommand.NavigateRight;
                return DeckCommand.NavigateLeft;
            }

            // left stick: dominant axis wins
            int x = state.X;
            int y = state.Y;

            if (Math.Abs(x) <= DeadZone && Math.Abs(y) <= DeadZone)
            {
                return null;
            }

            if (Math.Abs(y) >= Math.Abs(x))
            {
                return y < 0 ? DeckCommand.NavigateUp : DeckCommand.NavigateDown;
            }

            return x < 0 ? DeckCommand.NavigateLeft : DeckCommand.NavigateRight;
        }

        /// <summary>Triggers page through lists (XInput pads share the Z axis between them).</summary>
        private void HandleTriggers(JoystickState state)
        {
            if (!_controller.IsXInputDevice)
            {
                return;
            }

            bool pageUp = state.Z > DeadZone;    // left trigger
            bool pageDown = state.Z < -DeadZone; // right trigger

            if (pageUp && !_previousPageUp)
            {
                Raise(DeckCommand.PageUp);
            }

            if (pageDown && !_previousPageDown)
            {
                Raise(DeckCommand.PageDown);
            }

            _previousPageUp = pageUp;
            _previousPageDown = pageDown;
        }

        private void Raise(DeckCommand command)
        {
            DeckGlyphs.SetCurrentSet(DeckGlyphSet.Xbox);
            CommandRaised?.Invoke(command);
        }
    }
}
