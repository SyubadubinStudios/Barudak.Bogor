using System.Numerics;
using BarudakBogor.Activities;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Localization;
using BarudakBogor.Numerics;
using BarudakBogor.Sim;
using BarudakBogor.World;

namespace BarudakBogor.Missions;

/// <summary>
/// A car that hunts the player along the road graph (used by the prologue chase). A plain
/// pursuer follows; an interceptor heads for where the player is going, overtakes when it has
/// the legs and swings in front, so the two cars work as a pair: one pushes, one blocks.
/// </summary>
internal sealed class Pursuer(GameSession session, Vehicle vehicle, bool interceptor = false)
{
    private readonly List<int> _route = [];
    private RoadEdge? _edge;
    private int _direction = 1;
    private float _distance;
    private float _replan;
    private float _speed;

    public Vehicle Vehicle { get; } = vehicle;

    /// <summary>Seconds this pursuer has spent right next to a nearly stopped player.</summary>
    public float Cornered { get; private set; }

    public void Update(float dt)
    {
        WorldMap map = session.Map;
        Player player = session.Player;
        Vector2 position = Vehicle.Ground;
        Vector2 quarry = player.Ground;
        float gap = Vector2.Distance(position, quarry);
        Vector2 heading = player.Vehicle is { } ride && ride.Velocity.LengthSquared() > 4f
            ? Vector2.Normalize(ride.Velocity)
            : MathUtil.Forward(player.Yaw);

        // The interceptor aims ahead of the player: far away at where they will be in a few
        // seconds, close up at a point just off their front bumper.
        Vector2 target = quarry;
        // How far along the player's line of travel this car is: negative behind, positive in front.
        float lead = Vector2.Dot(position - quarry, heading);
        bool overtaking = interceptor && lead < 7f;
        if (interceptor)
        {
            target = gap > 28f
                ? quarry + (heading * MathF.Min(player.Speed * 3.5f, 90f))
                : quarry + (heading * 8f) + (MathUtil.Left(heading) * 1.5f);
        }

        // Rubber band: always a little slower than a well driven angkot, never left far behind.
        // An interceptor that has not yet got in front gets a little extra to draw past.
        float wanted = Math.Clamp((player.Speed * 0.88f) + 5f + (gap > 90f ? 8f : 0f) + (overtaking ? 3.5f : 0f), 7f, 24f);
        if (interceptor && !overtaking && gap < 28f)
        {
            // In front: ease off so the player has to brake or steer round.
            wanted = player.Speed * 0.85f;
        }
        else if (gap < 7f)
        {
            wanted = MathF.Min(wanted, player.Speed + (overtaking ? 2.5f : 0f));
        }

        _speed += Math.Clamp(wanted - _speed, -14f * dt, 6f * dt);
        Cornered = gap < 6.5f && player.Speed < 2.5f ? Cornered + dt : MathF.Max(0f, Cornered - (dt * 0.5f));

        if (gap < 28f)
        {
            // Close enough to see the target: drive straight at it.
            _edge = null;
            Vector2 toTarget = target - position;
            Vector2 direction = toTarget.LengthSquared() > 0.04f ? Vector2.Normalize(toTarget) : Vehicle.Forward;
            // Keep off the quarry's flank while overtaking instead of ramming through it.
            Vector2 away = position - quarry;
            if (interceptor && away.LengthSquared() is > 0.01f and < 16f)
            {
                direction = Vector2.Normalize(direction + (Vector2.Normalize(away) * 0.8f));
            }

            float yaw = MathUtil.TurnToward(Vehicle.Yaw, MathUtil.YawOf(direction), 2.4f * dt);
            Vector2 next = map.ResolveCircle(position + (MathUtil.Forward(yaw) * _speed * dt), Vehicle.Spec.Radius, out _);
            Vehicle.MoveKinematic(map, next, yaw, _speed, dt);
            return;
        }

        _replan -= dt;
        if (_edge is null || _replan <= 0f)
        {
            Plan(position, target);
        }

        if (_edge is null)
        {
            return;
        }

        _distance += _speed * _direction * dt;
        if (_distance > _edge.Length || _distance < 0f)
        {
            int node = _direction > 0 ? _edge.To : _edge.From;
            if (_route.Count > 0)
            {
                RoadEdge nextEdge = map.Roads.Edges[_route[0]];
                _route.RemoveAt(0);
                _edge = nextEdge;
                _direction = nextEdge.From == node ? 1 : -1;
                _distance = _direction > 0 ? 0f : nextEdge.Length;
            }
            else
            {
                Plan(position, target);
                if (_edge is null)
                {
                    return;
                }
            }
        }

        Vector2 point = _edge.Sample(_distance, out Vector2 along);
        Vector2 travel = along * _direction;
        Vector2 eased = Vector2.Lerp(position, point, MathUtil.Damp(5f, dt));
        Vehicle.MoveKinematic(map, eased, MathUtil.TurnToward(Vehicle.Yaw, MathUtil.YawOf(travel), 3f * dt), _speed, dt);
    }

