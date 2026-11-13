using System.Numerics;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Numerics;
using BarudakBogor.World;
using ThreeNet;

namespace BarudakBogor.Game.Rendering;

public enum CameraView
{
    /// <summary>Third person, behind the player or the vehicle.</summary>
    Chase,
    /// <summary>First person: the rider's eyes, the nose of a closed vehicle, or the character's eyes on foot.</summary>
    Cockpit,
}

/// <summary>
/// The player's camera. In chase view it orbits with the mouse, swings back behind a vehicle
/// when left alone, widens with speed, and pulls in rather than look through a wall, a tree,
/// a parked car or the ground. In cockpit view it sits at eye height and looks where you steer.
/// </summary>
public sealed class CameraRig(Scene scene)
{
    private float _distance = 5f;
    private float _clear = 5f;
    private float _idle;
    private float _shake;
    private float _lookYaw;
    private Vector3 _target;
    private bool _snapped;

    public Node Node { get; } = scene.AddCamera(Camera.Perspective(1.05f, 0.25f, 9_000f));

    public CameraView View { get; set; }

    /// <summary>True when the player's own model must not be drawn (it would fill the lens).</summary>
    public bool HidesPlayer => View == CameraView.Cockpit;

    /// <summary>Direction the camera looks along on the ground plane (same convention as entity yaw).</summary>
    public float Yaw { get; set; }

    public float Pitch { get; set; } = 0.28f;

    public Vector3 Position { get; private set; }

    public Vector3 Forward { get; private set; } = Vector3.UnitZ;

    /// <summary>Mouse or stick look, in radians.</summary>
    public void Orbit(float yawDelta, float pitchDelta)
    {
        if (View == CameraView.Cockpit)
        {
            // In the seat you turn your head, within reason.
            _lookYaw = Math.Clamp(_lookYaw + yawDelta, -2.2f, 2.2f);
            Pitch = Math.Clamp(Pitch + pitchDelta, -0.6f, 0.7f);
        }
        else
        {
            Yaw = MathUtil.WrapAngle(Yaw + yawDelta);
            Pitch = Math.Clamp(Pitch + pitchDelta, -0.25f, 1.25f);
        }

        _idle = 0f;
    }

    public void Shake(float amount) => _shake = MathF.Max(_shake, amount);

    /// <summary>Places the camera behind the player at once (new game, fast travel).</summary>
    public void Snap(GameSession session)
    {
        Yaw = session.Player.Vehicle?.Yaw ?? session.Player.Yaw;
        _lookYaw = 0f;
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

        if (View == CameraView.Cockpit)
        {
            UpdateCockpit(session, player, vehicle, speed, dt);
        }
        else
        {
            UpdateChase(session, player, vehicle, speed, dt);
        }

        if (_shake > 0.01f)
        {
            float t = (float)session.Time;
            Position += new Vector3(MathF.Sin(t * 47f), MathF.Sin(t * 61f), MathF.Cos(t * 53f)) * (_shake * 0.12f);
        }

        Node.Position = Position;
        Node.LookAt(Position + Forward);
        float fov = (View == CameraView.Cockpit ? 1.15f : 1.02f) + (MathUtil.Saturate(speed / 30f) * 0.22f);
        Node.Camera = Camera.Perspective(fov, View == CameraView.Cockpit ? 0.12f : 0.25f, 9_000f);
    }

    private void UpdateCockpit(GameSession session, Player player, Vehicle? vehicle, float speed, float dt)
    {
        float heading = vehicle?.Yaw ?? player.Yaw;
        // Let go of the look and the head settles back to straight ahead.
        if (_idle > 1.5f && speed > 1f)
        {
            _lookYaw += (0f - _lookYaw) * MathUtil.Damp(3f, dt);
        }

        Yaw = MathUtil.WrapAngle(heading + _lookYaw);
        Vector2 ahead = MathUtil.Forward(heading);
        Vector3 eye;
        if (vehicle is null)
        {
            eye = player.Position + new Vector3(0f, 1.58f, 0f) + (ahead.ToWorld() * 0.12f);
        }
        else
        {
            // Open vehicles: the rider's head. Closed ones: the driver's seat, on the right as
            // in every Indonesian car; the body is swapped for a cockpit while this view is on.
            bool open = vehicle.Spec.Kind is VehicleKind.Motor or VehicleKind.Becak or VehicleKind.Delman;
            if (!open)
            {
                Vector3 seat = SeatOf(vehicle.Spec.Kind);
                Vector2 left = MathUtil.Left(ahead);
                Position = vehicle.Position + new Vector3((left.X * seat.X) + (ahead.X * seat.Z), seat.Y, (left.Y * seat.X) + (ahead.Y * seat.Z));
                Vector2 look = MathUtil.Forward(Yaw);
                Forward = Vector3.Normalize(new Vector3(look.X * MathF.Cos(Pitch), -MathF.Sin(Pitch), look.Y * MathF.Cos(Pitch)));
                _target = Position;
                _snapped = true;
                return;
            }

            float forward = 0.05f;
            float height = vehicle.Spec.Kind switch
            {
                VehicleKind.Motor => 1.35f,
                VehicleKind.Delman => 1.75f,
                VehicleKind.Becak => 1.6f,
                VehicleKind.Truk => 1.5f,
                _ => 1.1f,
            };
            eye = vehicle.Position + new Vector3(0f, height, 0f) + (ahead.ToWorld() * forward);
        }

        Vector2 flat = MathUtil.Forward(Yaw);
        Position = eye;
        Forward = Vector3.Normalize(new Vector3(flat.X * MathF.Cos(Pitch), -MathF.Sin(Pitch), flat.Y * MathF.Cos(Pitch)));
        _target = eye;
        _snapped = true;
    }

