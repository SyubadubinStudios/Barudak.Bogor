using System.Numerics;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Numerics;
using ThreeNet;

namespace BarudakBogor.Game.Rendering;

/// <summary>
/// Draws everything that moves: the player, vehicles, pedestrians, animals and mission beacons.
/// Views are pooled per model, and the models' named parts (wheels, legs, arms) are animated
/// procedurally. The player is the exception: a skinned mesh on a skeleton, moved by clips made
/// in Blender (<see cref="Rig"/>).
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

    /// <summary>
    /// A character on a skeleton. Four clips play at once and are mixed by weight: idle underneath
    /// at full weight, then walk, run and sit over it. The engine applies players in the order
    /// they were started, each one blending over the pose so far, so that order matters and no
    /// player is ever stopped (a freed slot would be reused out of order).
    /// </summary>
    private sealed class Rig
    {
        public required Node Root;
        public AnimationPlayer? Walk;
        public AnimationPlayer? Run;
        public AnimationPlayer? Sit;
        public float WalkLength;
        public float RunLength;
        public float WalkWeight;
        public float RunWeight;
        public float SitWeight;
        /// <summary>Where in the stride the legs are, 0..1; shared by walk and run so they blend in step.</summary>
        public float Stride;
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
    private readonly Node[] _canopies;
    private readonly Node _umbrellaHandles;
    private readonly List<Matrix4x4>[] _canopyInstances;
    private readonly List<Matrix4x4> _handleInstances = [];
    private readonly Node _cockpit;
    private readonly Material _cockpitPaint;
    private readonly Node _steeringWheel;
    private readonly (Node Node, float Height)[] _trainParts;
    private readonly Matrix4x4[] _trainMatrices = new Matrix4x4[Train.Cars];
    private readonly Dictionary<string, Rig?> _rigs = [];
    private Rig? _playerRig;
    private Node? _playerBody;
    private string _playerModel = string.Empty;
    private VehicleKind? _cockpitKind;
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
        // Umbrellas: a shallow cone on a stick, drawn instanced (one draw call per colour).
        int[] canopyColours = [0xD8432F, 0x2F6FD8, 0xE8B43A, 0x1E9E57, 0x9C4FD0, 0x2B2F36];
        Geometry canopy = scene.CreateCylinderGeometry(0.03f, 0.68f, 0.24f, 8);
        _canopies = new Node[canopyColours.Length];
        _canopyInstances = new List<Matrix4x4>[canopyColours.Length];
        for (int i = 0; i < canopyColours.Length; i++)
        {
            int hex = canopyColours[i];
            Material cloth = scene.CreateMaterial(MaterialOptions.Pbr(
                new Vector4(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1f), 0f, 0.8f) with { CullMode = CullMode.None });
            _canopies[i] = scene.AddMesh(canopy, cloth, name: "payung");
            _canopies[i].Visible = false;
            _canopyInstances[i] = [];
        }

        Material stick = scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(0.12f, 0.12f, 0.13f, 1f), 0.4f, 0.5f));
        _umbrellaHandles = scene.AddMesh(scene.CreateCylinderGeometry(0.014f, 0.014f, 0.95f, 5), stick, name: "gagang_payung");
        _umbrellaHandles.Visible = false;
        _umbrellaHandles.CastShadow = false;

        // The view from the driver's seat: bonnet, dashboard, wheel, pillars and roof edge. It
        // stands in for the vehicle's own model, whose glass is not see-through.
        _cockpit = scene.CreateNode(null, "kokpit");
        _cockpit.Visible = false;
        _cockpitPaint = scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(0.12f, 0.62f, 0.34f, 1f), 0.2f, 0.5f));
        Material trim = scene.CreateMaterial(MaterialOptions.Pbr(new Vector4(0.07f, 0.075f, 0.08f, 1f), 0f, 0.8f));
        void Piece(Material material, Vector3 size, Vector3 at, float lean = 0f)
        {
            Node node = scene.AddMesh(scene.CreateBoxGeometry(size.X, size.Y, size.Z), material, _cockpit);
            node.Position = at;
            node.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, lean);
            node.CastShadow = false;
        }

        // Laid out round an eye at the origin looking along +Z; +X is the passenger side.
        Piece(_cockpitPaint, new Vector3(1.66f, 0.08f, 1.2f), new Vector3(0.4f, -0.74f, 2.05f), 0.04f);
        Piece(trim, new Vector3(1.72f, 0.22f, 0.42f), new Vector3(0.4f, -0.7f, 1.25f));
        Piece(trim, new Vector3(0.3f, 0.07f, 0.2f), new Vector3(0f, -0.57f, 1.12f), -0.3f);
        Piece(_cockpitPaint, new Vector3(0.06f, 1.05f, 0.06f), new Vector3(-0.46f, 0.02f, 1.2f), 0.4f);
        Piece(_cockpitPaint, new Vector3(0.06f, 1.05f, 0.06f), new Vector3(1.26f, 0.02f, 1.2f), 0.4f);
        Piece(_cockpitPaint, new Vector3(1.78f, 0.06f, 1f), new Vector3(0.4f, 0.5f, 0.5f));
        Piece(trim, new Vector3(0.22f, 0.06f, 0.03f), new Vector3(0.4f, 0.4f, 0.95f));
        Piece(_cockpitPaint, new Vector3(0.05f, 0.4f, 1.6f), new Vector3(-0.49f, -0.78f, 0.3f));
        Piece(_cockpitPaint, new Vector3(0.05f, 0.4f, 1.6f), new Vector3(1.29f, -0.78f, 0.3f));
        _steeringWheel = scene.AddMesh(scene.CreateTorusGeometry(0.17f, 0.016f, 6, 20), trim, _cockpit);
        _steeringWheel.Position = new Vector3(0f, -0.5f, 0.92f);
        _steeringWheel.CastShadow = false;

        // The commuter train is built from boxes: body, livery stripe, window band, underframe, roof.
        (float Width, float Tall, float Long, float Height, int Colour)[] carParts =
        [
            (2.9f, 2.5f, 19.4f, 2.15f, 0xE8E6DF),
            (2.94f, 0.35f, 19.42f, 1.25f, 0xC8322B),
            (2.95f, 0.75f, 17.6f, 2.55f, 0x1E2A30),
            (2.3f, 0.75f, 18f, 0.5f, 0x2B2F33),
            (2.5f, 0.25f, 18.8f, 3.5f, 0x8E9498),
        ];
        _trainParts = new (Node, float)[carParts.Length];
        for (int i = 0; i < carParts.Length; i++)
        {
            int hex = carParts[i].Colour;
            Material paint = scene.CreateMaterial(MaterialOptions.Pbr(
                new Vector4(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, 1f), 0.1f, 0.55f));
            Node part = scene.AddMesh(scene.CreateBoxGeometry(carParts[i].Width, carParts[i].Tall, carParts[i].Long), paint, name: "krl");
            part.Visible = false;
            _trainParts[i] = (part, carParts[i].Height);
        }

        Material flash = scene.CreateMaterial(MaterialOptions.Basic(new Vector4(4f, 4.4f, 6f, 1f)));
        _bolt = scene.AddMesh(scene.CreateBoxGeometry(1f, 1f, 1f), flash, name: "bolt");
        _bolt.Visible = false;
        _bolt.CastShadow = false;
    }

    /// <summary>Set while the camera sits in the player's head, where the model would fill the lens.</summary>
    public bool HidePlayer { get; set; }

    /// <summary>Node the camera should treat as the player (for audio listeners and debugging).</summary>
    public Node PlayerNode => _player.Pivot;

    /// <summary>Shows a lightning bolt at a world position for a moment.</summary>
    public void Lightning(Vector3 position)
    {
        // A forked bolt: the main channel wanders down from the cloud base, and now and then a
        // thinner branch splits off and dies out before it reaches the ground.
        List<Matrix4x4> pieces = [];
        Rng rng = new((uint)(position.X * 13f) ^ (uint)(position.Z * 7f) ^ (uint)_stamp);
        void Channel(Vector3 from, Vector3 to, float width, int steps, float wander, int depth)
        {
            Vector3 a = from;
            for (int i = 1; i <= steps; i++)
            {
                Vector3 b = Vector3.Lerp(from, to, i / (float)steps);
                if (i < steps)
                {
                    b += new Vector3(rng.Range(-wander, wander), 0f, rng.Range(-wander, wander));
                }

                Vector3 along = b - a;
                float length = along.Length();
                if (length > 0.01f)
                {
                    Vector3 up = along / length;
                    Vector3 axis = Vector3.Cross(Vector3.UnitY, up);
                    Quaternion turn = axis.LengthSquared() < 1e-6f
                        ? Quaternion.Identity
                        : Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(Math.Clamp(up.Y, -1f, 1f)));
                    pieces.Add(Matrix4x4.CreateScale(width, length, width) * Matrix4x4.CreateFromQuaternion(turn) * Matrix4x4.CreateTranslation((a + b) * 0.5f));
                }

                if (depth < 2 && i > 1 && i < steps - 1 && rng.Chance(depth == 0 ? 0.38f : 0.25f))
                {
                    Vector3 reach = new(rng.Range(-70f, 70f), -rng.Range(40f, 90f), rng.Range(-70f, 70f));
                    Channel(b, b + (reach * (depth == 0 ? 1f : 0.5f)), width * 0.5f, 5, wander * 0.6f, depth + 1);
                }

                a = b;
            }
        }

        Channel(position + new Vector3(rng.Range(-40f, 40f), 320f, rng.Range(-40f, 40f)), position, 2.4f, 14, 14f, 0);
        _bolt.Position = Vector3.Zero;
        _bolt.Scale = Vector3.One;
        _bolt.SetInstances([.. pieces]);
        _bolt.Visible = true;
        _boltTimer = 0.2f;
    }

    public void Update(GameSession session, Vector3 cameraPosition, float dt, float nightGlow)
    {
        _stamp++;
        Vector2 eye = cameraPosition.Xz();

        UpdatePlayer(session.Player, dt);
        if (session.Player.Vehicle is null)
        {
            _cockpit.Visible = false;
        }

        foreach (Vehicle vehicle in session.Vehicles)
        {
            DrawVehicle(vehicle, eye, dt, session.Player.Vehicle == vehicle);
        }

        foreach (Vehicle vehicle in session.Population.Traffic)
        {
            DrawVehicle(vehicle, eye, dt, false);
        }

        foreach (List<Matrix4x4> list in _canopyInstances)
        {
            list.Clear();
        }

        _handleInstances.Clear();
        foreach (Pedestrian pedestrian in session.Population.Pedestrians)
        {
            if (Acquire(pedestrian, pedestrian.Model, HumanParts) is { } view)
            {
                view.Pivot.Position = pedestrian.Position;
                view.Pivot.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, pedestrian.Yaw);
                AnimateHuman(view, pedestrian.Speed, dt);
                if (pedestrian.Umbrella)
                {
                    // Held a little forward and to one side, bobbing with the walk.
                    Vector2 ahead = MathUtil.Forward(pedestrian.Yaw);
                    Vector2 side = MathUtil.Left(ahead);
                    float bob = MathF.Sin(view.Phase * 2f) * 0.015f * MathUtil.Saturate(pedestrian.Speed);
                    Vector3 hand = pedestrian.Position + new Vector3((ahead.X * 0.16f) - (side.X * 0.2f), bob, (ahead.Y * 0.16f) - (side.Y * 0.2f));
                    Matrix4x4 tilt = Matrix4x4.CreateRotationX(0.1f) * Matrix4x4.CreateRotationY(pedestrian.Yaw);
                    _canopyInstances[pedestrian.Id % _canopies.Length].Add(tilt * Matrix4x4.CreateTranslation(hand + new Vector3(0f, 2.02f, 0f)));
                    _handleInstances.Add(tilt * Matrix4x4.CreateTranslation(hand + new Vector3(0f, 1.5f, 0f)));
                }
            }
        }

        for (int i = 0; i < _canopies.Length; i++)
        {
            _canopies[i].Visible = _canopyInstances[i].Count > 0;
            if (_canopyInstances[i].Count > 0)
            {
                _canopies[i].SetInstances([.. _canopyInstances[i]]);
            }
        }

        _umbrellaHandles.Visible = _handleInstances.Count > 0;
        if (_handleInstances.Count > 0)
        {
            _umbrellaHandles.SetInstances([.. _handleInstances]);
        }

        DrawTrain(session.Train, eye);

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

        // Poses the skeletons and deforms their meshes.
        _scene.UpdateAnimations(dt);

        UpdateHeadlight(session, nightGlow);
        UpdateBeacons(session, eye, (float)session.Time);

        if (_boltTimer > 0f)
        {
            _boltTimer -= dt;
            _bolt.Visible = _boltTimer > 0f;
        }
    }

    // ------------------------------------------------------------------ train

    private void DrawTrain(Train? train, Vector2 eye)
    {
        bool near = false;
        if (train is not null)
        {
            for (int i = 0; i < Train.Cars; i++)
            {
                near |= Vector2.DistanceSquared(train.CarPose(i, out _, out _), eye) < 900f * 900f;
            }
        }

        foreach ((Node node, float height) in _trainParts)
        {
            node.Visible = near;
            if (!near)
            {
                continue;
            }

            for (int i = 0; i < Train.Cars; i++)
            {
                Vector2 at = train!.CarPose(i, out Vector2 direction, out float level);
                _trainMatrices[i] = Matrix4x4.CreateRotationY(MathUtil.YawOf(direction))
                    * Matrix4x4.CreateTranslation(at.X, level + 0.3f + height, at.Y);
            }

            node.SetInstances(_trainMatrices);
        }
    }

    // ------------------------------------------------------------------ player

    private void UpdatePlayer(Player player, float dt)
    {
        string model = player.Profile.Model;
        if (model != _playerModel)
        {
            _playerBody?.Remove();
            _playerBody = null;
            _player.Parts = [];
            if (_playerRig is not null)
            {
                _playerRig.Root.Visible = false;
            }

            _playerModel = model;
            if (!_rigs.TryGetValue(model, out _playerRig))
            {
                _rigs[model] = _playerRig = CreateRig(model, _player.Pivot);
            }

            if (_playerRig is not null)
            {
                _playerRig.Root.Visible = true;
            }
            else if (_models.Spawn(model, _player.Pivot) is { } body)
            {
                // No skeleton for this character: the sliced model, swung part by part.
                _playerBody = body;
                _player.Parts = [.. HumanParts.Select(part => ModelLibrary.FindPart(body, part))];
            }
        }

        // Inside a car the driver is not drawn; pushing a cart they walk behind it.
        bool riding = player.Vehicle is { Spec.IsCart: false };
        // Open vehicles show their driver: the motorbike, the becak, the delman.
        bool open = player.Vehicle?.Spec.Kind is VehicleKind.Motor or VehicleKind.Becak or VehicleKind.Delman;
        _player.Pivot.Visible = !HidePlayer && (!riding || open);
        Vector3 position = player.Position;
        if (player.Vehicle is { } ridden && RiderOf(ridden.Spec.Kind) is { } seat)
        {
            // Sit where the vehicle's usual rider sits.
            Vector2 ahead = MathUtil.Forward(ridden.Yaw);
            position += new Vector3(ahead.X * seat.Seat.Z, seat.Seat.Y + 0.25f, ahead.Y * seat.Seat.Z);
        }
        else if (player.InWater)
        {
            position.Y -= 0.1f;
        }

        _player.Pivot.Position = position;
        _player.Pivot.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, player.Yaw);
        if (_playerRig is not null)
        {
            AnimateRig(_playerRig, riding ? 0f : player.Speed, dt, sitting: riding);
        }
        else
        {
            AnimateHuman(_player, riding ? 0f : player.Speed, dt, sitting: riding);
        }
    }

    private Rig? CreateRig(string model, Node parent)
    {
        if (_models.SpawnRigged(model, parent) is not { Idle: not null } rigged)
        {
            return null;
        }

        // Started in the order they are layered. Idle runs by the clock; the others are scrubbed by hand.
        rigged.Idle.Play(loop: true);
        return new Rig
        {
            Root = rigged.Root,
            Walk = rigged.Walk?.Play(loop: true, speed: 0f, weight: 0f),
            Run = rigged.Run?.Play(loop: true, speed: 0f, weight: 0f),
            Sit = rigged.Sit?.Play(loop: true, speed: 0f, weight: 0f),
            WalkLength = rigged.Walk?.Duration ?? 1f,
            RunLength = rigged.Run?.Duration ?? 1f,
        };
    }

    private static void AnimateRig(Rig rig, float speed, float dt, bool sitting)
    {
        float run = sitting ? 0f : MathUtil.SmoothStep(2f, 4.6f, speed);
        float walk = sitting ? 0f : MathUtil.Saturate(speed / 0.9f);
        float ease = 1f - MathF.Exp(-dt * 10f);
        rig.WalkWeight += (walk - rig.WalkWeight) * ease;
        rig.RunWeight += (run - rig.RunWeight) * ease;
        rig.SitWeight += ((sitting ? 1f : 0f) - rig.SitWeight) * ease;

        // The feet cover the ground the body does: a walking stride (two steps) is about 1.5 m,
        // a running one 3.2 m.
        rig.Stride = (rig.Stride + (speed * dt / MathUtil.Lerp(1.5f, 3.2f, run))) % 1f;
        Scrub(rig.Walk, rig.Stride * rig.WalkLength, rig.WalkWeight);
        Scrub(rig.Run, rig.Stride * rig.RunLength, rig.RunWeight);
        Scrub(rig.Sit, 0f, rig.SitWeight);

        static void Scrub(AnimationPlayer? player, float time, float weight)
        {
            if (player is not null)
            {
                player.Time = time;
                player.Weight = weight < 0.004f ? 0f : weight;
            }
        }
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

        // Seen from the driver's seat, the body gives way to the cockpit.
        bool seated = drivenByPlayer && HidePlayer && RiderOf(vehicle.Spec.Kind) is null;
        view.Pivot.Visible = !seated;
        if (drivenByPlayer)
        {
            _cockpit.Visible = seated;
        }

        if (seated)
        {
            Vector3 seat = CameraRig.SeatOf(vehicle.Spec.Kind);
            Vector2 ahead = vehicle.Forward;
            Vector2 left = MathUtil.Left(ahead);
            _cockpit.Position = vehicle.Position + new Vector3((left.X * seat.X) + (ahead.X * seat.Z), seat.Y + 0.12f, (left.Y * seat.X) + (ahead.Y * seat.Z));
            // The cockpit is modelled with +X to the passenger side, which is the vehicle's left.
            _cockpit.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, vehicle.Yaw) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, -vehicle.Pitch);
            _steeringWheel.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 1.2f) * Quaternion.CreateFromAxisAngle(Vector3.UnitY, vehicle.SteerAngle * 2.4f);
            Vector4 paint = vehicle.Spec.Kind switch
            {
                VehicleKind.Angkot => new Vector4(0.12f, 0.62f, 0.34f, 1f),
                VehicleKind.Sedan => new Vector4(0.85f, 0.85f, 0.88f, 1f),
                VehicleKind.Truk => new Vector4(0.8f, 0.55f, 0.12f, 1f),
                VehicleKind.Bajaj or VehicleKind.Bemo => new Vector4(0.15f, 0.4f, 0.8f, 1f),
                VehicleKind.TahuBulat or VehicleKind.Bakso => new Vector4(0.9f, 0.9f, 0.85f, 1f),
                _ => new Vector4(0.9f, 0.9f, 0.92f, 1f),
            };
            if (_cockpitKind != vehicle.Spec.Kind)
            {
                _cockpitKind = vehicle.Spec.Kind;
                _cockpitPaint.Update(options => options with { BaseColor = paint });
            }
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