    private void Plan(Vector2 position, Vector2 target)
    {
        WorldMap map = session.Map;
        _replan = 1.5f;
        if (map.Roads.Nearest(position, 200f, e => e.Kind != RoadKind.Gang) is not { } hit)
        {
            _edge = null;
            return;
        }

        // Already on the target's road: head along it. Routing to the nearest junction would
        // send the car the wrong way whenever that junction lies behind the target.
        if (map.Roads.Nearest(target, 200f, e => e.Kind != RoadKind.Gang) is { } there && there.Edge == hit.Edge)
        {
            _edge = hit.Edge;
            _distance = hit.Distance;
            _direction = there.Distance >= hit.Distance ? 1 : -1;
            _route.Clear();
            return;
        }

        RoadNode goal = map.Roads.NearestNode(target);
        // Leave the current edge through whichever end gives the shorter trip.
        List<int> viaTo = map.Roads.FindRoute(hit.Edge.To, goal.Id);
        List<int> viaFrom = map.Roads.FindRoute(hit.Edge.From, goal.Id);
        bool forward = hit.Edge.To == goal.Id || (viaTo.Count > 0 && (viaFrom.Count == 0 || viaTo.Count <= viaFrom.Count));
        _edge = hit.Edge;
        _distance = hit.Distance;
        _direction = forward ? 1 : -1;
        _route.Clear();
        _route.AddRange(forward ? viaTo : viaFrom);
    }
}

/// <summary>Opening story: the bag left behind in Ujang's angkot.</summary>
public sealed class PrologMission : Mission
{
    private readonly List<Pursuer> _pursuers = [];

    public override string Id => "prolog";

    public override string? GiverModel => "tas_misterius_hd";

    public override L Title => new("Tas Misterius", "The Mysterious Bag");

    public override L Summary => new(
        "Hujan deras di Tugu Kujang. Ada tas tertinggal di angkot, dan ada yang sangat menginginkannya kembali.",
        "Heavy rain at Tugu Kujang. A bag was left in the angkot, and someone wants it back badly.");

    public override PoiId StartPlace => PoiId.TuguKujang;

    public override long RewardMoney => 250_000;

    public override int RewardReputation => 10;

    public override string? Requires => null;

    protected override void Script()
    {
        L hero = new(S.Player.Profile.Name, S.Player.Profile.Name);
        L narrator = new("Narator", "Narrator");
        Vehicle? angkot = null;

        Do(() =>
        {
            S.Clock.Set(S.Clock.Day, 17.6f);
            S.Weather.Set(WeatherKind.HujanDeras, 600f);
            S.Weather.Locked = true;
            OnEnd(() => S.Weather.Locked = false);
            angkot = S.Vehicles.Where(v => v.Spec.Kind == VehicleKind.Angkot && !v.Occupied)
                .OrderBy(v => Vector2.DistanceSquared(v.Ground, S.Player.Ground)).FirstOrDefault()
                ?? S.SpawnVehicleAt(VehicleKind.Angkot, PoiId.TuguKujang, 6f);
        });
        Talk(narrator,
            new("Sore itu hujan deras mengguyur Tugu Kujang. Penumpang terakhir turun terburu-buru.",
                "That afternoon heavy rain poured over Tugu Kujang. The last passenger got off in a hurry."),
            new("Di jok belakang angkot, sesuatu tertinggal.", "On the back seat of the angkot, something was left behind."));
        Until(new("Periksa angkotmu di dekat Tugu Kujang", "Check your angkot near Tugu Kujang"),
            () => S.Player.Vehicle == angkot, () => angkot?.Ground);
        Talk(hero,
            new("Euleuh... tas saha ieu? Beurat pisan.", "Whoa... whose bag is this? It's so heavy."),
            new("DUIT?! Miliaran! Jeung... flashdisk? Tulisanna \"RAHASIA - ULAH DIBUKA\".",
                "CASH?! Billions! And... a flash drive? It says \"SECRET - DO NOT OPEN\"."),
            new("Eh, éta mobil hideung ti tadi nuturkeun. Waduh. KABUR!", "Hey, that black car has been following me. Uh-oh. RUN!"));
        Do(() =>
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 behind = angkot!.Ground - (angkot.Forward * (22f + (i * 12f)));
                behind = S.Map.Roads.Nearest(behind, 60f)?.Point ?? behind;
                Vehicle car = S.SpawnVehicle(VehicleKind.MobilKeluarga, behind, angkot.Yaw);
                car.Occupied = true;
                car.LightsOn = true;
                _pursuers.Add(new Pursuer(S, car, interceptor: i == 1));
                OnEnd(() => S.RemoveVehicle(car));
            }

            OnEnd(_pursuers.Clear);
        });
        Reach(new("Kabur ke kontrakan di Empang! Jangan sampai terkepung", "Escape to your room in Empang! Don't get boxed in"),
            () => S.Map[PoiId.RumahUjang].RoadPoint, 14f);
        Do(() =>
        {
            foreach (Pursuer pursuer in _pursuers)
            {
                S.RemoveVehicle(pursuer.Vehicle);
            }

            _pursuers.Clear();
        });
        Talk(hero,
            new("Hah... hah... lolos. Mobil hideungna nyasar ka Gang Selot, pasti macet tujuh turunan.",
                "Hah... hah... made it. The black car got lost in Selot Alley; it'll be stuck there for generations."),
            new("Tas ieu kudu dibalikeun... atawa diselidiki heula. Bogor, urang mimitian.",
                "This bag must be returned... or investigated first. Bogor, here we go."));
    }

    protected override void Tick(float dt)
    {
        foreach (Pursuer pursuer in _pursuers)
        {
            pursuer.Update(dt);
            if (pursuer.Cornered > 3f)
            {
                S.Missions.Fail(new("Terkepung! Tasnya direbut. Coba lagi dari Tugu Kujang.", "Boxed in! They took the bag. Try again from Tugu Kujang."));
                return;
            }
        }
    }
}

