# PLAN — Roadmap Barudak Bogor

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.

Status per fase: ✅ selesai · 🟡 sebagian · ⬜ belum. Catatan harian ada di [Progress.md](Progress.md).

## Fase 0 — Fondasi (v0.1) ✅

- ✅ Stack: .NET 10 + Three.Net (inti Rust/wgpu) + Avalonia UI.
- ✅ Simulasi inti tanpa render (`BarudakBogor.Core`) dengan 86 tes.
- ✅ Dunia 40 km x 40 km dari seed, 9 area, streaming chunk.
- ✅ Cuaca, jam, kendaraan, lalu lintas, pejalan kaki, hewan.
- ✅ 5 karakter, 10 misi cerita + 3 sampingan, 7 aktivitas, 5 karier, simpan/muat, dua bahasa.
- ✅ UI: layar peluncuran, menu, HUD, peta, dialog berpotret, mini game, kredit bergulir.
- ✅ 71 model Blender + 8 model Rodin, 10 ilustrasi Rodin.

## Fase 1 — Layak dimainkan dengan nyaman 🟡

- ✅ **Performa** (Intel UHD 620, 1280 x 720, hujan deras): Hemat 55-60 fps, Sedang 30 fps, Tinggi sekitar 20 fps setelah efek Fase 3 (SSR, bayangan halus, awan ray marching) ditambahkan. 60 fps di GPU khusus belum diukur (tidak ada perangkatnya).
  - ✅ Frame dipompa dari refresh layar, bukan timer 15,6 ms.
  - ✅ Primitif per model digabung lewat tekstur palet (`bake_palette.py`).
  - ✅ Hemat tidak lagi terkunci di 30 fps: penyebabnya resolusi timer Windows, kini 1 ms selama game berjalan.
  - ✅ Resolusi adaptif (bisa dimatikan di pengaturan).
  - ✅ LOD bangunan: chunk jauh memakai cangkang polos, tanpa perabot jalan.
  - 🟡 Perbaikan penjadwal `ThreeNetView` sudah ditulis di checkout Three.Net lokal (`src/ThreeNet.Avalonia/ThreeNetView.cs`) dan ter-build, tetapi belum di-commit, belum diuji dengan tes Three.Net, dan belum dirilis. Game sendiri kini memakai `GameViewport` miliknya.
- ⬜ Uji main oleh manusia dari awal sampai akhir untuk semua misi; setel kesulitan dan durasi. (Tidak bisa dikerjakan oleh AI.)
- 🟡 Gamepad: bermain dan menu sudah bisa, teruji lewat pad virtual; belum dicoba dengan pad fisik.
- 🟡 Kamera: tabrakan dengan bangunan, pohon, dan kendaraan ✅; orang pertama (V) ✅; kokpit sungguhan menunggu interior kendaraan.
- ✅ Mobil pengejar lebih pintar (satu mendorong, satu menyalip dan memblokir). Kesulitannya belum disetel manusia.

## Fase 2 — Dunia yang lebih hidup 🟡

- ✅ **Peta mengikuti Bogor asli, dari tepi ke tepi**: sekitar 900 km jalan dari OpenStreetMap (semua tol dan jalan raya di seluruh peta; sampai gang di pusat kota, Dramaga, Sentul, dan Cisarua), 24 tempat nyata di posisinya, Ciliwung, Cisadane, rel KRL, dan rel ke Sukabumi di alur aslinya. Tidak ada lagi jalan buatan tangan selain labirin Gang Selot.
  - ✅ Papan nama di tiap tempat nyata.
  - ✅ Tidak ada jalan utama yang terputus selain di tepi peta (diuji `NoMainRoadJustStops`).
  - ✅ Model khusus buatan Blender (`assets/blender/landmarks.py`) untuk Istana Bogor, museum (Zoologi, PETA, Tanah), Balai Kota, Katedral, Masjid Raya, dan Lawang Suryakencana, menggantikan balok bergaya. Dramaga, Sentul, dan Puncak (Cisarua) kini memakai jalan aslinya di posisi aslinya, bukan kisi bangkitan. Mal, terminal, dan stasiun masih balok bergaya; modelnya dibuat dari ingatan bentuk umum, bukan dari foto atau denah.
  - ✅ Rel ke selatan (Bogor Paledang sampai tepi peta arah Sukabumi, dengan jembatan di atas Cisadane dan peron Paledang), Sungai Cisadane (59 km, dari kaki Salak sampai sudut barat laut), Tol Jagorawi, Tol Lingkar Luar Bogor, dan Tol Bocimi di alur aslinya. Belum ada kereta di rel selatan, dan simpang susun digambar sebidang (jalan saling menumpang, tanpa jalan layang).
