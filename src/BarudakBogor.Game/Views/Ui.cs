using BarudakBogor.Localization;
using BarudakBogor.Sim;

namespace BarudakBogor.Game.Views;

/// <summary>Interface text in both languages.</summary>
internal static class Ui
{
    public static readonly L Loading = new("NGARAKIT KOTA BOGOR…", "ASSEMBLING THE CITY OF BOGOR…");
    public static readonly L Eyebrow = new("OPEN WORLD · KOTA HUJAN", "OPEN WORLD · THE RAIN CITY");
    public static readonly L Continue = new("Lanjutkan", "Continue");
    public static readonly L NewGame = new("Main baru", "New game");
    public static readonly L Language = new("Bahasa: Indonesia", "Language: English");
    public static readonly L Settings = new("Pengaturan", "Settings");
    public static readonly L Quit = new("Keluar", "Quit");
    public static readonly L Credits = new("Kredit", "Credits");
    public static readonly L CreditsClose = new("Tutup · Esc", "Close · Esc");

    /// <summary>The credits roll: a role and the names under it.</summary>
    public static readonly (L Role, L Names)[] CreditRoll =
    [
        (new("SEBUAH GAME DARI", "A GAME BY"), new("Syubadubin Studios", "Syubadubin Studios")),
        (new("DIBUAT OLEH", "MADE BY"), new("Ariana Mischa Fadhila", "Ariana Mischa Fadhila")),
        (new("KONSEP & DESAIN GAME", "CONCEPT & GAME DESIGN"), new("Ariana Mischa Fadhila", "Ariana Mischa Fadhila")),
        (new("PEMERAN", "CAST"), new("Ujang · Cecep · Mumu · Deden · Aceng", "Ujang · Cecep · Mumu · Deden · Aceng")),
        (new("WARGA BOGOR", "PEOPLE OF BOGOR"), new(
            "Mang Udin · Juragan Haji Dadang · Bu Euis · Pak Guru BP · Haji Dudung · Abah Aceng · Mang Jagung Bakar",
            "Mang Udin · Boss Haji Dadang · Bu Euis · The School Counsellor · Haji Dudung · Abah Aceng · The Grilled-Corn Seller")),
        (new("PEMERAN PEMBANTU", "SUPPORTING CAST"), new(
            "Seekor embé bermukena · Kucing belepotan bumbu kacang · Rusa Istana · Monyet Puncak pencari jajan",
            "A goat in a prayer robe · A cat covered in peanut sauce · The Palace deer · Snack-hunting Puncak macaques")),
        (new("PETA KOTA", "CITY MAP"), new(
            "Jalan utama, Ciliwung, rel, dan letak tempat dari data © kontributor OpenStreetMap (ODbL)",
            "Main roads, the Ciliwung, the railway and the places from data © OpenStreetMap contributors (ODbL)")),
        (new("MESIN GRAFIS", "GRAPHICS ENGINE"), new("Three.Net — inti Rust (wgpu)", "Three.Net — Rust (wgpu) core")),
        (new("ANTARMUKA", "INTERFACE"), new("Avalonia UI · .NET 10", "Avalonia UI · .NET 10")),
        (new("MODEL 3D", "3D MODELS"), new("Blender (skrip prosedural) · Rodin / Hyper3D", "Blender (procedural scripts) · Rodin / Hyper3D")),
        (new("ILUSTRASI", "ILLUSTRATION"), new("Rodin MCP · Nano Banana 2", "Rodin MCP · Nano Banana 2")),
        (new("HURUF", "TYPEFACES"), new("Bungee · Barlow Condensed (SIL Open Font License)", "Bungee · Barlow Condensed (SIL Open Font License)")),
        (new("MUSIK", "MUSIC"), new("\"Angkot Jalan\" · \"Hujan Sore\" — laras degung, digubah oleh kode", "\"Angkot Jalan\" · \"Hujan Sore\" — degung scale, composed in code")),
        (new("SUARA", "SOUND"), new("Disintesis saat game dibuka. Hujannya asli.", "Synthesised when the game starts. The rain is real.")),
        (new("TERIMA KASIH", "THANK YOU"), new(
            "Para sopir angkot, tukang cilok, penjual bandrek, pemancing empang, dan semua yang pernah neduh di Bogor.",
            "The angkot drivers, cilok sellers, bandrek sellers, pond anglers, and everyone who ever sheltered from the rain in Bogor.")),
        (new("TIDAK ADA YANG TERLUKA", "NOBODY WAS HARMED"), new(
            "Pejalan kaki selalu sempat loncat. Sendalnya saja yang kadang lepas.",
            "Pedestrians always jump clear. Only a sandal is lost now and then.")),
    ];
    public static readonly L Roster = new("PILIH BARUDAK", "PICK YOUR CHARACTER");
    public static readonly L NoSave = new("Belum ada simpanan. Pilih karakter, lalu Main baru.", "No save yet. Pick a character, then New game.");
    public static readonly L HasSave = new("Simpanan terakhir: {0} · hari ke-{1} · {2}", "Last save: {0} · day {1} · {2}");
    public static readonly L NewGameWarning = new("Main baru menimpa simpanan lama.", "A new game overwrites the old save.");

