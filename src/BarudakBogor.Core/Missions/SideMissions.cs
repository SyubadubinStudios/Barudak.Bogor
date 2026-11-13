using System.Numerics;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Localization;
using BarudakBogor.Numerics;
using BarudakBogor.World;

namespace BarudakBogor.Missions;

/// <summary>Paket Nyasar: three misdelivered parcels, one motorbike, a maze of alleys and a clock.</summary>
public sealed class PaketMission : Mission
{
    public override string Id => "paket";

    public override bool IsSide => true;

    public override string? GiverModel => "npc_ojol";

    public override L Title => new("Paket Nyasar", "Lost Parcels");

    public override L Summary => new(
        "Tiga paket salah alamat di Gang Selot. Antar naik motor sebelum pemiliknya komplain bintang satu.",
        "Three parcels went to the wrong address in Selot Alley. Deliver them by motorbike before the one-star reviews arrive.");

    public override PoiId StartPlace => PoiId.GangSelot;

    public override long RewardMoney => 90_000;

    public override Career RewardCareer => Career.Kuliner;

    public override int RewardReputation => 12;

    protected override L TimeoutReason => new("Bintang satu: \"Paket nepi, tapi geus lebaran.\"", "One star: \"Parcel arrived, eventually.\"");

    protected override void Script()
    {
        L courier = new("Kang Kurir", "The Courier");
        L customer = new("Pelanggan", "Customer");
        Vector2 a = RoadNear(PoiId.GangSelot, new Vector2(110f, -70f));
        Vector2 b = RoadNear(PoiId.GangSelot, new Vector2(-130f, 60f));
        Vector2 c = RoadNear(PoiId.PasarAnyar, new Vector2(80f, 120f));
        Vehicle? bike = null;
        bool Riding() => S.Player.Vehicle is { Spec.Kind: VehicleKind.Motor } v && MathF.Abs(v.Speed) < 2f;

        Talk(courier,
            new("Aduh, Kang! Tilu pakét salah alamat, sedengkeun abdi kedah ka dokter gigi. Pangnganteurkeun nya, nganggo motor abdi.",
                "Oh no! Three parcels went to the wrong address and I have a dentist's appointment. Deliver them for me? Take my bike."));
        Do(() =>
        {
            bike = S.SpawnVehicle(VehicleKind.Motor, S.Map.ResolveCircle(StartSpot + new Vector2(2.5f, 1.5f), 0.7f, out _), 0f);
            OnEnd(() => S.RemoveVehicle(bike));
        });
        Until(new("Naik motor kurir", "Get on the courier's motorbike"), () => S.Player.Vehicle == bike, () => bike?.Ground);
        Do(() => TimeLeft = 210f);
        Reach(new("Paket 1: rice cooker untuk Bu Tati", "Parcel 1: a rice cooker for Bu Tati"), () => a, 8f, Riding);
        Talk(customer, new("Rice cooker abdi! Parantos tilu dinten masak sangu dina panci.", "My rice cooker! Three days I've been boiling rice in a pot."));
        Reach(new("Paket 2: sepatu futsal untuk Asep", "Parcel 2: futsal shoes for Asep"), () => b, 8f, Riding);
        Talk(customer, new("Sapatuna! Pas pisan, engké soré tanding ngalawan RT 03.", "The shoes! Just in time, we play block 03 this afternoon."));
        Reach(new("Paket 3: 40 bungkus kerupuk ke Pasar Anyar", "Parcel 3: 40 packs of crackers to Pasar Anyar"), () => c, 8f, Riding);
        Do(() => TimeLeft = null);
        Talk(customer, new("Kurupukna remuk tilu. Teu nanaon, dijual jadi \"kurupuk mini\". Bintang lima!",
            "Three packs got crushed. Never mind, I'll sell them as \"mini crackers\". Five stars!"));
    }
}

/// <summary>Rusa Kabur: one of the palace deer has gone for a walk.</summary>
public sealed class RusaMission : Mission
{
    public override string Id => "rusa";

    public override bool IsSide => true;

    public override string? GiverModel => "npc_anak_sd";

    public override L Title => new("Rusa Kabur", "The Runaway Deer");

    public override L Summary => new(
        "Seekor rusa Istana jalan-jalan ke luar pagar. Giring pulang pelan-pelan, pakai wortel, jalan kaki.",
        "One of the palace deer has wandered out of the fence. Coax it home gently, with carrots, on foot.");

    public override PoiId StartPlace => PoiId.IstanaRusa;

    public override long RewardMoney => 60_000;

    public override int RewardReputation => 12;

