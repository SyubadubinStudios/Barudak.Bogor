using System.Numerics;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Localization;
using BarudakBogor.Numerics;
using BarudakBogor.Sim;
using BarudakBogor.World;

namespace BarudakBogor.Activities;

/// <summary>
/// Ojol Simulator: go on duty on a motorbike and orders arrive - passengers and food.
/// Rain means more orders and better pay, exactly when you least want to ride.
/// </summary>
public sealed class OjolService
{
    private static readonly L[] Foods =
    [
        new("Soto Mie Bogor", "Bogor Noodle Soto"), new("Toge Goreng", "Toge Goreng"), new("Asinan Bogor", "Bogor Pickled Salad"),
        new("Cungkring", "Cungkring"), new("Doclang", "Doclang"), new("Roti Unyil", "Unyil Buns"), new("Laksa Bogor", "Bogor Laksa"),
    ];

    private static readonly string[] Riders = ["Teh Rina", "Kang Asep", "Bu Hajjah Euis", "A Dadan", "Neng Lilis", "Pak RT"];

    private float _wait = 4f;

    public bool OnDuty { get; private set; }

    /// <summary>True from pick-up until drop-off.</summary>
    public bool Carrying { get; private set; }

    public bool HasOrder { get; private set; }

    public bool IsFood { get; private set; }

    public L Subject { get; private set; }

    public Vector2 Pickup { get; private set; }

    public Vector2 Dropoff { get; private set; }

    public float TimeLeft { get; private set; }

    public long Fare { get; private set; }

    public L Objective => !HasOrder
        ? new("Nunggu orderan masuk...", "Waiting for an order...")
        : Carrying
            ? (IsFood ? new L("Antar {0} ke pemesan", "Deliver the {0} to the customer") : new L("Antar {0} ke tujuan", "Take {0} to the destination")).With(Subject.Text)
            : (IsFood ? new L("Ambil pesanan {0}", "Pick up the {0}") : new L("Jemput {0}", "Pick up {0}")).With(Subject.Text);

    public void Toggle(GameSession session)
    {
        if (!OnDuty && session.Player.Vehicle?.Spec.Kind != VehicleKind.Motor)
        {
            session.Toast(new("Ojol mah kudu naik motor atuh.", "You need to be on a motorbike to go on duty."));
            return;
        }

        OnDuty = !OnDuty;
        HasOrder = false;
        Carrying = false;
        _wait = 3f;
        session.Toast(OnDuty
            ? new("Aplikasi ojol ON. Semoga orderan gacor!", "Ojol app ON. May the orders flow!")
            : new("Aplikasi ojol OFF. Ngopi heula.", "Ojol app OFF. Coffee break."));
    }

    public void Update(GameSession session, float dt)
    {
        if (!OnDuty)
        {
            return;
        }

        Player player = session.Player;
        if (player.Vehicle?.Spec.Kind != VehicleKind.Motor && !HasOrder)
        {
            OnDuty = false;
            return;
        }

        if (!HasOrder)
        {
            _wait -= dt * (1f + session.Weather.Rain);
            if (_wait <= 0f)
            {
                NewOrder(session);
            }

            return;
        }

        TimeLeft -= dt;
        if (TimeLeft <= 0f)
        {
            HasOrder = false;
            Carrying = false;
            _wait = 6f;
            session.Toast(new("Order dibatalkan. Bintang satu, Kang...", "Order cancelled. One star, mate..."));
            return;
        }

        Vector2 target = Carrying ? Dropoff : Pickup;
        if (Vector2.Distance(player.Ground, target) < 7f && player.Speed < 3f)
        {
            if (!Carrying)
            {
                Carrying = true;
                session.Emit(new GameEvent(GameEventKind.Pickup, L.Empty, player.Position));
                session.Toast(IsFood
                    ? new("Pesanan siap. Jangan sampai kuahnya tumpah!", "Order ready. Don't spill the broth!")
                    : new("\"Sesuai aplikasi ya, Kang.\"", "\"As per the app, please.\""));
            }
            else
            {
                Complete(session);
            }
        }

        session.Markers.Add(new Marker(Carrying ? MarkerKind.Objective : MarkerKind.Passenger, target, Objective, 5f));
    }

