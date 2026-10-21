# SIJastip - Sistem Informasi Jastip (Avalonia 11, .NET 10)

Aplikasi desktop untuk mengelola jasa titip: penyedia membuka event (open PO), pemesan menitip barang,
lalu penyedia memproses pesanan sampai selesai dan mencatat pembayarannya. Data disimpan di SQLite lokal,
jadi tidak perlu server database.

## Menjalankan
1. Pasang .NET 10 SDK.
2. Buka `JastipApp.slnx` / `JastipApp.csproj` di Visual Studio 2022+, Rider, atau VS Code.
3. Tekan F5 (NuGet dipulihkan otomatis), atau lewat terminal: `dotnet run`

## Akun demo
Password = username + `123` (contoh: `rani` / `rani123`).

| Peran    | Username                          |
|----------|-----------------------------------|
| Admin    | admin                             |
| Penyedia | rani, dita, rina, bayu, kevin     |
| Pemesan  | budi, sari, andi, wulan, dewi     |

Akun baru (pemesan atau penyedia) bisa dibuat lewat tombol "Daftar" di halaman login.

## Halaman (sidebar)
| Halaman       | Isi                                                                                         | Peran |
|---------------|---------------------------------------------------------------------------------------------|-------|
| Dashboard     | Kartu statistik (pesanan, omzet, keuntungan, piutang), grafik omzet per hari, pesanan per status, aktivitas terbaru, filter periode | semua (data sesuai peran) |
| Event         | Daftar event, cari/filter, detail, pesan barang, buat/edit/hapus event                      | semua |
| Pesanan       | Tabel pesanan: cari (kode/nama/event/barang), filter status & pembayaran, urutkan, halaman; detail, ubah status, catat pembayaran | semua (pemesan: "Pesanan saya") |
| Pelanggan     | Pemesan yang pernah memesan di event milik penyedia, total belanja & sisa tagihan          | penyedia |
| Pengguna      | Semua akun, aktif/nonaktifkan akun                                                          | admin |
| Pengaturan    | Mode gelap/terang, profil, ganti password, cadangkan & pulihkan database                    | semua (backup: admin) |

## Hak akses
| Aksi                                        | Pemesan                     | Penyedia             | Admin |
|---------------------------------------------|:---------------------------:|:--------------------:|:-----:|
| Lihat, cari, filter event                   | ✓                           | ✓                    | ✓     |
| Memesan / mengubah isi pesanan sendiri      | ✓ (status Diterima, sebelum deadline) |            |       |
| Membatalkan pesanan                         | milik sendiri (status Diterima, sebelum deadline) | event miliknya (sebelum dikirim) | ✓ (sebelum dikirim) |
| Buat event                                  |                             | ✓                    |       |
| Edit / hapus event                          |                             | event miliknya       | semua |
| Ubah status pesanan, catat pembayaran       |                             | event miliknya       | semua |
| Hapus pesanan permanen                      |                             |                      | ✓     |
| Kelola akun, backup/restore database        |                             |                      | ✓     |

Semua hak akses dicek di lapisan Service, bukan hanya disembunyikan di UI.

## Aturan bisnis
- **Kode pesanan** unik otomatis, format `JT-yyMMdd-0001`.
- **Status pesanan**: Diterima → Sudah dibeli → Dikirim → Selesai. Dibatalkan hanya sebelum dikirim.
  Status **Selesai** hanya bisa dipilih bila pesanan sudah lunas.
- **Kuota**: satu pesanan = satu slot. Kuota berkurang saat memesan dan kembali saat dibatalkan/dihapus.
  Satu pemesan hanya punya satu pesanan aktif per event.
- **Harga**: total = subtotal barang + biaya jasa + ongkos kirim. Harga, jasa, dan ongkir disalin ke
  pesanan saat memesan, jadi tidak berubah walau event diedit. Semua nilai uang memakai `decimal`.
- **Pembayaran**: bisa DP/cicilan, tidak boleh melebihi sisa tagihan. Status lunas otomatis saat sisa = 0.
- **Keuntungan** penyedia = total biaya jasa dari pesanan yang tidak dibatalkan.
- Pesanan baru, pembatalan, perubahan pesanan, simpan event, dan pembayaran dijalankan dalam transaksi.

## Database
- File SQLite `jastip.db` di folder data aplikasi:
  - Windows: `%LocalAppData%\Jastiku\`
  - macOS: `~/Library/Application Support/Jastiku/`
  - Linux: `~/.local/share/Jastiku/`
- Dibuat otomatis beserta data contoh saat pertama kali dijalankan.
- Versi skema disimpan di `PRAGMA user_version` (lihat `Data/DbInit.cs`, sekarang versi 3). Database versi
  lama otomatis dipindahkan menjadi `jastip-lama-<tanggal>.db`, lalu database baru dibuat.
  **Naikkan `VersiSkema` setiap kali struktur Model berubah.**
- Reset data: tutup aplikasi lalu hapus `jastip.db`.
- Cadangan (Pengaturan → Database) disimpan di subfolder `backup/`. Sebelum memulihkan, database saat ini
  dicadangkan otomatis (`sebelum-pulih-*.db`).
- Error teknis dicatat ke `error.log`; preferensi tema di `pengaturan.json` (keduanya di folder yang sama).

Tabel: `Pengguna`, `Events`, `JastipItem`, `Pesanan`, `PesananDetail`, `Pembayaran`, `Aktivitas`.

## Struktur
- Models/       Pengguna, JastipEvent, JastipItem, Pesanan (+ status), PesananDetail, Pembayaran, Aktivitas
- Data/         JastipDbContext (relasi, index unik, constraint), DbInit (versi skema), SampleData
- Services/     AuthService, EventService, PesananService (status & pembayaran), DashboardService,
                PenggunaService, BackupService, PengaturanService (tema), AktivitasLog, Sesi, AppException
- Helpers/      Fmt (format rupiah/tanggal), PasswordHasher (PBKDF2), Log (error.log), UiBus (notifikasi)
- ViewModels/   ShellViewModel (sidebar), DashboardViewModel, EventPageViewModel, PesananPageViewModel,
                PelangganPageViewModel, PengaturanViewModel, LoginViewModel, EventViewModel, PesananViewModel, ItemViewModel
- Views/        MainWindow (shell), DashboardView, EventView, PesananView, PelangganView, PengaturanView, LoginWindow
- App.axaml     Design tokens (warna terang & gelap), style bersama, dan pemetaan ViewModel → View

## Catatan teknis
- `AvaloniaUseCompiledBindingsByDefault` masih `false` (binding lewat reflection). Bisa diaktifkan nanti
  dengan menambahkan `x:DataType` di setiap View.
- EF Core 8.0.11 dipakai di atas .NET 10 (sudah diuji berjalan). Bisa diselaraskan ke EF Core 10 bila diperlukan.
- Grafik dan tabel dibuat dengan kontrol bawaan Avalonia (tanpa library chart/DataGrid tambahan).
