using System.Numerics;

namespace BarudakBogor.World;

/// <summary>The eight headline areas of the design document, plus the countryside between them.</summary>
public enum DistrictId
{
    Rural,
    PusatKota,
    KebunRaya,
    PasarAnyar,
    Dramaga,
    Sentul,
    Empang,
    Puncak,
    GangSelot,
    SuryaKencana,
}

/// <summary>What the land is used for at a point; drives buildings, vegetation and population.</summary>
public enum LandUse
{
    Sawah,
    Hutan,
    KebunTeh,
    Taman,
    Kota,
    Pasar,
    Kampung,
    Gang,
    Kampus,
    Modern,
    Villa,
}

/// <summary>A named circular area of the map.</summary>
public sealed record District(DistrictId Id, string NameKey, Vector2 Center, float Radius, LandUse Use);

/// <summary>A still water body: the terrain is dug into a bowl and a flat surface is drawn at <see cref="Level"/>.</summary>
public sealed record Pond(string NameKey, Vector2 Center, float Radius, float Depth)
{
    /// <summary>Water surface height, filled in when the terrain is created.</summary>
    public float Level { get; internal set; }
}

/// <summary>Named places missions and the map screen refer to.</summary>
public enum PoiId
{
    TuguKujang,
    GerbangKebunRaya,
    IstanaRusa,
    PasarAnyar,
    GangSelot,
    Empang,
    RumahUjang,
    TerminalDramaga,
    KampusIpb,
    SituGede,
    JalanBaru,
    SirkuitSentul,
    SekolahBinaInsani,
    LapanganStm,
    Ciawi,
    PuncakPass,
    VillaMisterius,
    KebunTeh,
    WarungBandrek,
    LapanganBadminton,
    Karaoke,
    StasiunBogor,
    IstanaBogor,
    MuseumZoologi,
    MuseumTanah,
    MuseumPeta,
    BalaiKota,
    Katedral,
    MasjidRaya,
    BotaniSquare,
    BogorTradeMall,
    YogyaJunction,
    PlazaJambuDua,
    Ramayana,
    Ekalokasari,
    LotteMart,
    BogorGreatMall,
    PasarBogor,
    SuryaKencana,
    AirMancur,
    LapanganSempur,
    GorPajajaran,
    AlunAlun,
    TamanKencana,
    TerminalBaranangsiang,
    BendungKatulampa,
}

/// <summary>A named place. <see cref="Position"/> is beside the road, <see cref="RoadPoint"/> on it.</summary>
public sealed record Poi(PoiId Id, string NameKey, Vector2 Position, Vector2 RoadPoint, float Yaw);