/// <summary>Cilok Terakhir: find Mang Udin's missing cilok cart.</summary>
public sealed class CilokMission : Mission
{
    public override string Id => "cilok";

    public override string? GiverModel => "npc_mang_udin";

    public override L Title => new("Cilok Terakhir", "The Last Cilok");

    public override L Summary => new(
        "Gerobak cilok Mang Udin hilang di Pasar Anyar. Tanpa cilok, Bogor bukan Bogor.",
        "Mang Udin's cilok cart has vanished from Pasar Anyar. Without cilok, Bogor isn't Bogor.");

    public override PoiId StartPlace => PoiId.PasarAnyar;

    public override long RewardMoney => 150_000;

    protected override void Script()
    {
        L udin = new("Mang Udin", "Mang Udin");
        L witness = new("Saksi", "Witness");
        Vector2 clue1 = RoadNear(PoiId.PasarAnyar, new Vector2(120f, 60f));
        Vector2 clue2 = RoadNear(PoiId.PasarAnyar, new Vector2(-140f, -90f));
        Vector2 clue3 = RoadNear(PoiId.PasarAnyar, new Vector2(-260f, 180f));
        Vector2 hideout = RoadNear(PoiId.GangSelot, new Vector2(60f, 40f));
        Vector2 home = S.Map[PoiId.PasarAnyar].RoadPoint;
        Vehicle? cart = null;

        Talk(udin,
            new("Kang, tulungan! Gerobak cilok abdi leungit! Padahal bumbu kacangna karék diulek.",
                "Help me! My cilok cart is gone! And I had just ground the peanut sauce."),
            new("Tadi abdi ngan saré sakedap... lima jam. Punten pangmilarikeun.",
                "I only napped for a moment... five hours. Please find it for me."));
        Reach(new("Tanya tukang parkir di pasar", "Ask the parking attendant at the market"), () => clue1);
        Talk(witness, new("Gerobak cilok? Tadi aya nu ngadorong ka kulon. Priiit! Parkirna dua rébu, Kang.",
            "A cilok cart? Somebody pushed it west. Tweet! Parking's two thousand, by the way."));
        Reach(new("Tanya ibu-ibu penjual sayur", "Ask the vegetable seller"), () => clue2);
        Talk(witness, new("Oh, éta mah budak-budak nu mawa. Cenah rék dipaké balap gerobak di Gang Selot.",
            "Oh, some kids took it. They said they'd use it for cart racing in Selot Alley."));
        Reach(new("Ikuti jejak saus kacang", "Follow the trail of peanut sauce"), () => clue3);
        Talk(witness, new("Meong. (Kucing ieu belepotan bumbu kacang. Jejakna ka arah Gang Selot.)",
            "Meow. (This cat is covered in peanut sauce. The trail leads to Selot Alley.)"));
        Do(() =>
        {
            cart = S.SpawnVehicle(VehicleKind.GerobakCilok, hideout, 0.6f);
            OnEnd(() => S.RemoveVehicle(cart));
        });
        Reach(new("Cari gerobak cilok di Gang Selot", "Find the cilok cart in Selot Alley"), () => hideout, 5f);
        Talk(new(S.Player.Profile.Name, S.Player.Profile.Name), new("Kapanggih! Masih haneut kénéh ciloknya. Hayu urang dorong balik.",
            "Found it! The cilok is still warm. Let's push it back."));
        Until(new("Dorong gerobak kembali ke Pasar Anyar", "Push the cart back to Pasar Anyar"),
            () => cart is not null && Vector2.Distance(cart.Ground, home) < 14f, () => home);
        Talk(udin,
            new("Alhamdulillah! Gerobak abdi! Ieu, cilok gratis saumur hirup... saminggu wé nya.",
                "Praise be! My cart! Here, free cilok for life... well, for a week."));
        Do(() => S.Progress.AddReputation(Career.Kuliner, 10));
    }
}