    public static readonly L Resume = new("Lanjut main", "Resume");
    public static readonly L Save = new("Simpan", "Save");
    public static readonly L ToTitle = new("Simpan & ke menu utama", "Save & main menu");
    public static readonly L Saved = new("Permainan disimpan.", "Game saved.");
    public static readonly L SaveBlocked = new("Selesaikan atau batalkan misi dulu sebelum menyimpan.", "Finish or abandon the mission before saving.");
    public static readonly L SaveFailed = new("Gagal menyimpan: {0}", "Could not save: {0}");

    public static readonly L TabMap = new("Peta", "Map");
    public static readonly L TabMissions = new("Misi", "Missions");
    public static readonly L TabCareer = new("Karier", "Career");
    public static readonly L TabSettings = new("Pengaturan", "Settings");
    public static readonly L TabHelp = new("Bantuan", "Help");

    public static readonly L MapHelp = new(
        "RODA MOUSE: ZOOM · SERET: GESER · KLIK TEMPAT: PILIH",
        "MOUSE WHEEL: ZOOM · DRAG: PAN · CLICK A PLACE: SELECT");
    public static readonly L MapPrompt = new("Pilih tempat di peta", "Pick a place on the map");
    public static readonly L MapPromptDetail = new(
        "Tukang ojek siap mengantar ke tempat mana pun yang kamu pilih. Ongkos dihitung dari jarak.",
        "An ojek driver will take you to any place you pick. The fare depends on distance.");
    public static readonly L FastTravel = new("Naik ojek ke sini · {0}", "Take an ojek here · {0}");
    public static readonly L Distance = new("{0:0.0} km dari sini", "{0:0.0} km from here");

    public static readonly L MissionStart = new("MISI DIMULAI", "MISSION STARTED");
    public static readonly L MissionDone = new("MISI BERES", "MISSION COMPLETE");
    public static readonly L MissionFailed = new("MISI GAGAL", "MISSION FAILED");
    public static readonly L Ending = new("GELAR BARU", "NEW TITLE");
    public static readonly L EndingDetail = new("Bogor bangga padamu. Ceritamu masih bisa lanjut.", "Bogor is proud of you. Your story can go on.");
    public static readonly L MissionLabel = new("MISI", "MISSION");
    public static readonly L StoryLabel = new("CERITA", "STORY");
    public static readonly L SideMission = new("Misi sampingan", "Side mission");
    public static readonly L Locked = new("Terkunci: selesaikan \"{0}\" dulu", "Locked: finish \"{0}\" first");
    public static readonly L Available = new("Tersedia di {0}", "Available at {0}");
    public static readonly L Active = new("Sedang berjalan", "In progress");
    public static readonly L Done = new("Selesai", "Completed");
    public static readonly L Abandon = new("Batalkan misi", "Abandon mission");
    public static readonly L Reward = new("Hadiah {0}", "Reward {0}");

