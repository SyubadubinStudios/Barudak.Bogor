# Progress — Barudak Bogor

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.

Roadmap ada di [PLAN.md](PLAN.md). Entri terbaru di atas.

## 2026-11-12 — Repo dan CI rilis (sesi 4)

- Proyek dipindahkan ke `C:\experiment\syubadubin\Barudak.Bogor` (repo GitHub `SyubadubinStudios/Barudak.Bogor`) dan folder asal dihapus.
- `ci.yml`: tes Core dan build game di setiap push dan pull request.
- `release.yml`: paket mandiri (tanpa perlu .NET) untuk Windows (installer Inno Setup + zip), Linux (`.deb` + `.tar.gz`), macOS Apple Silicon dan Intel (`.dmg`). Tag `vX.Y.Z` menerbitkan rilis GitHub.
- `dotnet publish` untuk keempat platform sudah dicoba di mesin ini dan berhasil.
- Belum: paket Linux dan macOS belum pernah dijalankan di perangkat sungguhan; macOS belum dinotarisasi; Android, iOS, dan konsol tidak dibuat.

## 2026-11-12 — Kota yang hidup (sesi 3)

### Selesai dan terverifikasi (37 tes lulus; tur `--capture` 21 gambar)

- **Karakter utama dari Rodin**: Ujang, Cecep, Mumu, Deden, Aceng dibuat ulang dengan Rodin dalam pose T, lalu `assets/blender/rig_rodin.py` memotongnya jadi badan, kepala, dua lengan, dua kaki supaya tetap bisa berjalan. Warga (`npc_bapak`, `npc_ibu`, `npc_anak_sd`) dan tujuh pemberi misi lewat skrip yang sama; pemberi misi yang berpose A hanya mendapat kaki.
- **Kota lebih ramai**: target pejalan kaki naik (Pasar 60, Kota 46) dan dikumpulkan dalam radius satu blok dari pemain. Terukur di Pasar Anyar: 54 orang, 18 kendaraan, 10 hewan dalam 6 detik.
- **Warga baru**: bapak, ibu, anak SD; tiap jenis punya beberapa wajah.
- **Hewan liar**: kucing, anjing, ayam muncul di mana pun ada permukiman (model `anjing`, `ayam` dari skrip Blender).
- **Kendaraan baru**: becak, bemo, truk, taksi sedan, di lalu lintas dan terparkir (bisa dikendarai).
- **Pengendara terlihat**: motor, delman, dan becak yang berjalan kini membawa pengendara, kusir, dan pengayuh.
- **Kabut saat hujan**: sekitar dua dari lima hujan membawa kabut di dataran rendah; HUD menulis "berkabut".
- **Rotasi waktu**: pagi, siang, sore, malam tampil di HUD dan diumumkan saat berganti.
- **Langit**: awan, bintang, dan bulan dari langit prosedural Three.Net. Sebelumnya tidak tampak karena arah matahari saat malam dikirim salah.
- **Musik latar**: dua gubahan laras degung yang disintesis kode, "Angkot Jalan" (siang) dan "Hujan Sore" (malam/hujan), saling memudar. Bisa dimatikan di pengaturan.

### Catatan jujur

- **Kredit Rodin habis di tengah jalan.** Yang sempat jadi: 5 karakter utama, bapak, ibu, anak SD. Pedagang, siswi, ojol, mahasiswa, dan pegawai masih model balok Blender; daftar di `rig_rodin.py` tinggal ditambah setelah kredit diisi.
- Lengan hasil potongan menggantung agak melebar (pose A), dan sambungan bahu terlihat dari dekat. Rig tulang sungguhan ada di Fase 2.
- **Musik belum pernah saya dengar.** Not, tempo, dan pencampurannya ditulis dari teori; perlu didengar manusia dan disetel.
- Performa tidak membaik: Hemat 27,9 fps, Sedang 20,4 fps, Tinggi 20,0 fps (Intel UHD 620, 1280 x 720, hujan deras), kini dengan 490–570 draw call.
- Tangan pemain saat mengemudi dan posisi duduk pengendara disetel lewat angka, belum dicek dari semua sudut.

## 2026-11-12 — Aset Rodin dan penyempurnaan UI (sesi 2)

