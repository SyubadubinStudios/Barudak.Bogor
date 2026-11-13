using System.Numerics;
using BarudakBogor.Entities;
using BarudakBogor.Gameplay;
using BarudakBogor.Localization;
using BarudakBogor.Numerics;
using BarudakBogor.Sim;
using BarudakBogor.World;

namespace BarudakBogor.Missions;

/// <summary>
/// Isi Flashdisk: what was on the drive from the mysterious bag. The password is a callback to
/// the cilok mission, the contents turn out to be a plan to pave the Botanical Gardens, and the
/// people who lost the bag notice someone has been reading it.
/// </summary>
public sealed class FlashdiskMission : Mission
{
    private readonly List<Pursuer> _pursuers = [];
    private bool _guessed;

    public override string Id => "flashdisk";

    public override string? GiverModel => "npc_mahasiswa";

    public override L Title => new("Isi Flashdisk", "What's on the Flash Drive");

    public override L Summary => new(
        "Flashdisk \"RAHASIA - ULAH DIBUKA\" akhirnya dibuka di warnet kampus. Isinya bikin satu Bogor pusing.",
        "The \"SECRET - DO NOT OPEN\" flash drive finally gets opened at the campus internet café. What's on it concerns all of Bogor.");

    public override PoiId StartPlace => PoiId.KampusIpb;

    public override long RewardMoney => 300_000;

    public override int RewardReputation => 25;

    public override string? Requires => "cilok";

    protected override void Script()
    {
        L opik = new("Opik Warnet", "Opik from the Café");
        L hero = new(S.Player.Profile.Name, S.Player.Profile.Name);
        L screen = new("Layar Laptop", "The Laptop Screen");
        Poi gate = S.Map[PoiId.GerbangKebunRaya];
        Vector2 inward = gate.Position - gate.RoadPoint;
        inward = inward.LengthSquared() > 0.01f ? Vector2.Normalize(inward) : Vector2.UnitY;
        Vector2 along = MathUtil.Left(inward);
        Vector2 stake1 = S.Map.ResolveCircle(gate.RoadPoint + (inward * 6.5f) + (along * 9f), 0.6f, out _);
        Vector2 stake2 = S.Map.ResolveCircle(gate.RoadPoint + (inward * 6.5f) - (along * 9f), 0.6f, out _);
        Vector2 home = S.Map[PoiId.RumahUjang].RoadPoint;
        bool OnFoot() => S.Player.Vehicle is null;
        _guessed = false;

        Talk(opik,
            new("Flashdisk ti tas éta? Sok colokkeun. ...Euleuh, dikonci. Aya pituduhna: \"jajanan nu gerobakna kungsi leungit\".",
                "The drive from that bag? Plug it in. ...Huh, it's locked. There's a hint: \"the snack whose cart once went missing\"."));
        Ask(hero, new("Kata sandinya...", "The password is..."),
        [
            new("bandrek", "bandrek"),
            new("cilok", "cilok"),
            new("tahubulat", "tahubulat"),
        ], choice => _guessed = choice == 1);
        TalkWhen(() => !_guessed, opik,
            new("Salah. Untung abdi apal gosip pasar: gerobak cilok Mang Udin. \"cilok\"... tah, kabuka!",
                "Wrong. Luckily I keep up with market gossip: Mang Udin's cilok cart. \"cilok\"... there, it's open!"));
        TalkWhen(() => _guessed, opik, new("Kabuka! Pinter ogé anjeun téh.", "It's open! You're sharper than you look."));
        Talk(screen,
            new("PROPOSAL PT BETON JAYA ABADI: \"Bogor Mega Parkir\". Kebun Raya diratakeun, diganti parkiran dalapan tingkat.",
                "PROPOSAL, PT BETON JAYA ABADI: \"Bogor Mega Parking\". The Botanical Gardens levelled and replaced by an eight-storey car park."),
            new("Lampiran: \"dana pelicin\" pikeun ngalancarkeun idin. Jumlahna sarua jeung eusi tas.",
                "Attachment: \"facilitation funds\" to smooth the permits. The amount matches what was in the bag."));
        Talk(hero,
            new("Jadi duit éta téh duit sogokan? Jeung Kebun Raya rék jadi parkiran?! Ieu kudu dibuktikeun heula.",
                "So that money is bribe money? And the Gardens are to become a car park?! I need to see proof of this."));
        Reach(new("Pergi ke Gerbang Kebun Raya", "Go to the Botanical Gardens gate"), () => gate.RoadPoint, 14f);
        Reach(new("Periksa patok ukur di kiri gerbang (jalan kaki)", "Check the survey stake left of the gate (on foot)"), () => stake1, 4f, OnFoot);
        Talk(hero, new("Patok beureum. Tulisanna \"BJA - TITIK PARKIR B2\". Rusa gé pasti teu satuju.",
            "A red stake. It says \"BJA - PARKING BAY B2\". Even the deer would object."));
        Reach(new("Periksa patok di kanan gerbang", "Check the stake right of the gate"), () => stake2, 4f, OnFoot);
        Talk(hero,
            new("Hiji deui. Bener, lain hoax. ...Éh, éta mobil hideung nu baréto. Maranéhna nempo kuring keur moto patok!",
                "Another one. It's real, not a hoax. ...Uh-oh, the black car from before. They saw me photographing the stakes!"));
        Do(() =>
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 spot = gate.RoadPoint - (along * (30f + (i * 14f)));
                spot = S.Map.Roads.Nearest(spot, 80f, e => e.Kind != RoadKind.Toll)?.Point ?? spot;
                Vehicle car = S.SpawnVehicle(VehicleKind.MobilKeluarga, spot, MathUtil.YawOf(along));
                car.Occupied = true;
                car.LightsOn = true;
                _pursuers.Add(new Pursuer(S, car, interceptor: i == 1));
                OnEnd(() => S.RemoveVehicle(car));
            }

            OnEnd(_pursuers.Clear);
        });
        Reach(new("Bawa buktinya pulang ke kontrakan! Jangan terkepung", "Get the evidence home! Don't get boxed in"), () => home, 14f);
        Do(() =>
        {
            foreach (Pursuer pursuer in _pursuers)
            {
                S.RemoveVehicle(pursuer.Vehicle);
            }

            _pursuers.Clear();
        });
        Talk(hero,
            new("Lolos deui. Tapi nyumput mah moal ngaréngsékeun nanaon. Warga kudu nyaho. Urang kumpulkeun di Tugu Kujang.",
                "Got away again. But hiding solves nothing. People have to know. Time to gather everyone at Tugu Kujang."));
    }

    protected override void Tick(float dt)
    {
        foreach (Pursuer pursuer in _pursuers)
        {
            pursuer.Update(dt);
            if (pursuer.Cornered > 3f)
            {
                S.Missions.Fail(new("Terkepung! Fotonya dihapus. Coba lagi dari kampus.", "Boxed in! They deleted the photos. Try again from the campus."));
                return;
            }
        }
    }
}