/// <summary>Narik Sampai Kaya: drive the IPB Dramaga - Jalan Baru angkot route.</summary>
public sealed class NarikMission : Mission
{
    private const long FarePerPassenger = 5_000;

    private readonly List<Vector2> _stops = [];
    private Vehicle? _angkot;
    private int _stop;
    private float _dwell;
    private long _income;

    public override string Id => "narik";

    public override string? GiverModel => "npc_haji_dadang";

    public override L Title => new("Narik Sampai Kaya", "Drive Till You're Rich");

    public override L Summary => new(
        "Jadi sopir angkot trayek Kampus Dalam: IPB Dramaga sampai Jalan Baru. Kejar setoran!",
        "Drive the Kampus Dalam angkot route from IPB Dramaga to Jalan Baru. Chase that daily quota!");

    public override PoiId StartPlace => PoiId.TerminalDramaga;

    public override long RewardMoney => 120_000;

    public override Career RewardCareer => Career.Angkot;

    public override int RewardReputation => 35;

    protected override void Script()
    {
        L boss = new("Juragan Haji Dadang", "Boss Haji Dadang");
        _stops.Clear();
        _stop = 0;
        _income = 0;
        _dwell = 0f;

        Talk(boss,
            new("Narik trayek Dramaga - Jalan Baru nya. Setoran genep puluh rébu. Sésana keur manéh.",
                "Take the Dramaga - Jalan Baru route. The quota is sixty thousand. The rest is yours."),
            new("Eureun di unggal halte. Ulah ngetem lila teuing, karunya penumpang!",
                "Stop at every halte. Don't wait around too long, think of the passengers!"));
        Do(() =>
        {
            _angkot = S.Vehicles.Where(v => v.Spec.Kind == VehicleKind.Angkot && !v.Occupied)
                .OrderBy(v => Vector2.DistanceSquared(v.Ground, S.Player.Ground)).FirstOrDefault();
            if (_angkot is null || Vector2.Distance(_angkot.Ground, S.Player.Ground) > 80f)
            {
                _angkot = S.SpawnVehicleAt(VehicleKind.Angkot, PoiId.TerminalDramaga, 8f);
            }

            _angkot.Passengers = 0;
            BuildStops();
        });
        Until(new("Naik ke angkot", "Get in the angkot"), () => S.Player.Vehicle == _angkot, () => _angkot?.Ground);
        Step(new("Berhenti di halte berikutnya", "Stop at the next halte"), () => _stop < _stops.Count ? _stops[_stop] : null, null, ServeStops, null, 8f);
        Talk(boss, new("Tah kitu! Sopir teladan. Isukan narik deui nya.", "That's the way! A model driver. Same again tomorrow."));
    }

    private void BuildStops()
    {
        WorldMap map = S.Map;
        RoadNode from = map.Roads.NearestNode(map[PoiId.TerminalDramaga].RoadPoint);
        RoadNode to = map.Roads.NearestNode(map[PoiId.JalanBaru].RoadPoint);
        int node = from.Id;
        float sinceLast = 300f;
        foreach (int edgeId in map.Roads.FindRoute(from.Id, to.Id))
        {
            RoadEdge edge = map.Roads.Edges[edgeId];
            bool forward = edge.From == node;
            for (float d = 0f; d < edge.Length; d += 30f)
            {
                sinceLast += 30f;
                if (sinceLast >= 800f)
                {
                    sinceLast = 0f;
                    _stops.Add(edge.Sample(forward ? d : edge.Length - d, out _));
                }
            }

            node = RoadNetwork.OtherEnd(edge, node);
        }

        _stops.Add(map[PoiId.JalanBaru].RoadPoint);
    }