### Selesai dan terverifikasi (lewat tur `--capture`, 16 tangkapan layar di `docs/images`)

- **Ilustrasi 2D Rodin (Nano Banana 2)** di `assets/art`: key art, latar kredit vertikal, 5 potret karakter, ikon, baliho bandrek.
- **Model 3D Rodin (Hyper3D)**: 7 NPC pemberi misi dan tas misterius. Mentahnya (±35 ribu segitiga, 7–8 MB) di `assets/models_hd`; `assets/blender/optimize_rodin.py` menurunkannya ke ±3.500 segitiga dan ±130 KB di `assets/models`.
- **Layar peluncuran** dengan key art, ikon, garis livery berjalan, lalu memudar ke menu utama.
- **Kredit bergulir** (tombol Kredit di menu utama): nama naik sementara lukisan turun dari Puncak ke kota.
- **Potret** di pemilihan karakter dan di dialog saat karakter pemain berbicara.
- **Pemberi misi** berdiri di titik mulai tiap misi sampai misinya selesai.
- Baliho bergambar, ikon jendela, lencana ikon di menu jeda.
- Perbaikan: penanda misi menipis saat didekati, titik mancing di tepi empang dengan saung di sampingnya, HUD disembunyikan saat jeda, variasi tanah di kota.
- Dokumentasi dua bahasa: `README.md`, `docs/en`, `docs/id`, dan `PLAN.md`.

### Catatan jujur

- **Tugu Kujang versi Rodin tidak dipakai.** Hasilnya pucat dan hiasan kujangnya terlalu kecil di dalam game; tugu buatan Blender tetap dipakai. File mentahnya masih ada di `assets/models_hd`.
- Key art memuat tulisan toko dan emblem mobil hasil generator gambar (bukan teks yang saya tulis); layak ditinjau sebelum rilis publik.
- Angka FPS di bawah diukur **sebelum** NPC Rodin ditambahkan dan belum diukur ulang.
- Belum pernah dimainkan langsung dengan keyboard/mouse oleh manusia; yang teruji adalah simulasi (34 tes) dan tur otomatis.
- Belum ada perubahan yang dikirim ke repo ThreeNet.

## 2026-11-12 — Fondasi game (sesi 1)

Stack yang dipilih: **.NET 10 + ThreeNet 0.12.1 (inti Rust/wgpu) + Avalonia 12**. Godot tidak terpasang di mesin ini; ThreeNet tersedia sebagai paket NuGet dan terbukti merender di sini.

### Selesai dan terverifikasi

- `src/BarudakBogor.Core` — seluruh simulasi tanpa ketergantungan render:
  - Dunia 40 km x 40 km dari seed: medan, 8 distrik, ±1.240 ruas jalan, ±32.000 bangunan, 21 tempat, 3 danau/empang.
  - Cuaca (cerah sampai badai petir, kabut Puncak, petir, mati lampu lokal) dan jam game.
  - 9 jenis kendaraan, pemain jalan kaki, lalu lintas, pejalan kaki, 7 jenis hewan.
  - 5 karakter, 8 misi cerita, aktivitas: mancing, badminton, karaoke, ojol, touring Puncak, fotografi, usaha bandrek.
  - 5 karier, simpan/muat JSON, teks dua bahasa.
- `tests/BarudakBogor.Core.Tests` — 34 tes, semuanya lulus.
- `assets/models` — 46 model GLB low-poly dari skrip Blender.
- `src/BarudakBogor.Game` — streaming chunk, bangunan/jalan prosedural, hujan, siang-malam, HUD, minimap, dialog, mini game, menu jeda, audio prosedural, `--capture`, `--bench`.

### Performa (Intel UHD 620, jendela 1280 x 720, hujan deras, naik angkot)

| Kualitas | FPS | Resolusi render |
|---|---|---|
| Hemat | 27,6 | 704 x 396 |
| Sedang | 21,4 | 922 x 518 |
| Tinggi | 20,0 | 1280 x 720 |

### Belum

- Performa di grafis terintegrasi (lihat Fase 1 di PLAN.md).
- Platform selain Windows belum dicoba.
- Sub-agen Blender sempat membuat Blender crash sekali saat build pertama (scene saat itu hanya berisi objek bawaan).