/// <summary>
/// Rapat Warga: the finale. Gather the people the earlier missions helped, then face the
/// developer at Tugu Kujang. However it is settled, nobody gets hurt and the Gardens stay.
/// </summary>
public sealed class MufakatMission : Mission
{
    private int _ending = -1;

    public override string Id => "mufakat";

    public override string? GiverModel => "npc_pegawai";

    public override L Title => new("Rapat Warga", "The Town Meeting");

    public override L Summary => new(
        "Kumpulkan warga, lalu hadapi Pak Beton di Tugu Kujang. Kebun Raya bukan parkiran.",
        "Rally the townsfolk, then face Mr Beton at Tugu Kujang. The Gardens are not a car park.");

    public override PoiId StartPlace => PoiId.TuguKujang;

    public override long RewardMoney => 1_000_000;

    public override int RewardReputation => 40;

    public override string? Requires => "flashdisk";

    protected override void Script()
    {
        L rt = new("Pak RT", "The Neighbourhood Head");
        L hero = new(S.Player.Profile.Name, S.Player.Profile.Name);
        L udin = new("Mang Udin", "Mang Udin");
        L dadang = new("Juragan Haji Dadang", "Boss Haji Dadang");
        L aceng = new("Abah Aceng", "Abah Aceng");
        L beton = new("Pak Beton", "Mr Beton");
        L crowd = new("Warga Bogor", "The People of Bogor");
        Vector2 market = S.Map[PoiId.PasarAnyar].RoadPoint;
        Vector2 terminal = S.Map[PoiId.TerminalDramaga].RoadPoint;
        Vector2 stall = S.Map[PoiId.WarungBandrek].RoadPoint;
        Vector2 monument = S.Map[PoiId.TuguKujang].RoadPoint;
        _ending = -1;

        Talk(rt,
            new("Kebun Raya rék jadi parkiran? Teu bisa kitu! Tapi Pak Beton téh loba duitna, urang kudu loba baturna.",
                "The Gardens as a car park? Not on my watch! But Mr Beton has money, so we need numbers."),
            new("Ajak Mang Udin, Haji Dadang, jeung Abah Aceng. Rapatna soré ieu, di dieu.",
                "Fetch Mang Udin, Haji Dadang and Abah Aceng. The meeting is this afternoon, right here."));
        Do(() =>
        {
            S.Clock.Set(S.Clock.Day + (S.Clock.Hours > 14f ? 1 : 0), 14f);
            S.Weather.Set(WeatherKind.Berawan, 900f);
            S.Weather.Locked = true;
            OnEnd(() => S.Weather.Locked = false);
        });
        Reach(new("Ajak Mang Udin di Pasar Anyar", "Fetch Mang Udin at Pasar Anyar"), () => market, 10f);
        Talk(udin, new("Kebun Raya digusur? Terus abdi dagang cilok ka saha, ka mobil parkir? Hayu, abdi milu!",
            "The Gardens bulldozed? Then who do I sell cilok to, parked cars? I'm in!"));
        Reach(new("Ajak Haji Dadang di Terminal Dramaga", "Fetch Haji Dadang at Dramaga terminal"), () => terminal, 10f);
        Talk(dadang, new("Parkiran dalapan tingkat? Angkot abdi rék narik ka mana? Sakabéh sopir ku abdi diajak!",
            "An eight-storey car park? Where would my angkots run? I'll bring every driver!"));
        Reach(new("Ajak Abah Aceng di Warung Bandrek", "Fetch Abah Aceng at the bandrek stall"), () => stall, 10f);
        Talk(aceng, new("Abah mawa bandrek sapanci gedé. Rapat mah kudu haneut, ulah panas.",
            "I'll bring a great pot of bandrek. A meeting should be warm, not heated."));
        Reach(new("Kembali ke Tugu Kujang", "Return to Tugu Kujang"), () => monument, 12f);
        Talk(beton,
            new("Jadi anjeun nu manggihan tas kuring. Balikkeun, urang anggap teu aya nanaon. Parkiran mah kamajuan!",
                "So you are the one who found my bag. Hand it back and we forget all this. A car park is progress!"));
        Ask(hero, new("Sadaya warga ningali. Kumaha ayeuna?", "The whole town is watching. What now?"),
        [
            new("Kembalikan uangnya di depan semua orang", "Return the money in front of everyone"),
            new("Tayangkan isi flashdisk di baliho", "Show the flash drive on the billboard"),
            new("Tantang Pak Beton main badminton", "Challenge Mr Beton to badminton"),
        ], choice => _ending = choice);
        TalkWhen(() => _ending == 0, hero,
            new("Ieu duit anjeun, Pak, saperak gé teu kurang. Tapi sadayana ayeuna terang éta duit keur naon.",
                "Here is your money, sir, every last rupiah. But now everybody knows what it was for."));
        TalkWhen(() => _ending == 1, crowd,
            new("\"BOGOR MEGA PARKIR\" mucunghul dina baliho, kalebet lampiran dana pelicin. Sadayana ngagorowok: HUUU!",
                "\"BOGOR MEGA PARKING\" appears on the billboard, facilitation funds and all. The crowd as one: BOOO!"));
        TalkWhen(() => _ending == 2, beton,
            new("Badminton? Kuring juara kantor! ...Dua puluh hiji-enol. Dipaéhan ku smash bapa-bapa sarungan.",
                "Badminton? I'm the office champion! ...Twenty-one nil. Undone by the smash of a dad in a sarong."));
        Talk(beton,
            new("Enya, enya, éléh. Proyékna dibatalkeun. Tapi... bandrekna ngeunah. Tiasa nambih?",
                "All right, all right, I give up. The project is cancelled. But... this bandrek is good. May I have another?"));
        Talk(rt,
            new("Duitna dipaké ngoméan terminal jeung melak tangkal. Hujanna asli, petualanganana teu aya tungtungna!",
                "The money goes to fixing the terminal and planting trees. The rain is real, and the adventure never ends!"));
        Do(() => S.Progress.AddReputation(Career.Warga, 20));
    }
}