    protected override void Script()
    {
        L kid = new("Dede", "Dede");
        L hero = new(S.Player.Profile.Name, S.Player.Profile.Name);
        L deer = new("Rusa", "The Deer");
        Poi palace = S.Map[PoiId.IstanaRusa];
        Vector2 away = palace.RoadPoint - palace.Position;
        away = away.LengthSquared() > 0.01f ? Vector2.Normalize(away) : Vector2.UnitY;
        Vector2 across = MathUtil.Left(away);
        // The deer keeps a few steps ahead: each spot is where it has trotted off to.
        Vector2 first = S.Map.ResolveCircle(palace.RoadPoint + (away * 26f) + (across * 30f), 0.6f, out _);
        Vector2 second = S.Map.ResolveCircle(palace.RoadPoint + (away * 12f) - (across * 44f), 0.6f, out _);
        Vector2 fence = S.Map.ResolveCircle(palace.RoadPoint - (away * 4f), 0.6f, out _);
        bool OnFoot() => S.Player.Vehicle is null;

        Talk(kid,
            new("Om! Rusana kabur hiji, kaluar tina pager! Abdi mah teu wantun, tandukna ageung.",
                "Mister! One of the deer got out through the fence! I daren't go near it, the antlers are huge."),
            new("Ieu wortel. Ulah lumpat nya, engké rusana reuwas.", "Here are some carrots. Don't run, you'll spook it."));
        Reach(new("Dekati rusa pelan-pelan (jalan kaki, jangan lari)", "Approach the deer slowly (on foot, no running)"), () => first, 4f, () => OnFoot() && S.Player.Speed < 3.2f);
        Talk(deer, new("(Rusa ngadahar wortel, tuluy leumpang deui ka peuntas.)", "(The deer eats the carrot, then ambles off across the road.)"));
        Reach(new("Ikuti rusa ke seberang", "Follow the deer across"), () => second, 4f, () => OnFoot() && S.Player.Speed < 3.2f);
        Talk(hero, new("Hayu, Sa. Di jero pager aya jukut nu leuwih héjo. Sumpah.", "Come on. The grass is greener inside the fence. Promise."));
        Reach(new("Giring rusa kembali ke pagar Istana", "Lead the deer back to the palace fence"), () => fence, 5f, OnFoot);
        Talk(kid, new("Asup deui! Hatur nuhun, Om. Wortel sésana kanggo Om wé.", "It's back in! Thank you, mister. Keep the rest of the carrots."));
    }
}

/// <summary>Delman Istimewa: a gentle carriage tour of the Gardens. Gentle is the whole job.</summary>
public sealed class DelmanMission : Mission
{
    private const float GentleSpeed = 5f;

    private float _spilled;

    public override string Id => "delman";

    public override bool IsSide => true;

    public override string? GiverModel => "npc_sopir";

    public override L Title => new("Delman Istimewa", "A Very Special Horse Cart");

    public override L Summary => new(
        "Ajak turis keliling Kebun Raya naik delman. Pelan-pelan: es krimnya jangan sampai jatuh.",
        "Take tourists round the Gardens by horse cart. Slowly: their ice creams must survive the trip.");

    public override PoiId StartPlace => PoiId.GerbangKebunRaya;

    public override long RewardMoney => 80_000;

    public override Career RewardCareer => Career.Angkot;

    public override int RewardReputation => 12;

    protected override void Script()
    {
        L coachman = new("Mang Kusir", "The Coachman");
        L tourist = new("Turis", "Tourist");
        Vector2 a = RoadNear(PoiId.GerbangKebunRaya, new Vector2(220f, 60f));
        Vector2 b = RoadNear(PoiId.TuguKujang, new Vector2(60f, -140f));
        Vector2 gate = S.Map[PoiId.GerbangKebunRaya].RoadPoint;
        Vehicle? cart = null;
        _spilled = 0f;
        bool Driving() => cart is not null && S.Player.Vehicle == cart;

        Talk(coachman,
            new("Cangkéng abdi nyeri. Pangnarikkeun delman sakali kuriling nya. Turisna nuju nyepeng és krim, ulah gujrug!",
                "My back's gone. Take the cart round once for me. The tourists are holding ice creams, so no jolting!"));
        Do(() =>
        {
            cart = S.Vehicles.Where(v => v.Spec.Kind == VehicleKind.Delman && !v.Occupied)
                .OrderBy(v => Vector2.DistanceSquared(v.Ground, S.Player.Ground)).FirstOrDefault();
            if (cart is null || Vector2.Distance(cart.Ground, S.Player.Ground) > 80f)
            {
                Vehicle spawned = S.SpawnVehicleAt(VehicleKind.Delman, PoiId.GerbangKebunRaya, 12f);
                cart = spawned;
                OnEnd(() => S.RemoveVehicle(spawned));
            }
        });
        Until(new("Naik ke delman", "Climb onto the horse cart"), Driving, () => cart?.Ground);
        Reach(new("Keliling pelan-pelan: tikungan pertama", "A gentle round: the first bend"), () => a, 9f, Driving);
        Talk(tourist, new("Wah, tangkalna ageung-ageung! Poto heula, Mang, poto heula!", "Wow, such big trees! Stop for a photo, stop for a photo!"));
        Reach(new("Lewat Tugu Kujang", "Go past Tugu Kujang"), () => b, 9f, Driving);
        Reach(new("Kembali ke gerbang Kebun Raya", "Back to the Gardens gate"), () => gate, 10f, Driving);
        Talk(tourist, new("Sae pisan! És krimna masih kénéh utuh. Ieu tip kanggo kudana.", "Lovely! The ice cream survived. Here's a tip for the horse."));
    }

    protected override void Tick(float dt)
    {
        if (S.Player.Vehicle is not { Spec.Kind: VehicleKind.Delman } cart)
        {
            return;
        }

        // Trotting too fast shakes the passengers; easing off lets them settle.
        _spilled = MathF.Abs(cart.Speed) > GentleSpeed ? _spilled + dt : MathF.Max(0f, _spilled - (dt * 0.5f));
        if (_spilled > 4f)
        {
            S.Missions.Fail(new("És krimna ragrag kana sapatu turis. Pelan-pelan, Mang!", "The ice cream landed on a tourist's shoe. Gently does it!"));
        }
    }
}