    private bool ServeStops(float dt)
    {
        if (_angkot is null || S.Player.Vehicle != _angkot)
        {
            return false;
        }

        Vector2 stop = _stops[_stop];
        bool waiting = Vector2.Distance(_angkot.Ground, stop) < 9f && MathF.Abs(_angkot.Speed) < 1.2f;
        _dwell = waiting ? _dwell + dt : 0f;
        if (_dwell < 1.2f)
        {
            return false;
        }

        _dwell = 0f;
        bool last = _stop == _stops.Count - 1;
        int off = last ? _angkot.Passengers : S.Random(0, _angkot.Passengers + 1);
        int on = last ? 0 : Math.Min(S.Random(1, 5), _angkot.Spec.Seats - (_angkot.Passengers - off));
        _angkot.Passengers += on - off;
        if (off > 0)
        {
            long fare = (long)(off * FarePerPassenger * (1f + S.Player.Profile.DrivingBonus));
            _income += fare;
            S.Progress.Earn(fare);
            S.Progress.PassengersCarried += off;
        }

        S.Emit(new GameEvent(GameEventKind.Pickup, L.Empty, _angkot.Position));
        S.Toast(new L("Halte {0}/{1}: naik {2}, turun {3}. Penumpang {4}. Pendapatan {5}",
            "Stop {0}/{1}: {2} on, {3} off. Passengers {4}. Income {5}")
            .With(_stop + 1, _stops.Count, on, off, _angkot.Passengers, Loc.Rupiah(_income)));
        _stop++;
        return last;
    }
}

/// <summary>Anak Bina Insani: get the kids to school on time through rain and traffic.</summary>
public sealed class BinaInsaniMission : Mission
{
    private float _minutesBefore = 1f;

    public override string Id => "binainsani";

    public override string? GiverModel => "npc_bu_euis";

    public override L Title => new("Anak Bina Insani", "The Bina Insani Kids");

    public override L Summary => new(
        "Antar tiga siswa ke sekolah sebelum bel jam tujuh. Hujan, macet, dan PR yang ketinggalan.",
        "Get three pupils to school before the seven o'clock bell. Rain, traffic, and forgotten homework.");

    public override PoiId StartPlace => PoiId.Empang;

    public override long RewardMoney => 120_000;

    public override int RewardReputation => 20;

    protected override L TimeoutReason => new("Bel sudah bunyi. Anak-anak dihukum hormat bendera.", "The bell has rung. The kids are saluting the flag as punishment.");

    protected override void Script()
    {
        L mother = new("Bu Euis", "Bu Euis");
        L kid = new("Dede", "Dede");
        Vector2 a = RoadNear(PoiId.Empang, new Vector2(160f, -120f));
        Vector2 b = RoadNear(PoiId.RumahUjang, new Vector2(260f, -260f));
        Vector2 c = RoadNear(PoiId.TuguKujang, new Vector2(-520f, 380f));
        Vector2 school = S.Map[PoiId.SekolahBinaInsani].RoadPoint;
        bool Riding() => S.Player.Vehicle is { Spec.IsCart: false } v && MathF.Abs(v.Speed) < 2f;

        Talk(mother,
            new("Kang, tulungan! Mobil jemputan mogok. Barudak kudu nepi ka Bina Insani saméméh jam tujuh!",
                "Please help! The school shuttle broke down. The kids must reach Bina Insani before seven!"),
            new("Jemput tilu budak, terus ngebut... eh, ulah ngebut. Tapi gancang!",
                "Pick up three kids, then floor it... no, don't floor it. But be quick!"));
        Do(() =>
        {
            _minutesBefore = S.Clock.MinutesPerSecond;
            S.Clock.Set(S.Clock.Day + (S.Clock.Hours > 6.2f ? 1 : 0), 6.2f);
            // 48 game minutes in 240 real seconds: the HUD clock itself is the deadline.
            S.Clock.MinutesPerSecond = 0.2f;
            S.Weather.Set(WeatherKind.HujanSedang, 600f);
            S.Weather.Locked = true;
            TimeLeft = 240f;
            OnEnd(() =>
            {
                S.Clock.MinutesPerSecond = _minutesBefore;
                S.Weather.Locked = false;
            });
        });
        Until(new("Cari kendaraan (bukan gerobak!)", "Find a vehicle (not a cart!)"), () => S.Player.Vehicle is { Spec.IsCart: false });
        Reach(new("Jemput Dede di gang depan", "Pick up Dede at the alley"), () => a, 8f, Riding);
        Talk(kid, new("Kang, PR abdi katinggaleun... Ah, engké wé nyalin di kelas.", "My homework's at home... Never mind, I'll copy it in class."));
        Reach(new("Jemput si kembar Asep & Usep", "Pick up the twins Asep & Usep"), () => b, 8f, Riding);
        Do(() =>
        {
            if (S.Player.Vehicle?.Spec.Kind == VehicleKind.Motor)
            {
                S.Toast(new("Bonceng tilu naék motor. Pak Polisi, punten nya...", "Three kids on one motorbike. Sorry, officer..."));
            }
        });
        Reach(new("Jemput Neng Sari di dekat Tugu", "Pick up Sari near the monument"), () => c, 8f, Riding);
        Reach(new("Antar ke Sekolah Bina Insani sebelum 07:00", "Drive to Bina Insani School before 07:00"), () => school, 10f, Riding);
        Do(() => TimeLeft = null);
        Talk(kid, new("Nuhun, Kang! Pas pisan bel disada. Isukan jemput deui nya!", "Thanks! The bell is ringing right now. Pick us up again tomorrow!"));
    }
}

