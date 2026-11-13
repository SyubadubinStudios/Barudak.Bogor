# Progress — Barudak Bogor

Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.

Roadmap ada di [PLAN.md](PLAN.md). Entri terbaru di atas.

## 2026-11-13 — Tepi air halus, rig tulang, bahu, awan ray marching (sesi 9)

Diverifikasi dengan 76 tes Core (lulus), 19 tes efek Three.Net (lulus), tur `--capture` penuh, dan `--bench`. **Belum ada yang di-commit**, di kedua repo.

- **Tepi sungai dan kolam halus.** Sel medan di sekitar air digambar dengan kisi 2 m (`Terrain.FineCell`) dari fungsi tinggi yang sama, dan `Terrain.HeightAt` ikut membaca bentuk itu (`WaterDetail`, `GroundAt`), jadi gambar dan gameplay tetap sepakat. Menjauh dari air bobotnya turun ke nol sehingga sel halus menyambung rapat dengan sel 8 m di sebelahnya.
- **Rig tulang untuk pemain.** `assets/blender/rig_bones.py` membuat `<nama>_rig.glb` untuk 8 karakter Rodin berpose T: satu mesh berkulit, 12 tulang (pinggul, punggung, dada, kepala, lengan atas/bawah, paha, betis), bobot kulit dilukis dengan aturan (bukan bobot otomatis Blender, yang gagal pada mesh bangkitan), dan empat klip: `idle`, `walk`, `run`, `sit`. Di game (`EntityRenderer.Rig`) keempat klip diputar sekaligus dan dicampur menurut kecepatan; fase langkah mengikuti jarak tempuh, jadi kaki tidak meluncur. Karakter pemain kini 1 draw call, bukan 6, dan bahunya menyatu.
- **Bola sendi bahu dinaikkan** (permintaan). Penyebab aslinya: lengan pose T hasil Rodin menurun ke arah tangan, sedangkan titik sendi diukur dari rata-rata tinggi seluruh lengan, jadi sendi dan bolanya berada di bawah bahu, dan irisan atas lengan ikut terpotong ke bagian kepala (terlihat sebagai "sayap" di bahu). `rig_rodin.py` kini mengukur sendi di bahu itu sendiri, bola diletakkan sejajar puncak bahu, dan irisan atas lengan kembali ke lengan. 8 karakter dibuat ulang.
- **Awan volumetrik ray marching** (permintaan: ditambahkan langsung ke Three.Net). `ThreeNet.Effects.CloudLayer` + `EffectShaders.Clouds`: lembar di dasar awan yang tiap pikselnya menelusuri lapisan udara di atasnya, dengan noise berlapis, bentuk dasar rata dan puncak bergumpal, cahaya matahari yang diredam awan di depannya (hukum Beer), dan tepi berkilau saat matahari di belakang. Game memakainya di kualitas Tinggi; tutupan dan gelapnya mengikuti cuaca.
- Tur tangkapan layar: perhentian baru `jalan`, `lari`, `naik-motor`.

### Performa (Intel UHD 620, 1280 x 720, hujan deras)

| Kualitas | FPS | Resolusi render |
|---|---|---|
| Hemat | 58,7 | 611 x 344 |
| Sedang | 29,9 | 829 x 467 |
| Tinggi | 20,1 | 768 x 432 |

Tinggi turun dari 22,7 ke 20,1 fps; satu-satunya tambahan di tingkat itu adalah awan ray marching. Hemat dan Sedang tidak memakainya.

### Catatan jujur