    private void NewOrder(GameSession session)
    {
        Vector2 here = session.Player.Ground;
        if (session.RandomRoadPoint(here, 130f, 450f) is not { } pickup || session.RandomRoadPoint(pickup, 350f, 1_300f) is not { } dropoff)
        {
            _wait = 1f;
            return;
        }

        IsFood = session.Chance(0.5f);
        string rider = Riders[session.Random(0, Riders.Length)];
        Subject = IsFood ? Foods[session.Random(0, Foods.Length)] : new L(rider, rider);

        Pickup = pickup;
        Dropoff = dropoff;
        float distance = Vector2.Distance(here, pickup) + Vector2.Distance(pickup, dropoff);
        TimeLeft = 45f + (distance / 6.5f);
        Fare = (long)((8_000 + (distance * 9f)) * (1f + (0.5f * session.Weather.Rain))) / 500 * 500;
        HasOrder = true;
        Carrying = false;
        session.Toast(new L("Order masuk! {0} - {1}", "New order! {0} - {1}").With(Subject.Text, Loc.Rupiah(Fare)));
    }

    private void Complete(GameSession session)
    {
        long tip = TimeLeft > 25f ? 3_000 : 0;
        session.Reward(Fare + tip, IsFood ? Career.Kuliner : Career.Warga, IsFood ? 4 : 3);
        session.Progress.OjolOrders++;
        session.Toast(tip > 0
            ? new L("Order beres! +{0} (tip {1}). Bintang lima!", "Order done! +{0} (tip {1}). Five stars!").With(Loc.Rupiah(Fare), Loc.Rupiah(tip))
            : new L("Order beres! +{0}", "Order done! +{0}").With(Loc.Rupiah(Fare)));
        HasOrder = false;
        Carrying = false;
        _wait = session.Range(4f, 9f);
    }
}

/// <summary>Touring Puncak: a timed ride up the hairpins from Ciawi to Puncak Pass.</summary>
public sealed class TouringRace
{
    private readonly List<Vector2> _checkpoints = [];
    private int _next;

    public bool Active { get; private set; }

    public float Elapsed { get; private set; }

    public float ParTime { get; private set; }

    public int Checkpoint => _next;

    public int Count => _checkpoints.Count;

    public L Objective => new L("Touring Puncak: checkpoint {0}/{1}", "Puncak Touring: checkpoint {0}/{1}").With(_next + 1, Count);

    public void Start(GameSession session)
    {
        if (session.Player.Vehicle is not { Spec.Motorised: true })
        {
            session.Toast(new("Touring mah kudu bawa kendaraan bermesin.", "You need a motor vehicle to go touring."));
            return;
        }

        WorldMap map = session.Map;
        RoadNode from = map.Roads.NearestNode(map[PoiId.Ciawi].RoadPoint);
        RoadNode to = map.Roads.NearestNode(map[PoiId.PuncakPass].RoadPoint);
        List<int> route = map.Roads.FindRoute(from.Id, to.Id);
        _checkpoints.Clear();
        float length = 0f;
        float sinceLast = 0f;
        int node = from.Id;
        foreach (int edgeId in route)
        {
            RoadEdge edge = map.Roads.Edges[edgeId];
            bool forward = edge.From == node;
            for (float d = 0f; d < edge.Length; d += 40f)
            {
                sinceLast += 40f;
                if (sinceLast >= 900f)
                {
                    sinceLast = 0f;
                    _checkpoints.Add(edge.Sample(forward ? d : edge.Length - d, out _));
                }
            }

            length += edge.Length;
            node = RoadNetwork.OtherEnd(edge, node);
        }

        _checkpoints.Add(map[PoiId.PuncakPass].RoadPoint);
        _next = 0;
        Elapsed = 0f;
        ParTime = length / 15f;
        Active = true;
        session.Toast(new("Touring dimulai! Hati-hati tikungan, jalanan licin.", "Touring started! Mind the hairpins, the road is slick."));
    }

    public void Update(GameSession session, float dt)
    {
        if (!Active)
        {
            return;
        }

        Elapsed += dt;
        Vector2 target = _checkpoints[_next];
        session.Markers.Add(new Marker(MarkerKind.Objective, target, Objective, 9f));
        if (Vector2.Distance(session.Player.Ground, target) < 14f)
        {
            _next++;
            session.Emit(new GameEvent(GameEventKind.Pickup, L.Empty, session.Player.Position));
            if (_next >= _checkpoints.Count)
            {
                Active = false;
                float ratio = ParTime / MathF.Max(Elapsed, 1f);
                long prize = (long)(Math.Clamp(ratio, 0.4f, 1.6f) * 150_000) / 1_000 * 1_000;
                session.Reward(prize, Career.Warga, ratio >= 1f ? 10 : 5);
                session.Toast(new L("Sampai Puncak Pass dalam {0}! Hadiah {1}", "Reached Puncak Pass in {0}! Prize {1}")
                    .With(FormatTime(Elapsed), Loc.Rupiah(prize)));
            }
        }
    }