/// <summary>STM vs STM: stop a school brawl without throwing a punch.</summary>
public sealed class StmMission : Mission
{
    private int _approach = -1;
    private bool _talkedDown;
    private bool _speechFailed;

    public override string Id => "stm";

    public override string? GiverModel => "npc_guru_bp";

    public override L Title => new("STM vs STM", "STM vs STM");

    public override L Summary => new(
        "Dua STM siap tawuran. Cegah dengan cara kreatif: ceramah, traktiran, atau tahu bulat.",
        "Two vocational schools are about to brawl. Stop it creatively: a speech, a treat, or tahu bulat.");

    public override PoiId StartPlace => PoiId.LapanganStm;

    public override long RewardMoney => 100_000;

    public override int RewardReputation => 25;

    protected override void Script()
    {
        L teacher = new("Pak Guru BP", "The School Counsellor");
        L hero = new(S.Player.Profile.Name, S.Player.Profile.Name);
        L crowd = new("Barudak STM", "The STM Kids");
        Vector2 field = S.Map[PoiId.LapanganStm].RoadPoint;
        Vector2 stall = S.Map[PoiId.WarungBandrek].RoadPoint;
        Vector2 campA = RoadNear(PoiId.LapanganStm, new Vector2(150f, 40f));
        Vector2 campB = RoadNear(PoiId.LapanganStm, new Vector2(-140f, -60f));
        _approach = -1;
        _talkedDown = false;
        _speechFailed = false;
        bool smooth = S.Player.Profile.Id is CharacterId.Deden or CharacterId.Mumu;
        bool InTruck() => S.Player.Vehicle?.Spec.Kind == VehicleKind.TahuBulat;

        Talk(teacher,
            new("Gawat! Barudak STM Wétan jeung STM Kulon rék tawuran di lapang. Gara-garana rebutan WiFi warnet.",
                "Disaster! The East STM and West STM kids are about to brawl on the field. Over internet-café WiFi."),
            new("Tulungan pegatkeun. Ulah make kekerasan. Abdi mah geus kolot, teu kuat lumpat.",
                "Please break it up. No violence. I'm too old to run."));
        Ask(hero, new("Kumaha carana?", "How do we do this?"),
        [
            new("Ceramahin baik-baik", "Give them a heartfelt speech"),
            new("Traktir bandrek sekampung (Rp 40.000)", "Buy everyone bandrek (Rp 40,000)"),
            new("Panggil mobil tahu bulat", "Bring in the tahu bulat truck"),
        ], choice => _approach = choice);

        // 0: the speech. Works at once for the smooth talkers, otherwise it backfires into option 1.
        Do(() =>
        {
            _talkedDown = smooth || S.Chance(0.5f);
            if (!_talkedDown)
            {
                _speechFailed = true;
                _approach = 1;
            }
        }, () => _approach == 0);
        TalkWhen(() => _approach == 0 && _talkedDown, hero,
            new("Barudak! Tawuran mah moal nyieun WiFi leuwih gancang. Mending mabar, sarua-sarua lag.",
                "Guys! Brawling won't make the WiFi faster. Better play together, we all lag the same."));
        TalkWhen(() => _speechFailed, crowd,
            new("\"Lapar, Kang. Ceramah mah teu matak wareg.\"", "\"We're hungry. Speeches don't fill stomachs.\""));

        // 1: the treat.
        Reach(new("Beli bandrek di Warung Bandrek Aceng", "Buy bandrek at Aceng's stall"), () => stall, 8f, null, () => _approach == 1);
        Do(() =>
        {
            if (!S.Progress.TrySpend(40_000))
            {
                S.Toast(new("Duitna kurang. Ngutang heula ka Aceng, dicatet!", "Short on cash. Aceng puts it on your tab!"));
            }
            else
            {
                S.Toast(new("Dua puluh gelas bandrek haneut siap dianteur.", "Twenty cups of hot bandrek, ready to go."));
            }
        }, () => _approach == 1);
        Reach(new("Bawa bandrek ke lapangan", "Take the bandrek to the field"), () => field, 9f, null, () => _approach == 1);

        // 2: the tahu bulat truck.
        Until(new("Naik mobil tahu bulat di dekat lapangan", "Get in the tahu bulat truck by the field"), InTruck,
            () => S.Vehicles.FirstOrDefault(v => v.Spec.Kind == VehicleKind.TahuBulat)?.Ground, () => _approach == 2);
        Reach(new("Lewati kubu STM Wétan sambil bunyikan jingle", "Drive past the East STM camp with the jingle on"), () => campA, 10f, InTruck, () => _approach == 2);
        Reach(new("Lewati kubu STM Kulon", "Drive past the West STM camp"), () => campB, 10f, InTruck, () => _approach == 2);
        TalkWhen(() => _approach == 2, crowd,
            new("\"TAHU BULAT! DIGORENG DADAKAN! LIMA RATUSAN!\" ...Sadayana ngantri. Tawuran bubar.",
                "\"TAHU BULAT! FRIED ON THE SPOT! FIVE HUNDRED EACH!\" ...Everyone queues up. The brawl dissolves."));

        Talk(teacher,
            new("Hadé pisan! Ayeuna maranéhna malah nyieun turnamén futsal. Juarana meunang WiFi sabulan.",
                "Wonderful! Now they're holding a futsal tournament instead. The winner gets a month of WiFi."));
    }
}

