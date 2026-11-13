# PLAN — Roadmap Barudak Bogor

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.

Status per fase: ✅ selesai · 🟡 sebagian · ⬜ belum. Catatan harian ada di [Progress.md](Progress.md).

## Fase 0 — Fondasi (v0.1) ✅

- ✅ Stack: .NET 10 + Three.Net (inti Rust/wgpu) + Avalonia UI.
- ✅ Simulasi inti tanpa render (`BarudakBogor.Core`) dengan 76 tes.
- ✅ Dunia 40 km x 40 km dari seed, 9 area, streaming chunk.
- ✅ Cuaca, jam, kendaraan, lalu lintas, pejalan kaki, hewan.
- ✅ 5 karakter, 10 misi cerita + 3 sampingan, 7 aktivitas, 5 karier, simpan/muat, dua bahasa.
- ✅ UI: layar peluncuran, menu, HUD, peta, dialog berpotret, mini game, kredit bergulir.
- ✅ 46 model Blender + 8 model Rodin, 10 ilustrasi Rodin.

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

- ✅ **Peta mengikuti Bogor asli**: seluruh jaringan jalan pusat kota (sekitar 300 km, dari jalan raya sampai gang) dari OpenStreetMap, 24 tempat nyata di posisinya, Ciliwung dan rel KRL di alur aslinya.
  - ✅ Papan nama di tiap tempat nyata; istana (pilar dan kubah), museum dan balai kota (pilar), katedral (menara), masjid raya (kubah dan menara) punya bentuk khas dari balok.
  - ✅ Tidak ada jalan utama yang terputus: ujung jaringan asli disambung ke jalan luar kota (diuji `NoMainRoadJustStops`).
  - ⬜ Bentuk bangunan tetap balok bergaya, bukan model khusus. Dramaga, Sentul, dan Puncak masih kisi bangkitan.
  - ⬜ Rel ke selatan (Paledang-Sukabumi), Sungai Cisadane, dan Tol Jagorawi dengan alur asli belum ada.
- ✅ Sungai Ciliwung, jembatan, rel KRL, perlintasan sebidang, Stasiun Bogor, dan KRL yang berjalan.
- ✅ Air sungai dan kolam lebih realistis: shader riak mengalir, kedalaman, buih, Fresnel, cincin hujan; tepian berlumpur dan batu sungai. Tepi air halus: medan di sekitar sungai dan kolam digambar dengan kisi 2 m, dan `Terrain.HeightAt` membaca bentuk yang sama.
- 🟡 Interior: warung bandrek dan kontrakan Ujang bisa dimasuki (dinding, pintu, perabot, tabrakan); kokpit untuk kendaraan tertutup (dasbor, setir, pilar). Kamera belakang belum menyesuaikan diri di dalam ruangan: pakai kamera orang pertama (V). Angkot belum punya kabin penumpang.
- 🟡 Rig tulang dan animasi Blender: karakter pemain (lima tokoh utama) kini satu mesh berkulit di atas 12 tulang dengan klip diam, jalan, lari, dan duduk buatan `rig_bones.py`. Warga dan pemberi misi masih potongan tubuh yang diayun kode (satu mesh berkulit per orang terlalu mahal untuk kerumunan); bola sendi bahunya sudah dinaikkan sejajar bahu.
- ✅ NPC dengan jadwal harian, payung saat hujan, celetukan acak.
- ✅ Musik: degung, dan dangdut di radio saat naik angkot. Semuanya disintesis kode dan belum pernah didengar manusia.
- ✅ Misi sampingan dan lanjutan cerita flashdisk.

## Fase 3 — Visual 🟡

- ✅ Medan berlapis lewat shader kustom WGSL (detail ruang-dunia di atas warna chunk), genangan saat basah dengan riak hujan, pantulan basah (SSR, kualitas Tinggi).
- 🟡 Bayangan lebih halus (filter 5x5 di Tinggi). Model kendaraan dan karakter resolusi tinggi belum: butuh kredit Rodin atau pemodelan manual.
- 🟡 Petir bercabang ✅; kabut berlapis (lembar kabut di ketinggian tetap) ✅; awan volumetrik ray marching ✅ di kualitas Tinggi (`CloudLayer`, ditambahkan ke Three.Net); Sedang tetap memakai gumpalan low-poly.

## Fase 4 — Platform ⬜

- 🟡 Linux dan macOS: paket dibuat oleh CI, belum diuji di perangkat sungguhan.
- ⬜ Android dan iOS: kontrol sentuh, kualitas khusus mobile.
- ⬜ Konsol (Xbox, PlayStation): membutuhkan SDK dan lisensi pihak pertama.
- ✅ CI rilis: installer Windows, `.deb`/`.tar.gz` Linux, `.dmg` macOS.
- ⬜ Tanda tangan kode Windows dan notarisasi macOS (butuh sertifikat berbayar milik studio).

## Di luar rencana saat ini

- Multiplayer.
- Kekerasan terhadap warga: sengaja tidak ada. Pejalan kaki selalu sempat loncat.
