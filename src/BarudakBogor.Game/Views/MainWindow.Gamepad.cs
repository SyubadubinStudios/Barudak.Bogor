using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using BarudakBogor.Activities;
using BarudakBogor.Game.Rendering;
using BarudakBogor.Gameplay;
using BarudakBogor.World;
using ThreeNet;

namespace BarudakBogor.Game.Views;

/// <summary>
/// Controller support. The layout itself lives in <see cref="PadInput"/> (and is unit tested);
/// this file polls the engine, turns the right stick into camera movement and handles the
/// buttons that belong to the window rather than to the game: pause, map, camera view.
/// </summary>
public partial class MainWindow
{
    private Gamepads? _pads;
    private bool _padsFailed;
    private PadState _pad;
    private bool _padConnected;
    private PadButtons _padSeen;

    private void PollGamepad()
    {
        if (_padsFailed)
        {
            return;
        }

        try
        {
            _pads ??= new Gamepads();
            _pads.Update();
            IReadOnlyList<GamepadState> connected = _pads.Connected;
            _padConnected = connected.Count > 0;
            if (_padConnected)
            {
                // "Previous" is what this window saw last frame, not the engine's own notion of it:
                // a virtual pad written between two polls would otherwise never show a fresh press.
                GamepadState state = connected[0];
                PadButtons buttons = (PadButtons)(uint)state.Buttons;
                _pad = new PadState(buttons, _padSeen, state.LeftStick, state.RightStick, state.LeftTrigger, state.RightTrigger);
                _padSeen = buttons;
            }
            else
            {
                _pad = default;
                _padSeen = PadButtons.None;
            }
        }
        catch (Exception exception) when (exception is ThreeNetException or DllNotFoundException or EntryPointNotFoundException)
        {
            // No controller backend on this machine: keyboard and mouse still work.
            _padsFailed = true;
            _padConnected = false;
        }
    }

    /// <summary>Adds the controller to the keyboard's input and moves the camera with the right stick.</summary>
    private void ApplyGamepad(ref InputState input, GameSession session, CameraRig camera, float dt)
    {
        if (!_padConnected)
        {
            return;
        }

        int lanes = session.Activity is RhythmGame rhythm ? rhythm.Lanes : 0;
        PadInput.Merge(ref input, _pad, session.Player.Vehicle is not null, session.Dialog?.Choices.Count ?? 0, lanes);

        Vector2 look = PadInput.ApplyDeadZone(_pad.RightStick);
        if (look != Vector2.Zero)
        {
            // Pushing right turns the view right (negative yaw); pushing up looks up.
            camera.Orbit(-look.X * 2.6f * dt, -look.Y * 1.6f * dt);
        }
    }

    /// <summary>Buttons that act on the window: Start pauses, Select opens the map, and the d-pad has shortcuts.</summary>
    private void HandleGamepadButtons(GameSession session, CameraRig camera)
    {
        if (!_padConnected)
        {
            return;
        }

        bool free = session.Dialog is null && session.Activity is null;
        if (_pad.WasPressed(PadButtons.Start) && session.Activity is null)
        {
            Pause(PauseTab.Missions);
            // The same press must not be read again by the menu as "resume".
            _pad = _pad with { Previous = _pad.Buttons };
            return;
        }

        if (_pad.WasPressed(PadButtons.Select) && free)
        {
            Pause(PauseTab.Map);
            _pad = _pad with { Previous = _pad.Buttons };
            return;
        }

        if (!free)
        {
            return;
        }

        if (_pad.WasPressed(PadButtons.Y) && session.Player.Vehicle is { Spec.Motorised: true })
        {
            _audio!.Horn();
        }

        if (_pad.WasPressed(PadButtons.DPadUp))
        {
            session.Ojol.Toggle(session);
        }

        if (_pad.WasPressed(PadButtons.DPadDown))
        {
            TakePhoto(session, camera);
        }

        if (_pad.WasPressed(PadButtons.DPadRight) || _pad.WasPressed(PadButtons.RightStick))
        {
            ToggleCameraView();
        }
    }

    private Button? _padFocus;
    private Point _padFocusPoint = new(-1, -1);
    private float _padRepeat;