    public static readonly L Stamina = new("STAMINA", "STAMINA");
    public static readonly L SpeedUnit = new("KM/JAM", "KM/H");
    public static readonly L Metres = new("{0:0} M LAGI", "{0:0} M TO GO");
    public static readonly L Kilometres = new("{0:0.0} KM LAGI", "{0:0.0} KM TO GO");
    public static readonly L DialogNext = new("SPASI · LANJUT", "SPACE · NEXT");
    public static readonly L DialogChoose = new("TEKAN ANGKA UNTUK MEMILIH", "PRESS A NUMBER TO CHOOSE");
    public static readonly L Ojol = new("OJOL · {0}", "OJOL · {0}");
    public static readonly L OjolFare = new("{0} · sisa {1} dtk", "{0} · {1} s left");
    public static readonly L Touring = new("TOURING PUNCAK · {0}", "PUNCAK TOURING · {0}");
    public static readonly L Selling = new("JUALAN BANDREK", "SELLING BANDREK");
    public static readonly L SellingDetail = new("Stok {0} gelas · terjual {1}", "{0} cups left · {1} sold");
    public static readonly L Blackout = new("MATI LAMPU", "BLACKOUT");
    public static readonly L Misty = new("berkabut", "misty");
    public static readonly L ViewCockpit = new("Kamera: orang pertama", "Camera: first person");
    public static readonly L ViewChase = new("Kamera: dari belakang", "Camera: chase");
    public static readonly L PadTitle = new("GAMEPAD", "CONTROLLER");

    public static readonly (string Keys, L Action)[] PadControls =
    [
        ("L STICK", new("Jalan / setir", "Walk / steer")),
        ("R STICK", new("Putar kamera", "Look around")),
        ("RT · LT", new("Gas · rem", "Throttle · brake")),
        ("A", new("Lompat · aksi · lanjut dialog", "Jump · action · next line")),
        ("X", new("Naik/turun kendaraan · bicara · mulai misi", "Enter/leave vehicle · talk · start mission")),
        ("B", new("Batal · keluar mini game", "Cancel · leave a mini game")),
        ("Y", new("Klakson", "Horn")),
        ("LB · RB", new("Lari · rem tangan", "Run · handbrake")),
        ("D-PAD", new("Atas: ojol · Bawah: potret · Kanan: ganti kamera · pilihan dialog", "Up: ojol · Down: photo · Right: camera view · dialogue choices")),
        ("START · SELECT", new("Menu · peta", "Menu · map")),
        ("MENU", new("D-pad pilih · A tekan · B kembali · LB/RB ganti tab · Y tempat berikutnya di peta · stik kanan gulir", "D-pad moves · A presses · B back · LB/RB change tab · Y next place on the map · right stick scrolls")),
    ];
    public static readonly L Music = new("Musik: {0}", "Music: {0}");
    public static readonly L StartHint = new(
        "Datangi tanda emas di dekat Tugu Kujang untuk memulai cerita. F1 untuk bantuan.",
        "Walk to the gold beacon by Tugu Kujang to start the story. F1 for help.");
    public static readonly L PhotoPaid = new("Foto {0} poin · {1} · dibayar {2}", "Photo {0} pts · {1} · paid {2}");
    public static readonly L PhotoPlain = new("Foto {0} poin · {1}", "Photo {0} pts · {1}");