    /// <summary>The driver's eye in a closed vehicle: (to the left, up, forward) from the vehicle's origin.</summary>
    public static Vector3 SeatOf(VehicleKind kind) => kind switch
    {
        VehicleKind.Angkot or VehicleKind.TahuBulat or VehicleKind.Bakso => new Vector3(-0.42f, 1.42f, 0.55f),
        VehicleKind.Truk => new Vector3(-0.5f, 1.95f, 1.2f),
        VehicleKind.Bajaj or VehicleKind.Bemo => new Vector3(0f, 1.2f, 0.3f),
        _ => new Vector3(-0.38f, 1.2f, 0.25f),
    };

    private void UpdateChase(GameSession session, Player player, Vehicle? vehicle, float speed, float dt)
    {
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

        Vector2 flat = MathUtil.Forward(Yaw);
        Vector3 back = new(-flat.X * MathF.Cos(Pitch), MathF.Sin(Pitch), -flat.Y * MathF.Cos(Pitch));

        // Pull in quickly when something comes between the lens and the player, ease back out after.
        float clear = ClearDistance(session, _target, back, _distance, vehicle);
        _clear = !_snapped || clear < _clear ? clear : _clear + ((clear - _clear) * MathUtil.Damp(3f, dt));
        _snapped = true;

        Vector3 position = _target + (back * _clear);
        position.Y = MathF.Max(position.Y, session.Map.Terrain.HeightAt(position.X, position.Z) + 0.45f);
        Position = position;
        Forward = Vector3.Normalize(_target - position);
    }

    /// <summary>
    /// How far back along <paramref name="back"/> the camera can sit before something solid is in
    /// the way: a building, a tree trunk, a parked or passing vehicle.
    /// </summary>
    private static float ClearDistance(GameSession session, Vector3 target, Vector3 back, float wanted, Vehicle? own)
    {
        WorldMap map = session.Map;
        const float step = 0.4f;
        for (float d = 0.8f; d <= wanted; d += step)
        {
            Vector3 p = target + (back * d);
            Vector2 ground = p.Xz();
            float floor = map.Terrain.HeightAt(ground);
            if (map.IsInsideBuilding(ground, 0.35f) && p.Y < floor + 14f)
            {
                return MathF.Max(0.9f, d - step);
            }

            // Trunks only matter below the crown; above it the camera clears the tree.
            if (p.Y < floor + 3.2f)
            {
                (int chunkX, int chunkZ) = WorldMap.ChunkOf(ground);
                foreach (Tree tree in map.TreesInChunk(chunkX, chunkZ))
                {
                    float radius = tree.Radius + 0.3f;
                    if (tree.Radius > 0f && Vector2.DistanceSquared(tree.Position, ground) < radius * radius)
                    {
                        return MathF.Max(0.9f, d - step);
                    }
                }
            }

            if (p.Y < floor + 2.4f && (Blocked(session.Vehicles, ground, own) || Blocked(session.Population.Traffic, ground, own)))
            {
                return MathF.Max(0.9f, d - step);
            }
        }

        return wanted;
    }

    private static bool Blocked(IReadOnlyList<Vehicle> vehicles, Vector2 point, Vehicle? own)
    {
        foreach (Vehicle vehicle in vehicles)
        {
            float radius = vehicle.Spec.Radius + 0.2f;
            if (vehicle != own && Vector2.DistanceSquared(vehicle.Ground, point) < radius * radius)
            {
                return true;
            }
        }

        return false;
    }
}
