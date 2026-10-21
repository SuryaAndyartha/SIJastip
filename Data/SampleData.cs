using JastipApp.Helpers;
using JastipApp.Models;
using JastipApp.Services;

namespace JastipApp.Data;

/// <summary>
/// Data contoh untuk database baru: akun demo, event, dan beberapa pesanan.
/// Tanggal dibuat relatif terhadap waktu database dibuat.
/// Password akun demo = username + "123", mis. rani / rani123.
/// </summary>
public static class SampleData
{
    public static void Isi(JastipDbContext db)
    {
        var now = DateTime.Now;

        Pengguna Akun(string username, string nama, Peran peran, string kontak = "") => new()
        {
            Username = username,
            Nama = nama,
            Peran = peran,
            Kontak = kontak,
            PasswordHash = PasswordHasher.Hash(username + "123")
        };

        // ---- Akun ----
        var admin = Akun("admin", "Administrator", Peran.Admin);

        var rani = Akun("rani", "Rani", Peran.Penyedia, "0812-0000-1111");
        var dita = Akun("dita", "Dita", Peran.Penyedia, "0857-0000-2222");
        var rina = Akun("rina", "Mbak Rina", Peran.Penyedia, "0813-0000-3333");
        var bayu = Akun("bayu", "Bayu", Peran.Penyedia, "0878-0000-4444");
        var kevin = Akun("kevin", "Kevin", Peran.Penyedia, "0821-0000-5555");

        var budi = Akun("budi", "Budi Santoso", Peran.Pemesan, "0811-1111-0001");
        budi.Alamat = "Jl. Kertajaya Indah No. 12, Surabaya";
        var sari = Akun("sari", "Sari Wulandari", Peran.Pemesan, "0811-1111-0002");
        var andi = Akun("andi", "Andi Pratama", Peran.Pemesan, "0811-1111-0003");
        var wulan = Akun("wulan", "Wulan Sari", Peran.Pemesan, "0811-1111-0004");
        var dewi = Akun("dewi", "Dewi Lestari", Peran.Pemesan, "0811-1111-0005");

        db.Pengguna.AddRange(admin, rani, dita, rina, bayu, kevin, budi, sari, andi, wulan, dewi);

        // ---- Event ----
        var esTeler = new JastipEvent
        {
            Judul = "Es Teler 77 & Bakso Malang, malam ini",
            Penyedia = rani, Kategori = KategoriJastip.Makanan,
            LokasiBelanja = "Tunjungan Plaza, Surabaya",
            Deskripsi = "Berangkat sepulang kerja, dikirim langsung ke rumah di area Surabaya Timur. Es dikemas terpisah dengan es batu.",
            Kontak = rani.Kontak,
            Deadline = now.AddMinutes(95), EstimasiSampai = now.AddHours(4),
            OngkosKirim = 8000, BiayaJasa = 3000, Kuota = 10,
            Items =
            {
                new() { Nama = "Es Teler Special", Harga = 28000, Catatan = "Tanpa susu bisa" },
                new() { Nama = "Es Campur", Harga = 26000 },
                new() { Nama = "Bakso Malang Komplit", Harga = 32000, Catatan = "Kuah dipisah" },
            }
        };

        var skincare = new JastipEvent
        {
            Judul = "Skincare Korea dari Olive Young",
            Penyedia = dita, Kategori = KategoriJastip.Barang,
            LokasiBelanja = "Seoul, Korea Selatan",
            Deskripsi = "Titip skincare dan makeup. Pembayaran DP 50% setelah pesanan dikonfirmasi, pelunasan sebelum barang dikirim.",
            Kontak = dita.Kontak,
            Deadline = now.AddHours(5), EstimasiSampai = now.AddDays(14),
            OngkosKirim = 25000, BiayaJasa = 15000, Kuota = 20,
            Items =
            {
                new() { Nama = "Round Lab Birch Juice Toner", Harga = 215000 },
                new() { Nama = "Anua Heartleaf Cleansing Oil", Harga = 245000, Catatan = "200 ml" },
                new() { Nama = "Medicube Zero Pore Pad", Harga = 260000 },
                new() { Nama = "Romand Juicy Lasting Tint", Harga = 125000, Catatan = "Sebutkan nomor warna" },
            }
        };

        var bakpia = new JastipEvent
        {
            Judul = "Bakpia Pathok & Gudeg Kering Jogja",
            Penyedia = rina, Kategori = KategoriJastip.Makanan,
            LokasiBelanja = "Malioboro dan Pathok, Yogyakarta",
            Deskripsi = "Oleh-oleh dibawa dengan kereta, tiba di Surabaya Sabtu. Bisa COD di Gubeng atau dikirim kurir.",
            Kontak = rina.Kontak,
            Deadline = now.AddDays(2).AddHours(3), EstimasiSampai = now.AddDays(5),
            OngkosKirim = 15000, BiayaJasa = 5000, Kuota = 15,
            Items =
            {
                new() { Nama = "Bakpia Pathok 25 Isi Kumbu Hitam", Harga = 35000 },
                new() { Nama = "Bakpia Pathok 25 Isi Keju", Harga = 38000 },
                new() { Nama = "Gudeg Kering Bu Tjitro", Harga = 55000, Catatan = "Kaleng, tahan 6 bulan" },
                new() { Nama = "Yangko Aneka Rasa", Harga = 22000 },
            }
        };

        var sneakers = new JastipEvent
        {
            Judul = "Sneakers & Jersey Original Singapura",
            Penyedia = bayu, Kategori = KategoriJastip.Barang,
            LokasiBelanja = "Orchard Road, Singapura",
            Deskripsi = "Barang dijamin original, struk dikirim sebagai bukti. Ongkir dihitung per kg setelah barang ditimbang.",
            Kontak = bayu.Kontak,
            Deadline = now.AddDays(6), EstimasiSampai = now.AddDays(12),
            OngkosKirim = 50000, BiayaJasa = 75000, Kuota = 3,
            Items =
            {
                new() { Nama = "Adidas Samba OG", Harga = 1800000 },
                new() { Nama = "Jersey Singapore Lions 2026", Harga = 650000 },
            }
        };

        var thrift = new JastipEvent
        {
            Judul = "Thrift Jaket & Kemeja Pasar Senen",
            Penyedia = kevin, Kategori = KategoriJastip.Barang,
            LokasiBelanja = "Pasar Senen, Jakarta",
            Deskripsi = "Event sebelumnya, sudah ditutup. Pantau terus untuk batch berikutnya.",
            Kontak = kevin.Kontak,
            Deadline = now.AddDays(-1), EstimasiSampai = now.AddDays(3),
            OngkosKirim = 20000, BiayaJasa = 10000, Kuota = 12,
            Items =
            {
                new() { Nama = "Jaket Denim Vintage", Harga = 150000 },
                new() { Nama = "Kemeja Flanel", Harga = 85000 },
            }
        };

        db.Events.AddRange(esTeler, skincare, bakpia, sneakers, thrift);
        db.SaveChanges();   // agar Id pengguna & event tersedia untuk catatan aktivitas

        // ---- Pesanan contoh: status & pembayaran bervariasi, tersebar 7 hari terakhir ----
        var nomor = 0;
        void Pesan(JastipEvent ev, Pengguna pemesan, double hariLalu, StatusPesanan status,
                   decimal dibayarPersen, params (int item, int qty)[] isi)
        {
            var waktu = now.AddDays(-hariLalu);
            var p = new Pesanan
            {
                Kode = $"JT-{waktu:yyMMdd}-{++nomor:D4}",
                Event = ev,
                Pemesan = pemesan,
                Waktu = waktu,
                Status = status,
                BiayaJasa = ev.BiayaJasa,
                OngkosKirim = ev.OngkosKirim,
                Detail = isi.Select(x => new PesananDetail
                {
                    NamaBarang = ev.Items[x.item].Nama,
                    Harga = ev.Items[x.item].Harga,
                    Qty = x.qty
                }).ToList()
            };
            if (dibayarPersen > 0)
                p.Pembayaran.Add(new Pembayaran
                {
                    Tanggal = waktu.AddHours(2),
                    Jumlah = Math.Round(p.Total * dibayarPersen / 100m, 0),
                    Metode = "Transfer",
                    Catatan = dibayarPersen >= 100 ? "Lunas" : $"DP {dibayarPersen}%"
                });
            db.Pesanan.Add(p);
            if (status != StatusPesanan.Dibatalkan) ev.Terisi++;

            AktivitasLog.Catat(db, pemesan, "Tambah", "Pesanan", p.Kode,
                $"{pemesan.Nama} memesan di \"{ev.Judul}\" ({p.Kode})", ev.PenyediaId, pemesan.Id, waktu);
        }

        Pesan(esTeler, sari, 0.10, StatusPesanan.Diterima, 0, (0, 2));
        Pesan(esTeler, andi, 0.05, StatusPesanan.Diterima, 100, (2, 1), (1, 1));
        Pesan(esTeler, wulan, 0.02, StatusPesanan.Diterima, 0, (0, 1));

        Pesan(skincare, sari, 2, StatusPesanan.Diterima, 50, (0, 1), (3, 2));
        Pesan(skincare, dewi, 1, StatusPesanan.Diterima, 0, (1, 1));

        Pesan(bakpia, andi, 1.5, StatusPesanan.Diterima, 100, (1, 2), (2, 1));

        Pesan(sneakers, sari, 5, StatusPesanan.Dibeli, 50, (0, 1));
        Pesan(sneakers, andi, 4, StatusPesanan.Dibeli, 100, (1, 2));
        Pesan(sneakers, wulan, 3, StatusPesanan.Diterima, 0, (0, 1));

        Pesan(thrift, budi, 6, StatusPesanan.Selesai, 100, (0, 1));
        Pesan(thrift, dewi, 5, StatusPesanan.Dikirim, 100, (1, 2));
        Pesan(thrift, andi, 4, StatusPesanan.Dibatalkan, 0, (0, 1));

        db.SaveChanges();
    }
}
