using System.Numerics;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Numerics;
using ThreeNet;

namespace BarudakBogor.Game.Rendering;

/// <summary>
/// Third person camera: orbits the player with the mouse, swings back behind a vehicle when
/// left alone, widens with speed, and never dips under the ground or into a building.
/// </summary>
public sealed class CameraRig(Scene scene)
{
    private float _distance = 5f;
    private float _idle;
    private float _shake;
    private Vector3 _target;
    private bool _snapped;

    public Node Node { get; } = scene.AddCamera(Camera.Perspective(1.05f, 0.25f, 9_000f));

    /// <summary>Direction the camera looks along on the ground plane (same convention as entity yaw).</summary>
    public float Yaw { get; set; }

    public float Pitch { get; set; } = 0.28f;

    public Vector3 Position { get; private set; }

    public Vector3 Forward { get; private set; } = Vector3.UnitZ;

    /// <summary>Mouse look, in radians.</summary>
    public void Orbit(float yawDelta, float pitchDelta)
    {
        Yaw = MathUtil.WrapAngle(Yaw + yawDelta);
        Pitch = Math.Clamp(Pitch + pitchDelta, -0.25f, 1.25f);
        _idle = 0f;
    }

    public void Shake(float amount) => _shake = MathF.Max(_shake, amount);

    /// <summary>Places the camera behind the player at once (new game, fast travel).</summary>
    public void Snap(GameSession session)
    {
        Yaw = session.Player.Vehicle?.Yaw ?? session.Player.Yaw;
        _snapped = false;
        Update(session, 0f);
    }

    public void Update(GameSession session, float dt)
    {
        Player player = session.Player;
        Vehicle? vehicle = player.Vehicle is { Spec.IsCart: false } v ? v : null;
        _idle += dt;
        _shake = MathF.Max(0f, _shake - (dt * 2.5f));

        float speed = vehicle is null ? player.Speed : MathF.Abs(vehicle.Speed);
        float wantedDistance = vehicle is null ? 4.6f : 5.2f + (vehicle.Spec.Length * 0.75f) + (speed * 0.11f);
        float height = vehicle is null ? 1.55f : 1.3f + (vehicle.Spec.Length * 0.12f);

        // Left alone, the camera drifts back behind whatever the player is steering.
        float followAfter = vehicle is null ? 2.5f : 0.9f;
        if (_idle > followAfter && speed > 1.2f)
        {
            float heading = vehicle?.Yaw ?? player.Yaw;
            float rate = vehicle is null ? 0.8f : 2.4f;
            Yaw = MathUtil.TurnToward(Yaw, heading, rate * dt * MathUtil.Saturate(MathF.Abs(MathUtil.WrapAngle(heading - Yaw)) + 0.15f));
            if (vehicle is not null)
            {
                Pitch += (0.24f - Pitch) * MathUtil.Damp(1.5f, dt);
            }
        }

        Vector3 anchor = (vehicle?.Position ?? player.Position) + new Vector3(0f, height, 0f);
        _target = _snapped ? Vector3.Lerp(_target, anchor, MathUtil.Damp(14f, dt)) : anchor;
        _distance = _snapped ? _distance + ((wantedDistance - _distance) * MathUtil.Damp(4f, dt)) : wantedDistance;
        _snapped = true;

        Vector2 flat = MathUtil.Forward(Yaw);
        Vector3 back = new(-flat.X * MathF.Cos(Pitch), MathF.Sin(Pitch), -flat.Y * MathF.Cos(Pitch));

        // Pull in until the lens is clear of walls and above the ground.
        float distance = _distance;
        Vector3 position = _target + (back * distance);
        for (int i = 0; i < 6; i++)
        {
            float ground = session.Map.Terrain.HeightAt(position.X, position.Z) + 0.45f;
            bool blocked = session.Map.IsInsideBuilding(position.Xz(), 0.5f) && position.Y < ground + 12f;
            if (!blocked)
            {
                position.Y = MathF.Max(position.Y, ground);
                break;
            }

            distance *= 0.72f;
            position = _target + (back * distance);
        }

        if (_shake > 0.01f)
        {
            float t = (float)session.Time;
            position += new Vector3(MathF.Sin(t * 47f), MathF.Sin(t * 61f), MathF.Cos(t * 53f)) * (_shake * 0.12f);
        }

        Position = position;
        Forward = Vector3.Normalize(_target - position);
        Node.Position = position;
        Node.LookAt(_target);
        float fov = 1.02f + (MathUtil.Saturate(speed / 30f) * 0.22f);
        Node.Camera = Camera.Perspective(fov, 0.25f, 9_000f);
    }
}
