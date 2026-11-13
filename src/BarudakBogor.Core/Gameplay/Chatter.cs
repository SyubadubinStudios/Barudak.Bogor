using BarudakBogor.Entities;
using BarudakBogor.Localization;
using BarudakBogor.Numerics;
using BarudakBogor.Sim;

namespace BarudakBogor.Gameplay;

/// <summary>
/// What passers-by say. A line is picked from who is speaking, the weather and the hour, so
/// the same street sounds different in a morning drizzle and on a dry night.
/// </summary>
public static class Chatter
{
    private static readonly L[] Rain =
    [
        new("\"Hujan deui, hujan deui. Namina ogé Bogor.\"", "\"Rain again, rain again. Well, it is Bogor.\""),
        new("\"Jemuran! Jemuran can diangkat!\"", "\"The laundry! Nobody took the laundry in!\""),
        new("\"Ngiuhan heula atuh, Jang.\"", "\"Come and shelter a while, lad.\""),
        new("\"Payung aing dipaké saha ieu téh?\"", "\"Now who has gone off with my umbrella?\""),
    ];

    private static readonly L[] UnderUmbrella =
    [
        new("\"Sayang, payungna ngan mahi keur saurang.\"", "\"Sorry, this umbrella only fits one.\""),
        new("\"Tiis kieu mah ngeunahna bandrek.\"", "\"Cold like this calls for a bandrek.\""),
    ];

    private static readonly L[] Morning =
    [
        new("\"Wilujeng enjing! Tos sarapan?\"", "\"Good morning! Had breakfast yet?\""),
        new("\"Bubur ayam di parapatan can béak, buru.\"", "\"The chicken porridge at the corner isn't sold out yet. Hurry.\""),
        new("\"Isuk-isuk geus macét waé.\"", "\"Barely morning and it is jammed already.\""),
    ];

    private static readonly L[] Night =
    [
        new("\"Wengi-wengi kénéh di jalan? Awas masuk angin.\"", "\"Still out at this hour? You'll catch a chill.\""),
        new("\"Ronda heula, Jang. Kopi aya di pos.\"", "\"Night watch, lad. There's coffee at the post.\""),
        new("\"Sepi nya. Angkot gé geus jarang.\"", "\"Quiet, isn't it. Even the angkots are scarce.\""),
    ];

    private static readonly L[] Anyone =
    [
        new("\"Punten, ka Pasar Anyar ka palih mana?\"", "\"Excuse me, which way to Pasar Anyar?\""),
        new("\"Angkot 03 naha lila pisan ngetemna.\"", "\"Why does the 03 angkot wait around so long?\""),
        new("\"Ceuk béja aya tas misterius di Tugu Kujang.\"", "\"They say there was a mysterious bag at Tugu Kujang.\""),
        new("\"Macét di Jalan Baru mah geus biasa.\"", "\"A jam on Jalan Baru is just a normal day.\""),
        new("\"Talas Bogor, oleh-oleh, mangga.\"", "\"Bogor taro, souvenirs, here you go.\""),
    ];

    private static L[] ByKind(NpcKind kind) => kind switch
    {
        NpcKind.Pedagang =>
        [
            new("\"Cilok, cilok! Saréwu tilu!\"", "\"Cilok, cilok! Three for a thousand!\""),
            new("\"Asinan Bogor, seger kénéh!\"", "\"Bogor pickles, still fresh!\""),
            new("\"Mangga, dicobian heula. Teu meser gé teu nanaon.\"", "\"Go on, have a taste. No need to buy.\""),
        ],
        NpcKind.Ojol =>
        [
            new("\"Ojek, A? Deukeut jauh sami waé.\"", "\"Ojek, mate? Near or far, same to me.\""),
            new("\"Orderan sepi, hujan mah karék ramé.\"", "\"Orders are slow. It only gets busy when it rains.\""),
        ],
        NpcKind.Sopir =>
        [
            new("\"Setoran can nutup yeuh.\"", "\"Haven't made the day's quota yet.\""),
            new("\"Baranangsiang! Baranangsiang! Hiji deui, hiji deui!\"", "\"Baranangsiang! Baranangsiang! Room for one more!\""),
        ],
        NpcKind.Mahasiswa =>
        [
            new("\"Praktikum deui, laporan deui.\"", "\"Another lab session, another report.\""),
            new("\"Aya nu terang wifi kampus naha pareum?\"", "\"Anyone know why the campus wifi is down?\""),
        ],
        NpcKind.AnakSd or NpcKind.AnakSekolah =>
        [
            new("\"Om, om! Angkotna tiasa ngebut?\"", "\"Mister, mister! Can your angkot go fast?\""),
            new("\"PR matématika can dipigawé...\"", "\"Haven't done my maths homework...\""),
        ],
        NpcKind.Pegawai =>
        [
            new("\"Rapat deui, rapat deui.\"", "\"Meeting after meeting.\""),
            new("\"Telat lima menit, dipotong uang makan.\"", "\"Five minutes late and they dock the lunch money.\""),
        ],
        NpcKind.Ibu =>
        [
            new("\"Harga cabé naék deui, Jang!\"", "\"The price of chillies is up again, lad!\""),
            new("\"Arisan engké soré, ulah poho.\"", "\"The arisan is this afternoon, don't forget.\""),
        ],
        _ =>
        [
            new("\"Kumaha damang? Lila teu katingali.\"", "\"How are you keeping? Haven't seen you in a while.\""),
            new("\"Persib meunang deui tadi peuting!\"", "\"Persib won again last night!\""),
        ],
    };

    /// <summary>Chooses a line for a passer-by.</summary>
    public static L Line(Pedestrian speaker, GameClock clock, WeatherSystem weather, ref Rng rng)
    {
        // Mostly something about who they are; otherwise about the moment.
        L[] pool = rng.Chance(0.45f) ? ByKind(speaker.Kind)
            : speaker.Umbrella && rng.Chance(0.5f) ? UnderUmbrella
            : weather.Rain > 0.3f ? Rain
            : clock.Hours is >= 4.5f and < 9f ? Morning
            : clock.IsNight ? Night
            : Anyone;
        return pool[rng.Range(0, pool.Length)];
    }
}
