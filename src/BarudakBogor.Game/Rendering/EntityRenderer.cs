using System.Numerics;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Numerics;
using ThreeNet;

namespace BarudakBogor.Game.Rendering;

/// <summary>
/// Draws everything that moves: the player, vehicles, pedestrians, animals and mission beacons.
/// Views are pooled per model, and the models' named parts (wheels, legs, arms) are animated
/// procedurally, so no skeletal animation data is needed.
/// </summary>
public sealed class EntityRenderer
{
    private const float DrawDistance = 380f;

    private sealed class View
    {
        public required string Model;
        public required Node Pivot;
        public Node?[] Parts = [];
        public Node? Rider;
        public Node?[] RiderParts = [];
        public bool RiderChecked;
        public float Phase;
        public int Stamp;
    }

    private static readonly string[] HumanParts = ["leg_l", "leg_r", "arm_l", "arm_r", "head", "torso"];
    private static readonly string[] AnimalParts = ["leg_fl", "leg_fr", "leg_rl", "leg_rr", "head", "tail"];
    private static readonly string[] WheelParts = ["wheel_fl", "wheel_fr", "wheel_rl", "wheel_rr", "wheel_f", "wheel_r", "wheel_l", "wheel_r", "leg_fl", "leg_fr", "leg_rl", "leg_rr"];

    private readonly Scene _scene;
    private readonly ModelLibrary _models;
    private readonly Dictionary<object, View> _views = [];
    private readonly Dictionary<string, Stack<View>> _pool = [];
    private readonly List<object> _stale = [];
    private readonly View _player;
    private readonly Node _headlight;
    private readonly Node _bolt;
    private readonly Geometry _beaconGeometry;
    private readonly Material[] _beaconMaterials;
    private readonly List<Node> _beacons = [];
    private string _playerModel = string.Empty;
    private float _boltTimer;
    private int _stamp;

    public EntityRenderer(Scene scene, ModelLibrary models)
    {
        _scene = scene;
        _models = models;
        _player = new View { Model = string.Empty, Pivot = scene.CreateNode(null, "player") };
        _headlight = scene.AddLight(Light.Spot(new Vector3(1f, 0.93f, 0.78f), 900f, 60f, 0.25f, 0.55f) with { Enabled = false }, name: "headlight");

        _beaconGeometry = scene.CreateCylinderGeometry(1f, 1f, 1f, 16);
        _beaconMaterials =
        [
            Beacon(0xE8B43A), // mission start: kujang gold
            Beacon(0x7CF29C), // objective
            Beacon(0x4FB6E8), // activity
            Beacon(0xF08A3C), // shop
            Beacon(0xF3E7C3), // home
            Beacon(0xE86FB0), // passenger
        ];
        Material flash = scene.CreateMaterial(MaterialOptions.Basic(new Vector4(4f, 4.4f, 6f, 1f)));
        _bolt = scene.AddMesh(scene.CreateBoxGeometry(0.5f, 1f, 0.5f), flash, name: "bolt");
        _bolt.Visible = false;
        _bolt.CastShadow = false;
    }

    /// <summary>Node the camera should treat as the player (for audio listeners and debugging).</summary>
    public Node PlayerNode => _player.Pivot;

    /// <summary>Shows a lightning bolt at a world position for a moment.</summary>
    public void Lightning(Vector3 position)
    {
        _bolt.Position = position + new Vector3(0f, 150f, 0f);
        _bolt.Scale = new Vector3(1f, 300f, 1f);
        _bolt.Visible = true;
        _boltTimer = 0.16f;
    }

    public void Update(GameSession session, Vector3 cameraPosition, float dt, float nightGlow)
    {
        _stamp++;
        Vector2 eye = cameraPosition.Xz();

        UpdatePlayer(session.Player, dt);
        foreach (Vehicle vehicle in session.Vehicles)
        {
            DrawVehicle(vehicle, eye, dt, session.Player.Vehicle == vehicle);
        }

        foreach (Vehicle vehicle in session.Population.Traffic)
        {
            DrawVehicle(vehicle, eye, dt, false);
        }

        foreach (Pedestrian pedestrian in session.Population.Pedestrians)
        {
            if (Acquire(pedestrian, pedestrian.Model, HumanParts) is { } view)
            {
                view.Pivot.Position = pedestrian.Position;
                view.Pivot.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, pedestrian.Yaw);
                AnimateHuman(view, pedestrian.Speed, dt);
            }
        }

