namespace BarudakBogor.Localization;

public enum Language
{
    Indonesia,
    English,
}

/// <summary>
/// A piece of text in both languages of the game. Keeping the two side by side at the place
/// of use means a line can never exist in only one language.
/// </summary>
public readonly record struct L(string Id, string En)
{
    public static readonly L Empty = new(string.Empty, string.Empty);

    public bool IsEmpty => string.IsNullOrEmpty(Id);

    public string Text => Loc.Language == Language.Indonesia ? Id : En;

    /// <summary>Formats both languages with the same arguments.</summary>
    public L With(params object[] args) => new(string.Format(Id, args), string.Format(En, args));

    public override string ToString() => Text;
}

/// <summary>Current language and the names of things that are referred to by key.</summary>
public static class Loc
{
    public const string Credit = "Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios";

    public static readonly L CreditLine = new(Credit, "Made by Ariana Mischa Fadhila of Syubadubin Studios");

    public static readonly L Tagline = new(
        "Hujanna Asli, Petualanganna Teu Aya Tungtungna.",
        "The rain is real, the adventure never ends.");

    private static readonly Dictionary<string, L> Names = new()
    {
        ["district.pusatkota"] = new("Pusat Kota Bogor", "Bogor City Centre"),
        ["district.kebunraya"] = new("Kebun Raya Bogor", "Bogor Botanical Gardens"),
        ["district.pasaranyar"] = new("Pasar Anyar", "Pasar Anyar Market"),
        ["district.dramaga"] = new("Dramaga", "Dramaga"),
        ["district.sentul"] = new("Sentul", "Sentul"),
        ["district.empang"] = new("Empang", "Empang"),
        ["district.puncak"] = new("Puncak", "Puncak"),
        ["district.gangselot"] = new("Gang Selot", "Selot Alley"),
        ["district.suryakencana"] = new("Suryakencana (Pecinan)", "Suryakencana (Chinatown)"),
        ["district.suryakencana"] = new("Suryakencana (Pecinan)", "Suryakencana (Chinatown)"),
        ["district.rural"] = new("Pinggiran Bogor", "Bogor Countryside"),
        ["pond.empang"] = new("Empang Haji Dudung", "Haji Dudung's Pond"),
        ["pond.situgede"] = new("Situ Gede", "Situ Gede Lake"),
        ["pond.telagawarna"] = new("Telaga Warna", "Telaga Warna Lake"),
        ["road.lingkarkebunraya"] = new("Jalur Lingkar Kebun Raya", "Botanical Gardens Ring Road"),
        ["road.kota"] = new("Jalan Kota", "City Street"),
        ["road.pajajaran"] = new("Jalan Raya Pajajaran", "Pajajaran Road"),
        ["road.otista"] = new("Jalan Otto Iskandardinata", "Otto Iskandardinata Street"),
        ["road.juanda"] = new("Jalan Ir. H. Juanda", "Ir. H. Juanda Street"),
        ["road.jalakharupat"] = new("Jalan Jalak Harupat", "Jalak Harupat Street"),
        ["road.sudirman"] = new("Jalan Jenderal Sudirman", "Jenderal Sudirman Street"),
        ["road.ahmadyani"] = new("Jalan Jenderal Ahmad Yani", "Jenderal Ahmad Yani Street"),
        ["road.pemuda"] = new("Jalan Pemuda", "Pemuda Street"),
        ["road.suryakencana"] = new("Jalan Suryakencana", "Suryakencana Street"),
        ["road.siliwangi"] = new("Jalan Siliwangi", "Siliwangi Street"),
        ["road.muslihat"] = new("Jalan Kapten Muslihat", "Kapten Muslihat Street"),
        ["road.merdeka"] = new("Jalan Merdeka", "Merdeka Street"),
        ["road.empang"] = new("Jalan Empang", "Empang Street"),
        ["road.pahlawan"] = new("Jalan Pahlawan", "Pahlawan Street"),
        ["road.pajajaran"] = new("Jalan Raya Pajajaran", "Pajajaran Road"),
        ["road.otista"] = new("Jalan Otto Iskandardinata", "Otto Iskandardinata Street"),
        ["road.juanda"] = new("Jalan Ir. H. Juanda", "Ir. H. Juanda Street"),
        ["road.jalakharupat"] = new("Jalan Jalak Harupat", "Jalak Harupat Street"),
        ["road.sudirman"] = new("Jalan Jenderal Sudirman", "Jenderal Sudirman Street"),
        ["road.ahmadyani"] = new("Jalan Jenderal Ahmad Yani", "Jenderal Ahmad Yani Street"),
        ["road.pemuda"] = new("Jalan Pemuda", "Pemuda Street"),
        ["road.suryakencana"] = new("Jalan Suryakencana", "Suryakencana Street"),
        ["road.siliwangi"] = new("Jalan Siliwangi", "Siliwangi Street"),
        ["road.muslihat"] = new("Jalan Kapten Muslihat", "Kapten Muslihat Street"),
        ["road.merdeka"] = new("Jalan Merdeka", "Merdeka Street"),
        ["road.empang"] = new("Jalan Empang", "Empang Street"),
        ["road.pahlawan"] = new("Jalan Pahlawan", "Pahlawan Street"),
        ["road.pusatkota"] = new("Jalan Kota", "City Street"),
        ["road.gangselot"] = new("Gang Selot", "Selot Alley"),
        ["road.dramaga"] = new("Jalan Raya Dramaga", "Dramaga Road"),
        ["road.jalanbaru"] = new("Jalan Baru", "Jalan Baru"),
        ["road.jagorawi"] = new("Tol Jagorawi", "Jagorawi Toll Road"),
        ["road.tajur"] = new("Jalan Raya Tajur", "Tajur Road"),
        ["road.puncak"] = new("Jalan Raya Puncak", "Puncak Road"),
        ["road.sentul"] = new("Jalan Sentul", "Sentul Street"),
        ["road.stasiun"] = new("Jalan Stasiun", "Station Road"),
        ["river.ciliwung"] = new("Sungai Ciliwung", "Ciliwung River"),
        ["rail.krl"] = new("Jalur KRL Bogor", "Bogor Commuter Line"),
        ["poi.stasiunbogor"] = new("Stasiun Bogor", "Bogor Station"),
        ["poi.tugukujang"] = new("Tugu Kujang", "Kujang Monument"),
        ["poi.gerbangkebunraya"] = new("Gerbang Kebun Raya", "Botanical Gardens Gate"),
        ["poi.istanarusa"] = new("Padang Rusa Istana", "Palace Deer Lawn"),
        ["poi.istanabogor"] = new("Istana Bogor", "Bogor Palace"),
        ["poi.museumzoologi"] = new("Museum Zoologi", "Zoology Museum"),
        ["poi.museumtanah"] = new("Museum Tanah dan Pertanian", "Soil and Agriculture Museum"),
        ["poi.museumpeta"] = new("Museum PETA (Pembela Tanah Air)", "PETA Museum (Defenders of the Homeland)"),
        ["poi.balaikota"] = new("Balai Kota Bogor", "Bogor City Hall"),
        ["poi.katedral"] = new("Katedral Bogor", "Bogor Cathedral"),
        ["poi.masjidraya"] = new("Masjid Raya Bogor", "Bogor Grand Mosque"),
        ["poi.botanisquare"] = new("Botani Square", "Botani Square"),
        ["poi.btm"] = new("Bogor Trade Mall (BTM)", "Bogor Trade Mall (BTM)"),
        ["poi.yogyajunction"] = new("Yogya Bogor Junction", "Yogya Bogor Junction"),
        ["poi.jambudua"] = new("Plaza Jambu Dua", "Plaza Jambu Dua"),
        ["poi.ramayana"] = new("Ramayana", "Ramayana"),
        ["poi.ekalokasari"] = new("Plaza Ekalokasari", "Ekalokasari Plaza"),
        ["poi.lottemart"] = new("Lotte Mart", "Lotte Mart"),
        ["poi.bogorgreatmall"] = new("Bogor Great Mall", "Bogor Great Mall"),
        ["poi.pasarbogor"] = new("Pasar Bogor", "Pasar Bogor Market"),
        ["poi.suryakencana"] = new("Lawang Suryakencana", "Suryakencana Gate"),
        ["poi.airmancur"] = new("Air Mancur", "The Fountain"),
        ["poi.sempur"] = new("Lapangan Sempur", "Sempur Field"),
        ["poi.gorpajajaran"] = new("GOR Pajajaran", "Pajajaran Sports Ground"),
        ["poi.alunalun"] = new("Alun-alun Kota Bogor", "Bogor Town Square"),
        ["poi.tamankencana"] = new("Taman Kencana", "Taman Kencana Park"),
        ["poi.baranangsiang"] = new("Terminal Baranangsiang", "Baranangsiang Bus Terminal"),
        ["poi.katulampa"] = new("Bendung Katulampa", "Katulampa Weir"),
        ["poi.istanabogor"] = new("Istana Bogor", "Bogor Palace"),
        ["poi.museumzoologi"] = new("Museum Zoologi", "Zoology Museum"),
        ["poi.museumtanah"] = new("Museum Tanah dan Pertanian", "Soil and Agriculture Museum"),
        ["poi.museumpeta"] = new("Museum PETA (Pembela Tanah Air)", "PETA Museum (Defenders of the Homeland)"),
        ["poi.balaikota"] = new("Balai Kota Bogor", "Bogor City Hall"),
        ["poi.katedral"] = new("Katedral Bogor", "Bogor Cathedral"),
        ["poi.masjidraya"] = new("Masjid Raya Bogor", "Bogor Grand Mosque"),
        ["poi.botanisquare"] = new("Botani Square", "Botani Square"),
        ["poi.btm"] = new("Bogor Trade Mall (BTM)", "Bogor Trade Mall (BTM)"),
        ["poi.yogyajunction"] = new("Yogya Bogor Junction", "Yogya Bogor Junction"),
        ["poi.jambudua"] = new("Plaza Jambu Dua", "Plaza Jambu Dua"),
        ["poi.ramayana"] = new("Ramayana", "Ramayana"),
        ["poi.ekalokasari"] = new("Plaza Ekalokasari", "Ekalokasari Plaza"),
        ["poi.lottemart"] = new("Lotte Mart", "Lotte Mart"),
        ["poi.bogorgreatmall"] = new("Bogor Great Mall", "Bogor Great Mall"),
        ["poi.pasarbogor"] = new("Pasar Bogor", "Pasar Bogor Market"),
        ["poi.suryakencana"] = new("Lawang Suryakencana", "Suryakencana Gate"),
        ["poi.airmancur"] = new("Air Mancur", "The Fountain"),
        ["poi.sempur"] = new("Lapangan Sempur", "Sempur Field"),
        ["poi.gorpajajaran"] = new("GOR Pajajaran", "Pajajaran Sports Ground"),
        ["poi.alunalun"] = new("Alun-alun Kota Bogor", "Bogor Town Square"),
        ["poi.tamankencana"] = new("Taman Kencana", "Taman Kencana Park"),
        ["poi.baranangsiang"] = new("Terminal Baranangsiang", "Baranangsiang Bus Terminal"),
        ["poi.katulampa"] = new("Bendung Katulampa", "Katulampa Weir"),
        ["poi.pasaranyar"] = new("Pasar Anyar", "Pasar Anyar Market"),
        ["poi.gangselot"] = new("Gapura Gang Selot", "Selot Alley Gate"),
        ["poi.empang"] = new("Pemancingan Empang", "Empang Fishing Pond"),
        ["poi.rumahujang"] = new("Kontrakan Ujang", "Ujang's Rented Room"),
        ["poi.warungbandrek"] = new("Warung Bandrek Aceng", "Aceng's Bandrek Stall"),
        ["poi.badminton"] = new("Lapangan Badminton Kampung", "Kampung Badminton Court"),
        ["poi.karaoke"] = new("Karaoke Mang Oleh", "Mang Oleh's Karaoke"),
        ["poi.binainsani"] = new("Sekolah Bina Insani", "Bina Insani School"),
        ["poi.lapanganstm"] = new("Lapangan Dekat STM", "Field by the Vocational School"),
        ["poi.terminaldramaga"] = new("Pangkalan Angkot Dramaga", "Dramaga Angkot Terminal"),
        ["poi.kampusipb"] = new("Kampus IPB Dramaga", "IPB Dramaga Campus"),
        ["poi.situgede"] = new("Saung Situ Gede", "Situ Gede Hut"),
        ["poi.jalanbaru"] = new("Halte Jalan Baru", "Jalan Baru Stop"),
        ["poi.sirkuitsentul"] = new("Sirkuit Sentul", "Sentul Circuit"),
        ["poi.ciawi"] = new("Simpang Ciawi", "Ciawi Junction"),
        ["poi.puncakpass"] = new("Puncak Pass", "Puncak Pass"),
        ["poi.villamisterius"] = new("Villa Misterius", "The Mysterious Villa"),
        ["poi.kebunteh"] = new("Kebun Teh", "Tea Plantation"),
    };

    public static Language Language { get; set; } = Language.Indonesia;

    /// <summary>Name for a world key such as <c>poi.tugukujang</c>; unknown keys come back as-is.</summary>
    public static L Name(string key) => Names.TryGetValue(key, out L name) ? name : new L(key, key);

    /// <summary>Rupiah the way people write it: Rp 12.500.</summary>
    public static string Rupiah(long amount)
    {
        string digits = Math.Abs(amount).ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');
        return amount < 0 ? $"-Rp {digits}" : $"Rp {digits}";
    }
}