/// <summary>Master Empang: the biggest fishing tournament in Bogor.</summary>
public sealed class EmpangMission : Mission
{
    private const float TargetKg = 4f;

    private FishingGame? _game;

    public override string Id => "empang";

    public override string? GiverModel => "npc_haji_dudung";

    public override L Title => new("Master Empang", "Master of the Pond");

    public override L Summary => new(
        "Turnamen mancing terbesar se-Bogor. Kumpulkan 4 kg ikan dalam waktu terbatas.",
        "The biggest fishing tournament in Bogor. Land 4 kg of fish before time runs out.");

    public override PoiId StartPlace => PoiId.Empang;

    public override long RewardMoney => 500_000;

    public override Career RewardCareer => Career.Mancing;

    public override int RewardReputation => 40;

    protected override void Script()
    {
        L host = new("Haji Dudung", "Haji Dudung");
        Vector2 spot = S.Map[PoiId.Empang].Position;

        Talk(host,
            new("Wilujeng sumping di Turnamén Master Empang! Hadiahna satengah juta jeung piala tina panci.",
                "Welcome to the Master Empang Tournament! The prize is half a million and a trophy made from a pot."),
            new("Opat kilo dina tilu menit. Sendal jepit teu diitung!", "Four kilos in three minutes. Flip-flops don't count!"));
        Reach(new("Jalan kaki ke saung pancing", "Walk to the fishing hut"), () => spot, 6f, () => S.Player.Vehicle is null);
        Do(() =>
        {
            _game = new FishingGame((uint)S.Random(1, int.MaxValue), S.Player.Profile.FishingBonus, S.Weather.Rain) { TimeLeft = 180f };
            S.StartActivity(_game);
        });
        Until(new L("Tangkap ikan total {0} kg", "Land {0} kg of fish in total").With(TargetKg), () => _game is { Finished: true });
        Do(() =>
        {
            if (_game!.TotalKg < TargetKg)
            {
                S.Missions.Fail(new L("Cuma {0:0.0} kg. Juaranya Abah Kolot lagi. Coba lagi!", "Only {0:0.0} kg. Old Abah wins again. Try again!").With(_game.TotalKg));
            }
        });
        Talk(host, new("JUARA! Piala panci keur anjeun. Ulah dipaké masak mi nya, éta pusaka.",
            "CHAMPION! The pot trophy is yours. Don't cook noodles in it, it's an heirloom."));
    }
}

/// <summary>Bandrek King: the first day of a business empire.</summary>
public sealed class BandrekMission : Mission
{
    private long _soldAtStart;

    public override string Id => "bandrek";

    public override string? GiverModel => "npc_abah_aceng";

    public override L Title => new("Bandrek King", "Bandrek King");

    public override L Summary => new(
        "Bangun kerajaan bandrek dari gerobak dorong. Langkah pertama: jual delapan gelas.",
        "Build a bandrek empire from a push cart. First step: sell eight cups.");

    public override PoiId StartPlace => PoiId.WarungBandrek;

    public override long RewardMoney => 200_000;

    public override Career RewardCareer => Career.Bandrek;

    public override int RewardReputation => 20;