        foreach (Animal animal in session.Population.Animals)
        {
            if (Acquire(animal, Animal.ModelOf(animal.Kind), AnimalParts) is { } view)
            {
                view.Pivot.Position = animal.Position;
                view.Pivot.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, animal.Yaw);
                AnimateAnimal(view, animal.Speed, dt, session.Time);
            }
        }

        foreach (Missions.Mission mission in session.Missions.All)
        {
            // Mission givers wait at their start place until their mission is done.
            if (mission.GiverModel is { } model && mission.State != Missions.MissionState.Completed
                && Vector2.DistanceSquared(mission.GiverPosition, eye) < DrawDistance * DrawDistance
                && Acquire(mission, model, []) is { } view)
            {
                Vector2 at = mission.GiverPosition;
                float sway = MathF.Sin(((float)session.Time * 1.4f) + view.Phase) * 0.03f;
                view.Pivot.Position = at.ToWorld(session.Map.Terrain.HeightAt(at));
                view.Pivot.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, mission.GiverYaw + sway)
                    * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, sway * 0.6f);
            }
        }

        // Anything not touched this frame has left the world: park its view in the pool.
        _stale.Clear();
        foreach ((object key, View view) in _views)
        {
            if (view.Stamp != _stamp)
            {
                _stale.Add(key);
            }
        }

        foreach (object key in _stale)
        {
            View view = _views[key];
            view.Pivot.Visible = false;
            _views.Remove(key);
            if (!_pool.TryGetValue(view.Model, out Stack<View>? stack))
            {
                _pool[view.Model] = stack = new Stack<View>();
            }

            stack.Push(view);
        }

        UpdateHeadlight(session, nightGlow);
        UpdateBeacons(session, eye, (float)session.Time);

        if (_boltTimer > 0f)
        {
            _boltTimer -= dt;
            _bolt.Visible = _boltTimer > 0f;
        }
    }

    // ------------------------------------------------------------------ player

    private void UpdatePlayer(Player player, float dt)
    {
        string model = player.Profile.Model;
        if (model != _playerModel)
        {
            foreach (Node child in _player.Pivot.Children)
            {
                child.Remove();
            }

            _playerModel = model;
            if (_models.Spawn(model, _player.Pivot) is { } body)
            {
                _player.Parts = [.. HumanParts.Select(part => ModelLibrary.FindPart(body, part))];
            }
        }

        // Inside a car the driver is not drawn; pushing a cart they walk behind it.
        bool riding = player.Vehicle is { Spec.IsCart: false };
        _player.Pivot.Visible = !riding || player.Vehicle!.Spec.Kind is VehicleKind.Motor;
        Vector3 position = player.Position;
        if (player.Vehicle is { Spec.Kind: VehicleKind.Motor })
        {
            position.Y += 0.25f;
        }
        else if (player.InWater)
        {
            position.Y -= 0.1f;
        }

        _player.Pivot.Position = position;
        _player.Pivot.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, player.Yaw);
        AnimateHuman(_player, riding ? 0f : player.Speed, dt, sitting: riding);
    }

    private static void AnimateHuman(View view, float speed, float dt, bool sitting = false)
    {
        if (view.Parts.Length < 4)
        {
            return;
        }

        view.Phase += speed * dt * 2.6f;
        float stride = MathUtil.Saturate(speed / 1.6f) * (speed > 3f ? 0.95f : 0.6f);
        float swing = MathF.Sin(view.Phase) * stride;
        float legBase = sitting ? -1.25f : 0f;
        float armBase = sitting ? -0.9f : 0f;
        Set(view.Parts[0], legBase + swing);
        Set(view.Parts[1], legBase - swing);
        Set(view.Parts[2], armBase - (swing * 0.8f));
        Set(view.Parts[3], armBase + (swing * 0.8f));

        static void Set(Node? part, float angle)
        {
            if (part is not null)
            {
                part.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, angle);
            }
        }
    }

    private static void AnimateAnimal(View view, float speed, float dt, double time)
    {
        view.Phase += speed * dt * 4.2f;
        float stride = MathUtil.Saturate(speed / 1.2f) * 0.55f;
        float swing = MathF.Sin(view.Phase) * stride;
        for (int i = 0; i < 4 && i < view.Parts.Length; i++)
        {
            if (view.Parts[i] is { } leg)
            {
                // Diagonal pairs move together, as in a trot.
                leg.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, i is 0 or 3 ? swing : -swing);
            }
        }

        if (view.Parts.Length > 5)
        {
            // Grazing: the head dips when standing still. The tail never stops.
            float graze = speed < 0.1f ? 0.35f + (0.1f * MathF.Sin((float)time * 1.3f + view.Phase)) : 0f;
            if (view.Parts[4] is { } head)
            {
                head.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, graze);
            }

            if (view.Parts[5] is { } tail)
            {
                tail.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Sin(((float)time * 5f) + view.Phase) * 0.4f);
            }
        }
    }

    // ------------------------------------------------------------------ vehicles

    /// <summary>
    /// Open vehicles show who is driving: a rider on the motorbike, a coachman on the delman, a
    /// pedaller on the becak. Seat is (right-left, up, forward) from the vehicle origin.
    /// </summary>
    private static (string Model, Vector3 Seat, float Legs, float Arms)? RiderOf(VehicleKind kind) => kind switch
    {
        VehicleKind.Motor => ("npc_ojol", new Vector3(0f, -0.12f, -0.28f), -1.15f, -1.0f),
        VehicleKind.Delman => ("npc_sopir", new Vector3(0f, 0.42f, -0.55f), -1.35f, -0.8f),
        VehicleKind.Becak => ("npc_pedagang", new Vector3(0f, 0.12f, -0.78f), -0.9f, -1.0f),
        _ => null,
    };

    private void UpdateRider(View view, Vehicle vehicle, bool drivenByPlayer, float dt)
    {
        if (!view.RiderChecked)
        {
            view.RiderChecked = true;
            if (RiderOf(vehicle.Spec.Kind) is { } rider && _models.Spawn(rider.Model, view.Pivot) is { } body)
            {
                body.Position = rider.Seat;
                view.Rider = body;
                view.RiderParts = [.. HumanParts.Select(part => ModelLibrary.FindPart(body, part))];
            }
        }

        if (view.Rider is not { } node)
        {
            return;
        }

        // The player's own character is drawn separately; a parked vehicle has nobody on it.
        bool shown = vehicle.Occupied && !drivenByPlayer;
        node.Visible = shown;
        if (!shown || RiderOf(vehicle.Spec.Kind) is not { } pose)
        {
            return;
        }

        // Pedalling for the becak; everyone else just sits.
        float pedal = vehicle.Spec.Kind == VehicleKind.Becak ? MathF.Sin(vehicle.WheelSpin) * 0.45f : 0f;
        Node?[] parts = view.RiderParts;
        if (parts.Length >= 4)
        {
            Pose(parts[0], pose.Legs + pedal);
            Pose(parts[1], pose.Legs - pedal);
            Pose(parts[2], pose.Arms);
            Pose(parts[3], pose.Arms);
        }

        static void Pose(Node? part, float angle)
        {
            if (part is not null)
            {
                part.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, angle);
            }
        }
    }

    private void DrawVehicle(Vehicle vehicle, Vector2 eye, float dt, bool drivenByPlayer)
    {
        if (Vector2.DistanceSquared(vehicle.Ground, eye) > DrawDistance * DrawDistance
            || Acquire(vehicle, vehicle.Spec.Model, WheelParts) is not { } view)
        {
            return;
        }

        UpdateRider(view, vehicle, drivenByPlayer, dt);
        float lift = vehicle.IsOnRoad || vehicle.IsTraffic ? 0.12f : 0f;
        float lean = vehicle.Spec.Kind == VehicleKind.Motor ? vehicle.SteerAngle * MathUtil.Saturate(MathF.Abs(vehicle.Speed) / 8f) * 0.9f : 0f;
        view.Pivot.Position = vehicle.Position + new Vector3(0f, lift, 0f);
        view.Pivot.Rotation =
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, vehicle.Yaw)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitX, -vehicle.Pitch)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -vehicle.Roll - lean);

        Quaternion spin = Quaternion.CreateFromAxisAngle(Vector3.UnitX, vehicle.WheelSpin);
        Quaternion steer = Quaternion.CreateFromAxisAngle(Vector3.UnitY, vehicle.SteerAngle) * spin;
        Node?[] parts = view.Parts;
        Turn(parts[0], steer);
        Turn(parts[1], steer);
        Turn(parts[2], spin);
        Turn(parts[3], spin);
        Turn(parts[4], steer);
        Turn(parts[5], spin);
        Turn(parts[6], spin);
        Turn(parts[7], spin);

        // The delman's horse trots.
        if (parts[8] is not null)
        {
            view.Phase += vehicle.Speed * dt * 3.2f;
            float swing = MathF.Sin(view.Phase) * MathUtil.Saturate(MathF.Abs(vehicle.Speed) / 2f) * 0.5f;
            for (int i = 8; i < 12; i++)
            {
                Turn(parts[i], Quaternion.CreateFromAxisAngle(Vector3.UnitX, i is 8 or 11 ? swing : -swing));
            }
        }

        static void Turn(Node? part, Quaternion rotation)
        {
            if (part is not null)
            {
                part.Rotation = rotation;
            }
        }
    }

    private void UpdateHeadlight(GameSession session, float nightGlow)
    {
        if (session.Player.Vehicle is { Spec.Motorised: true, LightsOn: true } vehicle && nightGlow > 0.15f)
        {
            Vector3 forward = vehicle.Forward.ToWorld();
            Vector3 origin = vehicle.Position + (forward * (vehicle.Spec.Length * 0.5f)) + new Vector3(0f, 0.9f, 0f);
            _headlight.Position = origin;
            _headlight.LookAt(origin + (forward * 10f) - new Vector3(0f, 1.4f, 0f));
            _headlight.Light = Light.Spot(new Vector3(1f, 0.93f, 0.78f), 1_400f * nightGlow, 70f, 0.22f, 0.6f);
        }
        else if (_headlight.Light is { Enabled: true } light)
        {
            _headlight.Light = light with { Enabled = false };
        }
    }

    // ------------------------------------------------------------------ pooling

    private View? Acquire(object key, string model, string[] partNames)
    {
        if (_views.TryGetValue(key, out View? view))
        {
            view.Stamp = _stamp;
            return view;
        }

        if (_pool.TryGetValue(model, out Stack<View>? stack) && stack.Count > 0)
        {
            view = stack.Pop();
            view.Pivot.Visible = true;
        }
        else
        {
            Node pivot = _scene.CreateNode(null, model);
            if (_models.Spawn(model, pivot) is not { } body)
            {
                pivot.Remove();
                return null;
            }

            view = new View { Model = model, Pivot = pivot, Parts = [.. partNames.Select(part => ModelLibrary.FindPart(body, part))] };
        }

        view.Stamp = _stamp;
        view.Phase = (key.GetHashCode() & 1023) * 0.01f;
        _views[key] = view;
        return view;
    }

    // ------------------------------------------------------------------ beacons

    private Material Beacon(int hex) => _scene.CreateMaterial(MaterialOptions.Basic(new Vector4(
        ((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 0.22f)) with
    {
        AlphaMode = AlphaMode.Additive,
        DepthWrite = false,
        CullMode = CullMode.None,
    });

    private void UpdateBeacons(GameSession session, Vector2 eye, float time)
    {
        int used = 0;
        foreach (Marker marker in session.Markers)
        {
            float distance = Vector2.Distance(marker.Position, eye);
            if (distance > 900f)
            {
                continue;
            }

            if (used == _beacons.Count)
            {
                Node created = _scene.AddMesh(_beaconGeometry, _beaconMaterials[0], name: "beacon");
                created.CastShadow = false;
                created.ReceiveShadow = false;
                _beacons.Add(created);
            }

            Node beacon = _beacons[used++];
            beacon.Visible = true;
            beacon.AttachMesh(_beaconGeometry, _beaconMaterials[(int)marker.Kind]);
            // Tall and thin from afar so it reads as a pillar of light; breathing slightly.
            // Up close it thins away to nothing, so it never blocks the view of the thing it marks.
            float near = MathUtil.SmoothStep(5f, 16f, distance);
            float height = (10f + MathF.Min(distance * 0.12f, 60f)) * MathF.Max(near, 0.15f);
            float radius = (0.35f + MathF.Min(distance * 0.004f, 1.6f)) * near * (1f + (0.08f * MathF.Sin(time * 3f)));
            if (radius < 0.02f)
            {
                beacon.Visible = false;
                continue;
            }

            beacon.Scale = new Vector3(radius, height, radius);
            beacon.Position = marker.Position.ToWorld(session.Map.Terrain.HeightAt(marker.Position) + (height * 0.5f));
        }

        for (int i = used; i < _beacons.Count; i++)
        {
            _beacons[i].Visible = false;
        }
    }
}