    public static readonly L Quality = new("Kualitas grafis: {0}", "Graphics quality: {0}");
    public static readonly L QualityNote = new(
        "Hemat untuk laptop tanpa kartu grafis, Tinggi untuk GPU khusus.",
        "Hemat (economy) suits laptops without a graphics card, Tinggi (high) a dedicated GPU.");
    public static readonly L Sound = new("Suara: {0}", "Sound: {0}");
    public static readonly L Volume = new("Volume: {0}%", "Volume: {0}%");
    public static readonly L DynamicResolution = new("Resolusi adaptif: {0}", "Dynamic resolution: {0}");
    public static readonly L DynamicResolutionNote = new(
        "Resolusi render turun sendiri saat GPU kewalahan supaya permainan tetap lancar, lalu naik lagi saat longgar.",
        "The render resolution drops by itself when the GPU cannot keep up, so the game stays smooth, and rises again when there is room.");
    public static readonly L Stats = new("Statistik render (F3): {0}", "Render statistics (F3): {0}");
    public static readonly L On = new("nyala", "on");
    public static readonly L Off = new("mati", "off");
    public static readonly L NoAudio = new("Perangkat audio tidak ditemukan: {0}", "No audio device found: {0}");

    public static readonly L CareerIntro = new(
        "Lima jalan hidup. Capai 100 reputasi di salah satunya untuk meraih gelarnya.",
        "Five ways of life. Reach 100 reputation in one of them to earn its title.");
    public static readonly L StatsTitle = new("CATATAN", "RECORDS");
    public static readonly L StatLines = new(
        "Ikan ditangkap {0} (terbesar {1:0.0} kg) · Penumpang angkot {2} · Order ojol {3} · Foto {4} (terbaik {5} poin) · Jarak nyetir {6:0.0} km · Bandrek terjual {7} gelas",
        "Fish caught {0} (biggest {1:0.0} kg) · Angkot passengers {2} · Ojol orders {3} · Photos {4} (best {5} pts) · Distance driven {6:0.0} km · Bandrek sold {7} cups");

    public static readonly (string Keys, L Action)[] Controls =
    [
        ("W A S D", new("Jalan / nyetir", "Walk / drive")),
        ("MOUSE (seret / drag)", new("Putar kamera", "Look around")),
        ("SHIFT", new("Lari", "Run")),
        ("SPASI / SPACE", new("Lompat · rem tangan · aksi", "Jump · handbrake · action")),
        ("E", new("Naik/turun kendaraan · bicara · mulai misi", "Enter/leave vehicle · talk · start mission")),
        ("H", new("Klakson", "Horn")),
        ("O", new("Aplikasi ojol nyala/mati (di atas motor)", "Ojol app on/off (on a motorbike)")),
        ("P", new("Potret (Fotografi Hujan Bogor)", "Take a photo (Bogor rain photography)")),
        ("V", new("Ganti kamera: belakang / orang pertama", "Camera view: chase / first person")),
        ("M / ESC", new("Peta dan menu", "Map and menu")),
        ("1 2 3 4", new("Pilihan dialog", "Dialogue choices")),
        ("A S D F", new("Karaoke · A dan D untuk badminton", "Karaoke · A and D for badminton")),
        ("F5", new("Simpan cepat", "Quick save")),
    ];

    public static L Weather(WeatherKind kind) => kind switch
    {
        WeatherKind.Cerah => new("Cerah", "Clear"),
        WeatherKind.Berawan => new("Berawan", "Cloudy"),
        WeatherKind.Gerimis => new("Gerimis", "Drizzle"),
        WeatherKind.HujanSedang => new("Hujan sedang", "Rain"),
        WeatherKind.HujanDeras => new("Hujan deras", "Heavy rain"),
        _ => new("Badai petir", "Thunderstorm"),
    };

    public static L QualityName(Quality quality) => quality switch
    {
        Game.Quality.Hemat => new("Hemat", "Hemat (economy)"),
        Game.Quality.Tinggi => new("Tinggi", "Tinggi (high)"),
        _ => new("Sedang", "Sedang (balanced)"),
    };
}
