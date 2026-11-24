using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using BarudakBogor.Gameplay;

namespace BarudakBogor.Game.Views;

/// <summary>
/// On-screen touch controls for Android tablets and phones: a virtual thumbstick on the left,
/// camera drag on the background <c>InputLayer</c>, and ergonomic action pads on the right.
/// </summary>
public partial class MainView
{
    private Vector2 _touchStick;
    private IPointer? _stickPointer;
    private bool _touchRunHeld;
    private bool _touchJumpHeld;
    private bool _touchJumpPressed;
    private bool _touchInteractPressed;
    private bool _touchHornHeld;

    private void WireTouchControls()
    {
        if (!OperatingSystem.IsAndroid())
        {
            return;
        }

        TouchMenuButton.Click += (_, _) =>
        {
            if (Playing && _session is { Activity: null })
            {
                Pause(PauseTab.Map);
            }
        };
        TouchViewButton.Click += (_, _) => ToggleCameraView();

        TouchStickArea.PointerPressed += (_, e) =>
        {
            _stickPointer = e.Pointer;
            e.Pointer.Capture(TouchStickArea);
            UpdateStick(e.GetPosition(TouchStickArea));
            e.Handled = true;
        };
        TouchStickArea.PointerMoved += (_, e) =>
        {
            if (_stickPointer == e.Pointer)
            {
                UpdateStick(e.GetPosition(TouchStickArea));
                e.Handled = true;
            }
        };
        TouchStickArea.PointerReleased += (_, e) =>
        {
            if (_stickPointer == e.Pointer)
            {
                _stickPointer = null;
                e.Pointer.Capture(null);
                ResetStick();
                e.Handled = true;
            }
        };
        TouchStickArea.PointerCaptureLost += (_, _) =>
        {
            _stickPointer = null;
            ResetStick();
        };

        WirePad(TouchInteractPad,
            () => _touchInteractPressed = true,
            () => { });
        WirePad(TouchRunPad,
            () => _touchRunHeld = true,
            () => _touchRunHeld = false);
        WirePad(TouchJumpPad,
            () =>
            {
                _touchJumpHeld = true;
                _touchJumpPressed = true;
            },
            () => _touchJumpHeld = false);
        WirePad(TouchHornPad,
            () =>
            {
                _touchHornHeld = true;
                if (_session?.Player.Vehicle is { Spec.Motorised: true })
                {
                    _audio?.Horn();
                }
            },
            () => _touchHornHeld = false);
    }

    private static void WirePad(Border pad, Action onDown, Action onUp)
    {
        pad.PointerPressed += (_, e) =>
        {
            e.Pointer.Capture(pad);
            pad.Opacity = 0.65;
            onDown();
            e.Handled = true;
        };
        pad.PointerReleased += (_, e) =>
        {
            e.Pointer.Capture(null);
            pad.Opacity = 1.0;
            onUp();
            e.Handled = true;
        };
        pad.PointerCaptureLost += (_, _) =>
        {
            pad.Opacity = 1.0;
            onUp();
        };
    }

    private void UpdateStick(Point pos)
    {
        const double radius = 44.0;
        const double center = 75.0;
        double dx = pos.X - center;
        double dy = pos.Y - center;
        double dist = Math.Sqrt((dx * dx) + (dy * dy));
        if (dist > radius)
        {
            dx = dx / dist * radius;
            dy = dy / dist * radius;
        }

        Canvas.SetLeft(TouchStickKnob, 44.0 + dx);
        Canvas.SetTop(TouchStickKnob, 44.0 + dy);
        // Move.X is positive = left, Move.Y is positive = forward.
        _touchStick = new Vector2((float)(-dx / radius), (float)(-dy / radius));
    }

    private void ResetStick()
    {
        _touchStick = Vector2.Zero;
        Canvas.SetLeft(TouchStickKnob, 44.0);
        Canvas.SetTop(TouchStickKnob, 44.0);
    }

    private void UpdateTouchOverlay(GameSession session)
    {
        if (!OperatingSystem.IsAndroid())
        {
            return;
        }

        bool show = Playing && session.Dialog is null && session.Activity is null;
        TouchControls.IsVisible = show;
        if (!show)
        {
            return;
        }

        bool inVehicle = session.Player.Vehicle is { Spec.IsCart: false };
        TouchRunLabel.Text = inVehicle ? "GAS" : "LARI";
        TouchJumpLabel.Text = inVehicle ? "REM" : "LOMPAT";
        TouchHornPad.IsVisible = inVehicle;
    }

    private void ApplyTouchInput(ref InputState input, GameSession session)
    {
        if (!OperatingSystem.IsAndroid())
        {
            return;
        }

        if (_touchStick.LengthSquared() > 0.01f)
        {
            input.Move = _touchStick;
        }

        bool inVehicle = session.Player.Vehicle is { Spec.IsCart: false };
        if (inVehicle)
        {
            if (_touchRunHeld)
            {
                input.Move = new Vector2(input.Move.X, 1f);
            }

            if (_touchJumpHeld)
            {
                input.Handbrake = true;
                if (!_touchRunHeld && input.Move.Y > -0.1f)
                {
                    input.Move = new Vector2(input.Move.X, -0.7f);
                }
            }
        }
        else
        {
            input.Run |= _touchRunHeld;
            input.Jump |= _touchJumpPressed;
        }

        input.Interact |= _touchInteractPressed;
        input.Action |= _touchInteractPressed || _touchJumpPressed;
        input.ActionHeld |= _touchJumpHeld;
        input.Horn |= _touchHornHeld;

        _touchJumpPressed = false;
        _touchInteractPressed = false;
    }
}