    public void Abort() => Active = false;

    public static string FormatTime(float seconds) => $"{(int)(seconds / 60f):00}:{(int)(seconds % 60f):00}";
}

/// <summary>Result of pressing the shutter.</summary>
public sealed record PhotoResult(int Score, IReadOnlyList<L> Tags, long Payment);

/// <summary>
/// Fotografi Hujan Bogor: a photo is scored on what is in frame and on the weather.
/// Rain, lightning, fog and landmarks make a picture worth posting.
/// </summary>
public static class Photography
{
    public static PhotoResult Shoot(GameSession session, Vector3 cameraPosition, Vector3 cameraForward)
    {
        List<L> tags = [];
        int score = 10;
        WeatherSystem weather = session.Weather;
        float hours = session.Clock.Hours;
        Vector2 eye = cameraPosition.Xz();
        Vector2 look = cameraForward.Xz();
        look = look.LengthSquared() > 1e-4f ? Vector2.Normalize(look) : Vector2.UnitY;

        if (weather.Rain > 0.15f)
        {
            score += (int)(35f * weather.Rain);
            tags.Add(weather.Rain > 0.7f ? new("Hujan deras", "Heavy rain") : new("Hujan", "Rain"));
        }

        if (weather.Flash > 0.25f)
        {
            score += 45;
            tags.Add(new("Kilat tertangkap!", "Lightning caught!"));
        }

        if (weather.FogAt(cameraPosition.Y, hours) > 0.4f)
        {
            score += 25;
            tags.Add(new("Kabut Puncak", "Puncak fog"));
        }

        if (hours is (>= 5.5f and < 7f) or (>= 17f and < 18.6f))
        {
            score += 20;
            tags.Add(new("Golden hour", "Golden hour"));
        }
        else if (session.Clock.IsNight)
        {
            score += 10;
            tags.Add(new("Malam", "Night"));
        }

        foreach (Poi poi in session.Map.Pois)
        {
            Vector2 offset = poi.Position - eye;
            float distance = offset.Length();
            if (distance is > 4f and < 240f && Vector2.Dot(offset / distance, look) > 0.72f)
            {
                score += 40;
                tags.Add(Loc.Name(poi.NameKey));
                if (tags.Count > 6)
                {
                    break;
                }
            }
        }

        int animals = 0;
        foreach (Animal animal in session.Population.Animals)
        {
            Vector2 offset = animal.Position.Xz() - eye;
            float distance = offset.Length();
            if (distance is > 1f and < 45f && Vector2.Dot(offset / distance, look) > 0.8f && animals < 3)
            {
                animals++;
                score += 15;
            }
        }

        if (animals > 0)
        {
            tags.Add(new L("Satwa x{0}", "Wildlife x{0}").With(animals));
        }

        long payment = score >= 40 ? score * 400L : 0L;
        session.Progress.PhotosTaken++;
        session.Progress.BestPhotoScore = Math.Max(session.Progress.BestPhotoScore, score);
        if (payment > 0)
        {
            session.Reward(payment, Career.Kuliner, score / 25);
        }

        PhotoResult result = new(score, tags, payment);
        session.Emit(new GameEvent(GameEventKind.Photo, new L("Foto: {0} poin", "Photo: {0} pts").With(score), cameraPosition, score));
        return result;
    }
}

public enum BusinessTier
{
    Gerobak,
    Warung,
    Kedai,
    Franchise,
}

/// <summary>
/// Bandrek King: sell hot ginger drink from the cart, restock, and grow from a cart to a
/// national franchise. Higher tiers earn while you are away; collect the takings at the stall.
/// </summary>
public sealed class BandrekBusiness
{
    public const long CupCost = 3_000;
    public const int RestockCups = 20;

    private static readonly L[] CustomerLines =
    [
        new("\"Bandrekna hiji, Kang. Tiris euy!\"", "\"One bandrek, please. It's freezing!\""),
        new("\"Jahenya nendang pisan!\"", "\"That ginger really kicks!\""),
        new("\"Bungkus dua, buat mertua.\"", "\"Two to go, for the in-laws.\""),
        new("\"Hujan-hujan gini mah bandrek juara.\"", "\"Nothing beats bandrek in this rain.\""),
    ];

    private float _sellTimer = 4f;
    private float _incomeHours;

    public BusinessTier Tier { get; set; }