- **Three.Net 0.12.2 belum terbit di nuget.org.** Game memakai paket lokal `packages-local/ThreeNet.0.12.2-clouds.1.nupkg` (lihat `nuget.config`), dibangun dari checkout Three.Net yang belum di-commit, dengan pustaka native 0.12.1 yang sudah terbit (ABI tidak berubah). Setelah 0.12.2 terbit: ganti versi di csproj, hapus `packages-local`, `nuget.config`, dan pengecualian di `.gitignore`. Menerbitkan paket adalah keputusan pemilik; tidak saya lakukan.
- Rig tulang hanya untuk pemain. Warga, pengendara, dan pemberi misi masih potongan tubuh; 7 NPC berpose A tidak bisa dirig dengan skrip ini.
- Klip animasi dibuat dari rumus di skrip (ayunan sinus), bukan dianimasikan tangan atau motion capture: gerakannya rapi tetapi kaku. Tidak ada klip lompat, berenang, atau memancing; saat itu karakter memakai pose diam atau jalan.
- Baju panjang (Mumu) meregang di bagian bawah saat lari dan duduk, karena kain dibagi ke tulang paha kiri dan kanan.
- Warna bola sendi bahu diambil dari satu titik lengan baju; pada baju bermotif (batik) bolanya tampak polos.
- Awan: hanya terlihat dari bawah, tidak menjatuhkan bayangan ke tanah, dan sangat redup di malam hari. Agar tidak tersapu kabut jarak, shadernya menghitung balik kabut yang akan diterapkan mesin; ini bergantung pada rumus kabut mesin.
- Pola garis pantulan (SSR) di tanah lapang pada kualitas Tinggi masih ada dan belum ditelusuri.
- Atas permintaan: model resolusi tinggi dan uji main manusia tetap ditunda.

## 2026-11-13 — Air sungai dan kolam (sesi 8)

Diverifikasi dengan 76 tes dan tangkapan layar `ciliwung`, `ciliwung-hujan`, `kolam` (di `docs/images`). Belum di-commit.

- **Shader air** (`Rendering/WaterShader.cs`) untuk Ciliwung dan kolam: riak yang mengalir ke hilir, warna dalam lebih gelap dari tepian, buih di tepi sungai, permukaan yang makin memantul pada sudut landai (Fresnel), tepi yang menipis, dan cincin tetesan saat hujan. Ciliwung cokelat kehijauan, kolam lebih jernih dengan lumut di tepinya.
- **Dasar dan tepian**: dasar sungai berlumpur, tepian tanah basah dan kerikil, balas rel di kiri-kanan bantalan, serta batu-batu di sungai.
- **Empang dipindah** sekitar 150 m ke celah di antara jalan asli; sebelumnya kolam menimpa jalan dan warga berjalan di dalam air. Gang yang terendam di alur sungai tidak lagi dimuat.
- `--capture <folder> --only nama1,nama2` menjalankan sebagian tur saja, untuk memeriksa satu perubahan dengan cepat.

Catatan jujur: garis tepi air masih bergerigi karena medan memakai kisi 8 m; batu sungai tetap tumpukan balok pipih; tidak ada gelombang geometri, air terjun, atau bendung di Katulampa. FPS tidak diukur ulang setelah shader air. Atas permintaan: model resolusi tinggi dan uji main manusia ditunda.

## 2026-11-13 — Jalan asli, papan nama, interior, visual Fase 3 (sesi 7)

Diverifikasi dengan 76 tes (lulus semua) dan tur `--capture` 41 gambar; `docs/images` diperbarui. **Belum ada yang di-commit.**

### Yang dikerjakan

- **Jaringan jalan asli**: kisi jalan bangkitan di pusat kota diganti dengan jalan sebenarnya dari OpenStreetMap (sekitar 300 km: jalan raya, jalan lingkungan, gang), disimpan di `src/BarudakBogor.Core/World/bogor_streets.txt`. Nama jalan asli tampil di HUD.
- **Jalan terputus diperbaiki**: ruas `trunk` (sebagian Jalan Pajajaran) sebelumnya tidak ikut sehingga jalan terpotong; kini ikut. Ujung jalan utama di tepi area data disambung ke jalan luar kota, dan ujung jalur kembar yang berjarak beberapa meter dirapatkan. Tes baru `NoMainRoadJustStops` menjaganya.
- **Celah bahu karakter**: `rig_rodin.py` kini menambahkan bola sendi sewarna lengan baju di tiap bahu; 5 karakter utama dan 3 warga Rodin dibuat ulang.
- **Papan nama dan bentuk khas** untuk tempat nyata (lihat PLAN).
- **Interior**: warung bandrek dan kontrakan Ujang; kokpit kendaraan tertutup menggantikan kamera moncong.
- **Dangdut** di radio angkot.
- **Visual**: shader medan berlapis, genangan dengan riak hujan, SSR dan bayangan lebih halus di Tinggi, petir bercabang, lembar kabut berlapis, awan 3D low-poly.

