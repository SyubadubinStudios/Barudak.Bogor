using System.Numerics;

namespace BarudakBogor.Gameplay;

/// <summary>Controller buttons. The bit values match the engine's, so its state casts straight across.</summary>
[Flags]
public enum PadButtons : uint
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    LeftBumper = 1 << 4,
    RightBumper = 1 << 5,
    Select = 1 << 6,
    Start = 1 << 7,
    LeftStick = 1 << 9,
    RightStick = 1 << 10,
    DPadUp = 1 << 11,
    DPadDown = 1 << 12,
    DPadLeft = 1 << 13,
    DPadRight = 1 << 14,
}

/// <summary>One frame of a controller. Sticks are -1..1 with right and up positive; triggers 0..1.</summary>
public readonly record struct PadState(
    PadButtons Buttons, PadButtons Previous, Vector2 LeftStick, Vector2 RightStick, float LeftTrigger, float RightTrigger)
{
    public bool IsDown(PadButtons button) => (Buttons & button) != 0;

    public bool WasPressed(PadButtons button) => (Buttons & button) != 0 && (Previous & button) == 0;
}

/// <summary>
/// Folds a controller into the device-independent <see cref="InputState"/>, on top of whatever
/// the keyboard already put there. Layout: left stick moves and steers, right trigger is the
/// throttle and left trigger the brake, A jumps and acts, X interacts, B cancels, Y honks,
/// the left bumper runs and the right bumper is the handbrake.
/// </summary>
public static class PadInput
{
    private const float DeadZone = 0.18f;

    /// <param name="input">Input to add to.</param>
    /// <param name="pad">Controller state this frame.</param>
    /// <param name="driving">True when the player is in a vehicle (triggers become throttle and brake).</param>
    /// <param name="choices">Number of dialogue choices on screen, 0 when none.</param>
    /// <param name="lanes">Lanes of the running rhythm game (2 or 4), 0 when none.</param>
    public static void Merge(ref InputState input, in PadState pad, bool driving, int choices, int lanes)
    {
        Vector2 stick = ApplyDeadZone(pad.LeftStick);
        // The game's Move.X is positive to the left; a stick is positive to the right.
        float x = -stick.X;
        float y = stick.Y;
        if (driving && (pad.RightTrigger > 0.05f || pad.LeftTrigger > 0.05f))
        {
            y = pad.RightTrigger - pad.LeftTrigger;
        }

        if (MathF.Abs(x) > MathF.Abs(input.Move.X))
        {
            input.Move.X = x;
        }

        if (MathF.Abs(y) > MathF.Abs(input.Move.Y))
        {
            input.Move.Y = y;
        }

        input.Run |= pad.IsDown(PadButtons.LeftBumper) || pad.IsDown(PadButtons.LeftStick);
        input.Handbrake |= pad.IsDown(PadButtons.RightBumper);
        input.Horn |= pad.IsDown(PadButtons.Y);
        input.Jump |= pad.WasPressed(PadButtons.A);
        input.Action |= pad.WasPressed(PadButtons.A);
        input.ActionHeld |= pad.IsDown(PadButtons.A);
        input.Interact |= pad.WasPressed(PadButtons.X);
        input.Cancel |= pad.WasPressed(PadButtons.B);

        if (choices > 0)
        {
            // Up, right, down, left pick choices 1 to 4, in the order they are listed.
            ReadOnlySpan<PadButtons> pick = [PadButtons.DPadUp, PadButtons.DPadRight, PadButtons.DPadDown, PadButtons.DPadLeft];
            for (int i = 0; i < Math.Min(choices, pick.Length); i++)
            {
                if (pad.WasPressed(pick[i]))
                {
                    input.Lanes |= 1 << i;
                }
            }
        }
        else if (lanes == 2)
        {
            if (pad.WasPressed(PadButtons.DPadLeft) || pad.WasPressed(PadButtons.LeftBumper))
            {
                input.Lanes |= 1;
            }

            if (pad.WasPressed(PadButtons.B) || pad.WasPressed(PadButtons.RightBumper))
            {
                input.Lanes |= 2;
            }

            // B is a lane here, not "leave".
            input.Cancel = input.Cancel && !pad.WasPressed(PadButtons.B);
        }
        else if (lanes == 4)
        {
            ReadOnlySpan<PadButtons> keys = [PadButtons.DPadLeft, PadButtons.DPadUp, PadButtons.Y, PadButtons.B];
            for (int i = 0; i < keys.Length; i++)
            {
                if (pad.WasPressed(keys[i]))
                {
                    input.Lanes |= 1 << i;
                }
            }

            input.Cancel = input.Cancel && !pad.WasPressed(PadButtons.B);
        }
    }

    /// <summary>Ignores stick drift near the centre and rescales the rest to start from zero.</summary>
    public static Vector2 ApplyDeadZone(Vector2 stick)
    {
        float length = stick.Length();
        if (length < DeadZone)
        {
            return Vector2.Zero;
        }

        float scaled = MathF.Min(1f, (length - DeadZone) / (1f - DeadZone));
        return stick / length * scaled;
    }
}
