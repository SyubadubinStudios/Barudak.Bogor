# PLAN — Roadmap Barudak Bogor

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.

Status per fase: ✅ selesai · 🟡 sebagian · ⬜ belum. Catatan harian ada di [Progress.md](Progress.md).

## Fase 0 — Fondasi (v0.1) ✅

- ✅ Stack: .NET 10 + Three.Net (inti Rust/wgpu) + Avalonia UI.
- ✅ Simulasi inti tanpa render (`BarudakBogor.Core`) dengan 37 tes.
- ✅ Dunia 40 km x 40 km dari seed, 8 area, streaming chunk.
- ✅ Cuaca, jam, kendaraan, lalu lintas, pejalan kaki, hewan.
- ✅ 5 karakter, 8 misi cerita, 7 aktivitas, 5 karier, simpan/muat, dua bahasa.
- ✅ UI: layar peluncuran, menu, HUD, peta, dialog berpotret, mini game, kredit bergulir.
- ✅ 46 model Blender + 8 model Rodin, 10 ilustrasi Rodin.

## Fase 1 — Layak dimainkan dengan nyaman 🟡

- 🟡 **Performa**: target 30 fps di Intel UHD 620 pada kualitas Sedang tercapai (31 fps); 60 fps di GPU khusus belum diukur.
  - ✅ Frame dipompa dari refresh layar, bukan timer 15,6 ms.
  - ✅ Primitif per model digabung lewat tekstur palet (`bake_palette.py`).
  - ⬜ Hemat terkunci di 30 fps padahal kerjanya 14 ms: selidiki komposisi Avalonia.
  - ⬜ Usulkan perbaikan penjadwal ke `ThreeNetView` di repo Three.Net.
  - ⬜ LOD bangunan pengisi dan radius chunk adaptif.
- ⬜ Uji main oleh manusia dari awal sampai akhir untuk semua 8 misi; setel kesulitan dan durasi.
- ⬜ Dukungan gamepad (Three.Net sudah menyediakan API-nya).
- ⬜ Kamera: tabrakan dengan pohon dan kendaraan, mode kokpit.
- ⬜ Mobil pengejar di prolog lebih pintar (menyalip, memblokir).

## Fase 2 — Dunia yang lebih hidup ⬜

- ⬜ Interior: warung, kontrakan, angkot.
- ⬜ Sungai Ciliwung, jembatan, rel kereta dan Stasiun Bogor.
- ⬜ Rig tulang dan animasi Blender (jalan, lari, duduk, mancing) menggantikan animasi prosedural.
- ⬜ NPC dengan jadwal harian, payung saat hujan, percakapan acak.
- ⬜ Musik: gamelan degung dan dangdut untuk radio angkot.
- ⬜ Misi sampingan tambahan dan cerita lanjutan flashdisk rahasia.

## Fase 3 — Visual ⬜

- ⬜ Tekstur medan berlapis (shader kustom WGSL), genangan dan pantulan basah (SSR).
- ⬜ Model kendaraan dan karakter resolusi tinggi, bayangan lebih halus.
- ⬜ Petir bercabang, awan volumetrik, kabut Puncak berlapis.

## Fase 4 — Platform ⬜

- 🟡 Linux dan macOS: paket dibuat oleh CI, belum diuji di perangkat sungguhan.
- ⬜ Android dan iOS: kontrol sentuh, kualitas khusus mobile.
- ⬜ Konsol (Xbox, PlayStation): membutuhkan SDK dan lisensi pihak pertama.
- ✅ CI rilis: installer Windows, `.deb`/`.tar.gz` Linux, `.dmg` macOS.
- ⬜ Tanda tangan kode Windows dan notarisasi macOS.

## Di luar rencana saat ini

- Multiplayer.
- Kekerasan terhadap warga: sengaja tidak ada. Pejalan kaki selalu sempat loncat.