### Performa (Intel UHD 620, 1280 x 720, hujan deras)

| Kualitas | FPS | Resolusi render |
|---|---|---|
| Hemat | 59,5 | 646 x 364 |
| Sedang | 30,0 | 876 x 492 |
| Tinggi | 22,7 | 768 x 432 |

Tinggi turun dari 29,5 ke 22,7 fps karena SSR, bayangan halus, dan shader medan; resolusi adaptif sudah di batas bawahnya. Hemat tidak memakai efek baru.

### Catatan jujur

- **Fase 2 dan 3 belum sepenuhnya selesai.** Belum: rig tulang dan animasi Blender, model kendaraan/karakter resolusi tinggi (butuh kredit Rodin), kabin penumpang angkot, awan volumetrik sungguhan.
- Bola sendi bahu menutup celah, tetapi lengan tetap potongan kaku; di beberapa karakter bolanya terlihat sebagai tonjolan bulat.
- Pantulan SSR menampilkan pola titik-titik di beberapa sudut (terlihat di tangkapan layar hujan). Belum saya telusuri.
- Kokpit satu bentuk untuk semua kendaraan tertutup (hanya warna dan posisi mata yang berbeda).
- Di dalam ruangan, kamera belakang berada di atas atap; kamera orang pertama yang benar.
- Survei API mesin lewat sub-agen gagal karena batas kuota; saya membacanya sendiri.
- Dangdut dan degung tetap belum pernah didengar manusia. Semua tetap belum diuji main oleh manusia.

## 2026-11-13 — Peta Bogor asli, sungai, rel, misi baru, performa (sesi 6)

Diverifikasi dengan 75 tes (semuanya lulus) dan tur `--capture` 34 gambar; gambar di `docs/images` diperbarui. **Belum ada yang di-commit.**

### Peta mengikuti Bogor asli

- Data diambil dari OpenStreetMap (Overpass) dan diubah ke meter timur/selatan dari Tugu Kujang. Kredit "© kontributor OpenStreetMap (ODbL)" ada di kredit game dan README.
- **Jalan utama memakai garis aslinya**: lingkar Kebun Raya (Pajajaran, Otista, Juanda, Jalak Harupat) dan Sudirman, Ahmad Yani, Pemuda, Pajajaran utara-selatan, Suryakencana, Siliwangi, Kapten Muslihat/Veteran, Merdeka, Empang, Pahlawan. Tol Jagorawi kini berawal di dekat Terminal Baranangsiang; Jalan Tajur menyambung dari ujung Pajajaran.
- **24 tempat nyata** di posisi sebenarnya (tes `RealBogorTests` memeriksa jaraknya dari koordinat asli dan bahwa semuanya bisa dicapai lewat jalan): Istana Bogor, Museum Zoologi, Museum Tanah dan Pertanian, Museum PETA, Balai Kota, Katedral, Masjid Raya, Botani Square, BTM, Yogya Bogor Junction, Plaza Jambu Dua, Ramayana, Ekalokasari, Lotte Mart, Bogor Great Mall, Pasar Bogor, Lawang Suryakencana, Air Mancur, Lapangan Sempur, GOR Pajajaran, Alun-alun, Taman Kencana, Terminal Baranangsiang, Bendung Katulampa. Ditambah area baru Suryakencana (Pecinan). Pasar Anyar dan Stasiun Bogor dipindah ke posisi aslinya.
- **Sungai Ciliwung** mengikuti alur asli dari atas Katulampa, membelah Kebun Raya, sampai tepi utara peta. Medan dipahat menjadi alur; setiap jalan yang melintas otomatis mendapat jembatan (dek, tembok pembatas, pilar).
- **Rel KRL** mengikuti alur asli dari Stasiun Bogor ke utara, dengan perlintasan sebidang. Stasiun punya gedung, peron, kanopi seng, dan sepur badug. **KRL** empat gerbong bolak-balik; ia mengerem dan membunyikan klakson kalau pemain ada di rel, dan lalu lintas menunggu di perlintasan.

