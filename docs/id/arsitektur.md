# Arsitektur

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios. · [English](../en/architecture.md)

## Stack dan alasannya

| Lapisan | Pilihan |
|---|---|
| Bahasa dan runtime | C# di .NET 10 |
| Render, audio | Three.Net 0.12.1: inti Rust (wgpu) di balik C ABI dengan API C#. DirectX 12 di Windows |
| Jendela dan antarmuka | Avalonia UI 12; tampilan 3D adalah kontrol `ThreeNetView` dengan HUD digambar di atasnya |
| Model | Skrip Python Blender (low-poly, tanpa tekstur) dan Rodin/Hyper3D (pemberi misi) |

Dokumen desain menyebut Godot 4. Three.Net dipilih karena brief meminta .NET 10 dengan Rust di bawahnya, tersedia sebagai paket NuGet dengan native siap pakai, dan terbukti merender dengan benar di mesin pengembangan. Game ini tidak menambah kode Rust sendiri: bagian Rust-nya adalah inti mesin grafis.

## Dua proyek, satu aturan

```
src/BarudakBogor.Core   seluruh simulasi game; tanpa render, tanpa perangkat input, tanpa UI
src/BarudakBogor.Game   jendela: membaca tombol, menjalankan simulasi, menggambar hasilnya
tests/BarudakBogor.Core.Tests
```

**Semua aturan game ada di Core.** `GameSession.Update(dt, in InputState)` memajukan semuanya; jendela hanya menerjemahkan antara pemain, sesi, dan layar. Itu yang membuat misi, fisika, dan ekonomi bisa dites tanpa GPU.

```
tombol/mouse ─► InputState ─► GameSession.Update ─► state ─► WorldRenderer / EntityRenderer / Atmosphere / HUD
                                     │
                                     └─► antrean GameEvent ─► suara, toast, banner, guncangan kamera
```

## Core

| Folder | Isi |
|---|---|
| `World` | `WorldMap` (tidak berubah setelah dibuat), `WorldGenerator`, `Terrain`, `RoadNetwork`, distrik, tempat |
| `Sim` | `GameClock`, `WeatherSystem` |
| `Entities` | `Player`, `Vehicle`, `Population` (lalu lintas, pejalan kaki, hewan) |
| `Gameplay` | `GameSession`, `Progress`, event, penanda, dialog, `SaveSystem` |
| `Missions` | Kelas dasar `Mission` dengan skrip langkah, `MissionManager`, delapan misi cerita |
| `Activities` | Mancing, game ritme di balik badminton dan karaoke, ojol, touring, fotografi, usaha bandrek |
| `Localization` | `L(Id, En)`: setiap teks membawa dua bahasa di tempat ia ditulis |

Hal yang perlu diketahui sebelum mengubahnya:

- **Koordinat.** +X timur, +Z selatan, +Y atas. Yaw 0 menghadap +Z dan yaw positif berbelok ke +X, yaitu sisi kiri. Hanya `MathUtil.Forward`, `Left`, dan `YawOf` yang memuat aturan ini.
- **Dunia adalah fungsi dari seed.** Jalan, bangunan, dan tempat dibuat sekali (sekitar 1,5 detik). Pohon diturunkan per chunk dari hash koordinat, jadi chunk mana pun bisa dibangun dalam urutan apa pun dengan hasil yang sama.
- **Tinggi medan** didefinisikan pada kisi 8 m lalu diinterpolasi. Mesh medan memakai kisi yang sama, jadi tinggi untuk gameplay dan tinggi yang digambar selalu cocok.
- **Kendaraan** memakai model arcade: model kemudi sepeda dengan cengkeraman maju dan samping yang terpisah. Tidak ada solver benda tegar. Lalu lintas digerakkan mengikuti lajur secara kinematik dan berjalan di kiri.
- **Misi** adalah skrip linear dari `Talk`, `Reach`, `Until`, `Ask`, `Do`. Apa pun yang dibuat atau diubah misi didaftarkan lewat `OnEnd` supaya dibatalkan bagaimanapun misi berakhir.
- **Event, bukan callback.** Kode gameplay memanggil `session.Emit` atau `session.Toast`; jendela menguras antrean sekali per frame.

## Game

| File | Peran |
|---|---|
| `Rendering/WorldRenderer` | Streaming chunk 256 m di sekitar pemain; mesh dibangun di thread latar (`ChunkBuilder`) dan diunggah beberapa per frame. Ubin kasar 2 km di luarnya memberi siluet pegunungan |
| `Rendering/ChunkBuilder` | Medan, tekstur tanah per chunk, pita jalan, bangunan digabung jadi satu mesh per chunk, pohon dan properti ber-instance |
| `Rendering/Textures` | Melukis atlas fasad bangunan dan tekstur jalan saat mulai; basah dan cahaya jendela adalah parameter material |
| `Rendering/EntityRenderer` | View yang dipakai ulang untuk pemain, kendaraan, orang, hewan, pemberi misi, dan penanda; jalan dan roda dianimasikan lewat kode dari bagian model yang bernama |
| `Rendering/Atmosphere` | Matahari, langit, kabut, garis hujan, lampu jalan |
| `Rendering/CameraRig` | Kamera orang ketiga |
| `Audio/GameAudio` | Semua suara disintesis saat mulai |
| `Views/MainWindow*.cs` | Loop frame, input, HUD, menu, kredit, tur `--capture` dan `--bench` |
| `Views/MapControl`, `ActivityControl` | Peta dan arena mini game yang digambar sendiri |

## Aset

- `assets/blender/*.py` membangun `assets/models/*.glb`. Konvensi yang diandalkan kode: meter, depan menghadap glTF +Z, titik asal di tanah pada tengah tapak, bagian bergerak sebagai node anak bernama (`wheel_fl`, `leg_l`, `arm_r`, `head`, ...).
- `assets/models_hd` berisi keluaran mentah Rodin; `optimize_rodin.py` mengurangi poligon, menskalakan, dan mengecilkan tekstur ke `assets/models`.
- `assets/art` berisi ilustrasi Rodin (key art, kredit, potret, ikon, baliho).

## Catatan performa

Diukur dengan `--bench` pada 1280 x 720 di Intel UHD 620, hujan deras, sambil menyetir: Hemat 27,6 fps, Sedang 21,4 fps, Tinggi 20,0 fps. Adegan berisi 400–500 draw call dan sekitar 440 ribu segitiga. Biaya yang sudah diketahui: draw call per primitif pada model low-poly dan bangunan pengisi; lihat PLAN.md.