- ✅ Sungai Ciliwung, jembatan, rel KRL, perlintasan sebidang, Stasiun Bogor, dan KRL yang berjalan.
- ✅ Air sungai dan kolam lebih realistis: shader riak mengalir, kedalaman, buih, Fresnel, cincin hujan; tepian berlumpur dan batu sungai. Tepi air halus: medan di sekitar sungai dan kolam digambar dengan kisi 2 m, dan `Terrain.HeightAt` membaca bentuk yang sama.
- 🟡 Interior: warung bandrek dan kontrakan Ujang bisa dimasuki (dinding, pintu, perabot, tabrakan); kokpit untuk kendaraan tertutup (dasbor, setir, pilar). Kamera belakang belum menyesuaikan diri di dalam ruangan: pakai kamera orang pertama (V). Angkot belum punya kabin penumpang.
- 🟡 Rig tulang dan animasi Blender: karakter pemain (lima tokoh utama) kini satu mesh berkulit di atas 12 tulang dengan sepuluh klip buatan `rig_bones.py`: diam, jalan, lari, duduk, lompat, berenang, mengapung, memancing, badminton, dan bernyanyi. Lompat mengikuti kecepatan naik-turun pemain; berenang dan mengapung dipakai di air; tiga yang terakhir dipakai di mini game-nya. Belum ada joran, raket, atau mikrofon di tangan. Warga dan pemberi misi masih potongan tubuh yang diayun kode (satu mesh berkulit per orang terlalu mahal untuk kerumunan); bola sendi bahunya sudah dinaikkan sejajar bahu.
- ✅ NPC dengan jadwal harian, payung saat hujan, celetukan acak.
- ✅ Lalu lintas yang tertib: kendaraan mengantre di belakang yang lebih lambat, menyalip yang berhenti atau lamban bila lajur lawan kosong, bergantian di persimpangan, dan tidak pernah melangkah ke dalam kendaraan lain (diuji `TrafficNeverDrivesThroughItself`). Lampu merah di simpang jalan raya dalam kota bergiliran per lengan dan dipatuhi (`TrafficStopsAtARedLight`).
- ✅ Kehidupan jalanan: pedagang kaki lima berpikulan, polisi, PNS, badut, pengamen, dan anak punk di antara warga; kawanan merpati yang terbang saat didekati, monyet di Kebun Raya, ayam di kampung; lampu merah, tiang CCTV, spanduk dan light box toko bertulisan, videotron dengan iklan bergantian.
- ✅ Kehidupan air: ikan mas, kura-kura, dan bebek di setiap kolam; ikan, kura-kura, bebek, dan sesekali buaya pemalas di Ciliwung dan Cisadane. Semuanya menjauh bila didekati dan tidak pernah keluar dari air (`WaterAnimalsLiveInTheWaterAndStayThere`). Teratai dan eceng gondok mengapung di kolam dan tepi sungai, gelagah tumbuh di tepiannya (`PlaceWaterPlants`).
  - ⬜ Ikan yang berenang belum terhubung dengan mini game memancing, buaya hanya hanyut dan menjauh, dan tumbuhan air tidak bergoyang.
  - ⬜ Warga jalanan baru berjalan dan berdiri saja: pengamen belum bernyanyi, polisi belum mengatur lalu lintas, PKL belum bisa dibeli dagangannya. Pejalan kaki dan pemain tidak terikat lampu merah.
- ✅ Musik: degung, dan dangdut di radio saat naik angkot. Semuanya disintesis kode dan belum pernah didengar manusia.
- ✅ Misi sampingan dan lanjutan cerita flashdisk.

## Fase 3 — Visual 🟡

- ✅ Medan berlapis lewat shader kustom WGSL (detail ruang-dunia di atas warna chunk), genangan saat basah dengan riak hujan, pantulan basah (SSR, kualitas Tinggi).
- ✅ Pijakan mengikuti permukaan jalan: di lereng, jalan digambar rata dari tepi ke tepi sehingga berdiri di atas tanah; `Terrain.HeightAt` kini menjawab dengan permukaan jalan itu (karakter dan kendaraan tidak lagi tertanam), dan tinggi tanah dibaca dari dua segitiga yang sama dengan mesh.
- ✅ Pola garis pada pantulan (SSR) hilang: ketebalan uji lebih kecil dari satu langkah sinar; kini 1 m.
- 🟡 Bayangan lebih halus (filter 5x5 di Tinggi). Model kendaraan dan karakter resolusi tinggi belum: butuh kredit Rodin atau pemodelan manual.
- 🟡 Petir bercabang ✅; kabut berlapis (lembar kabut di ketinggian tetap) ✅; awan volumetrik ray marching ✅ di kualitas Tinggi (`CloudLayer`, Three.Net 0.12.2 dari nuget.org), kini juga terlihat di malam hari (cahaya bulan dan pendar kota); Sedang tetap memakai gumpalan low-poly.

## Fase 4 — Platform ⬜

- 🟡 Linux dan macOS: paket dibuat oleh CI, belum diuji di perangkat sungguhan.
- ⬜ Android dan iOS: kontrol sentuh, kualitas khusus mobile.
- ⬜ Konsol (Xbox, PlayStation): membutuhkan SDK dan lisensi pihak pertama.
- ✅ CI rilis: installer Windows, `.deb`/`.tar.gz` Linux, `.dmg` macOS.
- ⬜ Tanda tangan kode Windows dan notarisasi macOS (butuh sertifikat berbayar milik studio).

## Di luar rencana saat ini

- Multiplayer.
- Kekerasan terhadap warga: sengaja tidak ada. Pejalan kaki selalu sempat loncat.