### Hidup kota dan cerita

- Warga punya jadwal harian (anak sekolah hanya terlihat sebelum dan sesudah jam pelajaran, pegawai saat berangkat/makan siang/pulang, malam hampir sepi), payung saat hujan, dan celetukan untuk pemain yang berjalan kaki.
- Cerita flashdisk dilanjutkan sampai tamat: **Isi Flashdisk** (rencana "Bogor Mega Parkir" di Kebun Raya) dan **Rapat Warga** (tiga cara menyelesaikan, tanpa kekerasan). Tiga misi sampingan: Paket Nyasar, Rusa Kabur, Delman Istimewa.

### Kontrol dan kamera

- Gamepad untuk bermain dan untuk menu (sorotan d-pad, A, B, LB/RB ganti tab, Y memilih tempat di peta). Tur `--capture` menyetir dan menavigasi menu lewat pad virtual.
- Kamera belakang mendekat saat terhalang pohon atau kendaraan; kamera orang pertama (V).
- Pengejar prolog berpasangan: satu mengikuti, satu menyalip lalu memblokir.

### Performa (Intel UHD 620, jendela 1280 x 720, hujan deras, naik angkot)

| Kualitas | Sesi 5 | Sekarang | Resolusi render |
|---|---|---|---|
| Hemat | 30,3 fps | 59,7 fps | 634 x 356 |
| Sedang | 31,4 fps | 29,9 fps | 922 x 518 |
| Tinggi | 18,1 fps | 29,5 fps | 768 x 432 |

- Hemat terkunci di 30 fps karena resolusi timer Windows (15,6 ms): Avalonia menunggu garis kisi berikutnya. Kini game meminta resolusi 1 ms.
- Resolusi adaptif: menurunkan resolusi render agar frame masuk ke refresh layar. Tinggi mencapai 30 fps dengan cara itu (jadi gambarnya lebih lunak dari 1280 x 720 penuh).
- LOD: chunk jauh memakai dinding polos dan tanpa perabot jalan; radius pandang Sedang/Tinggi naik dari 3 ke 4 chunk.
- `GameViewport` menggantikan `ThreeNetView` di game, supaya resolusi bisa diubah tanpa membuat ulang renderer.
- Angka antar-run berfluktuasi beberapa fps; laptop uji memanas selama sesi.

### Bug yang ditemukan dan diperbaiki

- Pengejar kabur ke arah berlawanan di ruas jalan panjang (perencana rute menuju simpul di belakang pemain). Sudah ada sejak sesi 1.
- Tombol pad virtual tidak pernah terbaca sebagai "baru ditekan" kalau ditulis di antara dua polling; tepi tombol kini dihitung oleh game sendiri.
- **Tur `--capture` dan `--bench` menimpa simpanan dan pengaturan pemain.** Kini keduanya tidak menulis apa pun. Catatan: saat menguji, saya menghapus `save.json` di `%APPDATA%\BarudakBogor` yang baru saja dibuat oleh tur itu sendiri (sebelumnya tidak ada simpanan, terlihat dari tangkapan layar judul).
- Tabrakan bisa mendorong pemain dari satu dinding ke dalam dinding sebelahnya; kini diulang sampai bebas, dengan jalan terdekat sebagai jalan keluar terakhir.

### Catatan jujur