    protected override void Script()
    {
        L abah = new("Abah Aceng", "Abah Aceng");
        Vector2 stall = S.Map[PoiId.WarungBandrek].RoadPoint;
        bool Pushing() => S.Player.Vehicle?.Spec.Kind == VehicleKind.GerobakBandrek;

        Talk(abah,
            new("Rahasia bandrek Abah: jahé beureum, gula aren, jeung sabar. Ayeuna giliran anjeun nu dagang.",
                "Abah's bandrek secret: red ginger, palm sugar, and patience. Now it's your turn to sell."),
            new("Dorong gerobak ka tempat nu ramé, terus eureun. Lamun hujan, laris manis!",
                "Push the cart somewhere busy, then stop. When it rains, it sells like crazy!"));
        Do(() =>
        {
            if (!S.Vehicles.Any(v => v.Spec.Kind == VehicleKind.GerobakBandrek))
            {
                S.SpawnVehicleAt(VehicleKind.GerobakBandrek, PoiId.WarungBandrek, 5f);
            }

            S.Business.Stock = Math.Max(S.Business.Stock, 12);
            _soldAtStart = S.Business.CupsSold;
        });
        Until(new("Dorong gerobak bandrek", "Push the bandrek cart"), Pushing,
            () => S.Vehicles.FirstOrDefault(v => v.Spec.Kind == VehicleKind.GerobakBandrek)?.Ground);
        Until(new("Berhenti di tempat ramai dan jual 8 gelas", "Stop somewhere busy and sell 8 cups"),
            () => S.Business.CupsSold - _soldAtStart >= 8);
        Reach(new("Kembali ke warung", "Return to the stall"), () => stall, 12f);
        Talk(abah,
            new("Tah, geus jadi tukang bandrek sajati. Kumpulkeun modal, engké urang muka warung, kedai, nepi ka franchise!",
                "There, a true bandrek seller. Save up capital and we'll open a stall, a café, even a franchise!"));
    }
}

/// <summary>Horor Puncak: the mysterious villa in the fog.</summary>
public sealed class HororMission : Mission
{
    public override string Id => "horor";

    public override string? GiverModel => "npc_mang_jagung";

    public override L Title => new("Horor Puncak", "Puncak Horror");

    public override L Summary => new(
        "Villa tua di tengah kabut Puncak katanya berhantu. Selidiki... kalau berani.",
        "They say the old villa in the Puncak fog is haunted. Investigate... if you dare.");

    public override PoiId StartPlace => PoiId.PuncakPass;

    public override long RewardMoney => 180_000;

    protected override void Script()
    {
        L seller = new("Mang Jagung Bakar", "The Grilled-Corn Seller");
        L hero = new(S.Player.Profile.Name, S.Player.Profile.Name);
        L unknown = new("???", "???");
        Poi villa = S.Map[PoiId.VillaMisterius];
        Vector2 gate = villa.RoadPoint;
        Vector2 front = villa.Position + (MathUtil.Forward(villa.Yaw) * 11f);
        Vector2 sideYard = villa.Position + (MathUtil.Left(MathUtil.Forward(villa.Yaw)) * 14f);
        Vector2 back = villa.Position - (MathUtil.Forward(villa.Yaw) * 13f);
        bool OnFoot() => S.Player.Vehicle is null;

        Talk(seller,
            new("Villa nu di luhur téh... unggal peuting aya sora awéwé nyanyi. Jeung aya nu bodas-bodas ngalayang.",
                "That villa up there... every night a woman's voice sings. And something white floats about."),
            new("Wani teu nalungtik? Engké ku abdi dibéré jagung bakar gratis.", "Dare to look into it? I'll give you free grilled corn."));
        Do(() =>
        {
            S.Clock.Set(S.Clock.Day, 21f);
            S.Weather.Set(WeatherKind.Gerimis, 900f);
            S.Weather.Locked = true;
            OnEnd(() => S.Weather.Locked = false);
        });
        Reach(new("Pergi ke Villa Misterius", "Go to the Mysterious Villa"), () => gate, 12f);
        Reach(new("Periksa teras depan (jalan kaki)", "Check the front porch (on foot)"), () => front, 5f, OnFoot);
        Talk(hero, new("Aya tapak suku... leutik. Opat. Hantu nanahaon sukuna opat?",
            "There are footprints... small ones. Four of them. What kind of ghost has four feet?"));
        Reach(new("Periksa halaman samping", "Check the side yard"), () => sideYard, 5f, OnFoot);
        Do(() => S.Weather.Strike(S.Player.Ground, blackout: true));
        Talk(unknown, new("\"...kemesraan iniii... janganlah cepat berlaluuu...\"",
            "\"...this tenderness... please don't let it end so soon...\""));
        Talk(hero, new("EUWEUH LISTRIK TAPI AYA NU NYANYI?! ...Tunggu. Éta sora spiker bluetooth.",
            "THE POWER'S OUT BUT SOMEONE IS SINGING?! ...Wait. That's a bluetooth speaker."));
        Reach(new("Ikuti suara ke belakang villa", "Follow the voice behind the villa"), () => back, 5f, OnFoot);
        Talk(unknown, new("Mbééé.", "Baaa."));
        Talk(hero,
            new("Embé. Embé make mukena, nyangkut tina jemuran. Jeung penjaga villa keur karaokéan.",
                "A goat. A goat tangled in a white prayer robe from the clothesline. And the caretaker doing karaoke."),
            new("\"Punten, Kang. Tong béja-béja nya, isin.\" Misteri Puncak: terpecahkan.",
                "\"Sorry about that. Please don't tell anyone, it's embarrassing.\" The Puncak mystery: solved."));
        Do(() => S.Progress.AddReputation(Career.Kuliner, 5));
    }
}