    /// <summary>Cups ready to sell from the cart.</summary>
    public int Stock { get; set; } = 10;

    public long CupsSold { get; set; }

    /// <summary>Takings of the stall waiting to be collected.</summary>
    public long Pending { get; set; }

    public long CupPrice => 8_000 + ((int)Tier * 2_000);

    public long? UpgradeCost => Tier switch
    {
        BusinessTier.Gerobak => 750_000,
        BusinessTier.Warung => 4_000_000,
        BusinessTier.Kedai => 20_000_000,
        _ => null,
    };

    public static L TierName(BusinessTier tier) => tier switch
    {
        BusinessTier.Gerobak => new("Gerobak Keliling", "Push Cart"),
        BusinessTier.Warung => new("Warung Bandrek", "Bandrek Stall"),
        BusinessTier.Kedai => new("Kedai Bandrek Kekinian", "Trendy Bandrek Café"),
        _ => new("Franchise Nasional", "National Franchise"),
    };

    /// <summary>True while the player is parked with the cart and customers are being served.</summary>
    public bool Selling { get; private set; }

    public bool Restock(GameSession session)
    {
        if (!session.Progress.TrySpend(CupCost * RestockCups))
        {
            session.Toast(new("Duitna kurang, Kang.", "Not enough money."));
            return false;
        }

        Stock += RestockCups;
        session.Toast(new L("Stok bandrek +{0} gelas.", "Bandrek stock +{0} cups.").With(RestockCups));
        return true;
    }

    public bool Upgrade(GameSession session)
    {
        if (UpgradeCost is not { } cost)
        {
            return false;
        }

        if (!session.Progress.TrySpend(cost))
        {
            session.Toast(new("Modal belum cukup buat naik kelas.", "Not enough capital to level up yet."));
            return false;
        }

        Tier++;
        session.Progress.AddReputation(Career.Bandrek, 25);
        session.Toast(new L("Usaha naik kelas: {0}!", "Business upgraded: {0}!").With(TierName(Tier).Text));
        return true;
    }

    public void Collect(GameSession session)
    {
        if (Pending <= 0)
        {
            session.Toast(new("Belum ada setoran.", "No takings yet."));
            return;
        }

        long amount = Pending;
        Pending = 0;
        session.Reward(amount, Career.Bandrek, (int)Math.Min(10, amount / 100_000));
        session.Toast(new L("Setoran diambil: {0}", "Takings collected: {0}").With(Loc.Rupiah(amount)));
    }

    public void Update(GameSession session, float dt)
    {
        // Stalls and franchises earn by the game hour.
        if (Tier > BusinessTier.Gerobak)
        {
            _incomeHours += dt * session.Clock.MinutesPerSecond / 60f;
            if (_incomeHours >= 1f)
            {
                _incomeHours -= 1f;
                long hourly = Tier switch { BusinessTier.Warung => 35_000, BusinessTier.Kedai => 160_000, _ => 900_000 };
                Pending += (long)(hourly * (1f + (0.6f * session.Weather.Rain)));
            }
        }

        Player player = session.Player;
        Selling = player.Vehicle?.Spec.Kind == VehicleKind.GerobakBandrek && MathF.Abs(player.Vehicle.Speed) < 0.3f && Stock > 0;
        if (!Selling)
        {
            return;
        }

        // Customers come faster in busy places, in the rain and in the cool of the night.
        float crowd = session.Map.LandUseAt(player.Ground) switch
        {
            LandUse.Pasar => 1.6f,
            LandUse.Kota or LandUse.Kampus => 1.2f,
            LandUse.Kampung or LandUse.Gang or LandUse.Villa => 1f,
            LandUse.Modern or LandUse.Taman => 0.8f,
            _ => 0.3f,
        };
        float demand = crowd * (1f + (1.5f * session.Weather.Rain)) * (session.Clock.IsNight ? 1.3f : 1f);
        _sellTimer -= dt * demand;
        if (_sellTimer > 0f)
        {
            return;
        }

        _sellTimer = session.Range(3.5f, 7f);
        Stock--;
        CupsSold++;
        long price = (long)(CupPrice * (1f + player.Profile.TradeBonus));
        session.Reward(price, Career.Bandrek, CupsSold % 3 == 0 ? 1 : 0);
        if (CupsSold % 4 == 1)
        {
            session.Toast(CustomerLines[session.Random(0, CustomerLines.Length)]);
        }

        if (Stock == 0)
        {
            session.Toast(new("Bandrek habis! Belanja stok dulu di warung.", "Sold out! Restock at the stall."));
        }
    }
}