- **"Seperti Bogor asli" baru berlaku untuk kerangka kota**: jalan utama, sungai, rel, dan letak tempat. Jalan kecil tetap kisi bangkitan, dan bangunan tempat nyata adalah kotak generik bergaya mal/gedung tanpa papan nama; Istana, katedral, dan masjid tidak mirip aslinya. Air mancur hanya kolam dan tiang dari balok.
- Posisi tempat digeser ke tepi jalan terdekat, jadi bisa meleset sampai sekitar 200 m dari koordinat asli (lebih jauh untuk Lotte Mart dan Bogor Great Mall, yang jalan aslinya belum ada di peta).
- "Ramayana" memakai titik OpenStreetMap di dekat Plaza Jambu Dua. Gang Selot dan empang tetap fiksi.
- Jembatan muncul di setiap jalan arteri kisi yang memotong sungai, lebih banyak dari aslinya. KRL menembus pejalan kaki dan kendaraan lalu lintas yang kebetulan berhenti di atas rel; hanya pemain yang dijamin aman.
- Dunia berubah total dari v0.1.0: simpanan lama tetap bisa dimuat, tetapi posisi pemain bisa jatuh di tempat yang berbeda.
- Belum dicoba dengan gamepad fisik. Kesulitan misi baru dan kejar-kejaran hanya diuji oleh bot. Dialog Sunda di misi baru dan celetukan warga belum ditinjau penutur asli.
- "Mode kokpit" di kendaraan tertutup sebenarnya kamera moncong.
- Perbaikan `ThreeNetView` di checkout Three.Net (`C:\experiment\VibeCoding\Vibe_Graphics\ThreeNet`) belum di-commit dan belum diuji dengan tes repo itu. Saat diukur lewat game, hasilnya setara dengan pompa frame milik game.
- Belum dikerjakan dari roadmap: interior, rig tulang, dangdut radio, seluruh Fase 3 (visual), Android/iOS/konsol, tanda tangan kode. Uji main manusia dan uji perangkat Linux/macOS tidak bisa saya lakukan.

## 2026-11-12 — Performa (sesi 5)

Rilis pertama **v0.1.0** terbit di GitHub (installer Windows, zip, `.deb`, `.tar.gz`, dua `.dmg`); rilis itu belum memuat perubahan di bawah.

### Yang ditemukan

- **Angka FPS lama untuk Sedang dan Tinggi salah.** Alat ukur `--bench` memakai waktu simulasi yang dibatasi 50 ms per frame, sehingga tidak pernah melaporkan di bawah 20 fps. Dengan waktu nyata, sebelum perbaikan: Hemat 31,7 · Sedang 22,4 · Tinggi 15,1 fps.
- **Hambatannya penjadwal frame, bukan isi adegan.** Mematikan lampu, hujan, kabut, langit prosedural, atau memperkecil radius dunia tidak mengubah FPS. Kontrol `ThreeNetView` berdetak lewat `DispatcherTimer`, yang di Windows berjalan di kisi ±15,6 ms, sehingga setiap frame dibulatkan ke atas (31, 45, 66 ms).

### Yang diubah

- Frame kini dipompa dari refresh layar (`RequestAnimationFrame`) dan timer bawaan kontrol dimatikan.
- `assets/blender/bake_palette.py`: warna datar tiap model low-poly digabung ke satu tekstur palet, jadi satu bagian = satu draw call. Total 684 → 205 primitif di semua model; di adegan uji 570 → sekitar 210 draw call. Tampilan tidak berubah.
- `--bench` memakai waktu nyata dan melaporkan waktu kerja per frame.
- Perbaikan crash saat jendela ditutup dari dalam sebuah frame.

### Hasil (Intel UHD 620, jendela 1280 x 720, hujan deras, naik angkot)

| Kualitas | Sebelum | Sesudah | Waktu kerja per frame |
|---|---|---|---|
| Hemat | 31,7 fps | 30,3 fps | 13,9 ms |
| Sedang | 22,4 fps | 31,4 fps | 24,4 ms |
| Tinggi | 15,1 fps | 18,1 fps | 50,4 ms |

### Catatan jujur

- Penggabungan draw call sendiri tidak menaikkan FPS di mesin ini (GPU-nya terikat piksel); manfaatnya baru terasa di CPU yang lebih lemah atau adegan yang lebih padat.
- Hemat terkunci di 30 fps walau kerjanya hanya 13,9 ms: sisa waktu habis di komposisi Avalonia dan frame jatuh ke refresh berikutnya. Belum diselidiki.
- Tinggi memang berat untuk grafis terintegrasi (MSAA 4x, SSAO, bayangan 3 kaskade pada resolusi penuh).
- Bahan emisif (lampu depan, jendela villa) kini warna biasa di palet, tidak lagi berpendar sendiri.
- Perbaikan penjadwal sebaiknya juga masuk ke `ThreeNetView` di repo Three.Net; belum saya kerjakan.

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