    /// <summary>
    /// Menus with a controller: the d-pad or left stick moves a highlight to the nearest button in
    /// that direction, A presses it, B goes back, the bumpers change the pause tab, Y picks the
    /// next place on the map and the right stick scrolls.
    /// </summary>
    private void HandleGamepadMenus(float dt)
    {
        Control? root = !_padConnected ? null
            : _credits ? CreditsScreen
            : _screen == Screen.Title ? TitleScreen
            : _screen == Screen.Paused ? PauseScreen
            : null;
        if (root is null)
        {
            SetPadFocus(null);
            return;
        }

        if (_pad.WasPressed(PadButtons.B) || (_screen == Screen.Paused && _pad.WasPressed(PadButtons.Start)))
        {
            if (_credits)
            {
                HideCredits();
            }
            else if (_screen == Screen.Paused)
            {
                Resume();
            }

            return;
        }

        if (_screen == Screen.Paused)
        {
            int turn = (_pad.WasPressed(PadButtons.RightBumper) ? 1 : 0) - (_pad.WasPressed(PadButtons.LeftBumper) ? 1 : 0);
            if (turn != 0)
            {
                int count = Enum.GetValues<PauseTab>().Length;
                _tab = (PauseTab)(((int)_tab + turn + count) % count);
                _audio?.Click();
                RefreshPause();
            }

            if (_tab == PauseTab.Map && _pad.WasPressed(PadButtons.Y) && _session is { } session)
            {
                // Step through the places, nearest first.
                PoiId[] places = [.. session.Map.Pois.OrderBy(p => Vector2.DistanceSquared(p.Position, session.Player.Ground)).Select(p => p.Id)];
                int at = BigMap.Selected is { } current ? Array.IndexOf(places, current) : -1;
                PoiId next = places[(at + 1) % places.Length];
                BigMap.Selected = next;
                SelectPlace(next);
                BigMap.InvalidateVisual();
                _audio?.Click();
            }

            float scroll = PadInput.ApplyDeadZone(_pad.RightStick).Y;
            if (scroll != 0f && TabList.IsVisible)
            {
                TabList.Offset = new Avalonia.Vector(TabList.Offset.X, Math.Max(0, TabList.Offset.Y - (scroll * 900 * dt)));
            }
        }

        List<Button> buttons = [.. root.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.IsEffectivelyEnabled)];
        if (buttons.Count == 0)
        {
            SetPadFocus(null);
            return;
        }

        // Menus rebuild their buttons after most clicks; pick up again where the highlight was.
        if (_padFocus is null || !buttons.Contains(_padFocus))
        {
            SetPadFocus(_padFocusPoint.X < 0
                ? buttons.FirstOrDefault(b => b.Classes.Contains("go")) ?? buttons[0]
                : buttons.MinBy(b => Distance(Centre(b), _padFocusPoint)));
        }

        Vector2 stick = PadInput.ApplyDeadZone(_pad.LeftStick);
        int dx = (_pad.IsDown(PadButtons.DPadRight) || stick.X > 0.6f ? 1 : 0) - (_pad.IsDown(PadButtons.DPadLeft) || stick.X < -0.6f ? 1 : 0);
        int dy = (_pad.IsDown(PadButtons.DPadDown) || stick.Y < -0.6f ? 1 : 0) - (_pad.IsDown(PadButtons.DPadUp) || stick.Y > 0.6f ? 1 : 0);
        _padRepeat -= dt > 0f ? dt : 0.016f;
        if (dx == 0 && dy == 0)
        {
            _padRepeat = 0f;
        }
        else if (_padRepeat <= 0f)
        {
            _padRepeat = 0.22f;
            Point from = Centre(_padFocus!);
            Button? best = null;
            double bestScore = double.MaxValue;
            foreach (Button button in buttons)
            {
                Point to = Centre(button);
                double along = ((to.X - from.X) * dx) + ((to.Y - from.Y) * dy);
                double across = Math.Abs(((to.X - from.X) * dy) - ((to.Y - from.Y) * dx));
                if (button == _padFocus || along < 4)
                {
                    continue;
                }

                // Prefer what is straight ahead over what is merely close.
                double score = along + (across * 2.5);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = button;
                }
            }

            if (best is not null)
            {
                SetPadFocus(best);
                best.BringIntoView();
                _audio?.Click();
            }
        }

        if (_pad.WasPressed(PadButtons.A) && _padFocus is { } pressed)
        {
            _padFocusPoint = Centre(pressed);
            pressed.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        }
    }

    private void SetPadFocus(Button? button)
    {
        if (_padFocus == button)
        {
            return;
        }

        _padFocus?.Classes.Set("pad", false);
        _padFocus = button;
        if (button is not null)
        {
            button.Classes.Set("pad", true);
            _padFocusPoint = Centre(button);
        }
        else
        {
            _padFocusPoint = new Point(-1, -1);
        }
    }

    private Point Centre(Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), this) ?? default;

    private static double Distance(Point a, Point b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
}
