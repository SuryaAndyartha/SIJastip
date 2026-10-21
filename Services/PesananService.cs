using JastipApp.Data;
using JastipApp.Helpers;
using JastipApp.Models;
using Microsoft.EntityFrameworkCore;

namespace JastipApp.Services;

/// <summary>
/// Pesanan: buat, ubah, batalkan, ubah status, hapus, dan pembayaran.
/// Setiap perubahan yang menyentuh kuota atau tagihan dijalankan dalam transaksi dan dicatat ke Aktivitas.
/// </summary>
public class PesananService
{
    public static readonly string[] MetodePembayaran = { "Transfer", "Tunai", "E-wallet", "QRIS" };

    // =====================================================================
    //  Membaca
    // =====================================================================

    private static IQueryable<Pesanan> Lengkap(JastipDbContext db) =>
        db.Pesanan.AsNoTracking().AsSplitQuery()
            .Include(p => p.Detail)
            .Include(p => p.Pembayaran)
            .Include(p => p.Pemesan)
            .Include(p => p.Event).ThenInclude(e => e.Penyedia);

    /// <summary>
    /// Pesanan yang boleh dilihat pengguna yang login:
    /// pemesan → miliknya; penyedia → pesanan di event miliknya; admin → semua.
    /// </summary>
    public async Task<List<Pesanan>> DaftarAsync()
    {
        var user = Sesi.Wajib;
        await using var db = new JastipDbContext();
        var q = Lengkap(db);
        q = user.Peran switch
        {
            Peran.Pemesan => q.Where(p => p.PemesanId == user.Id),
            Peran.Penyedia => q.Where(p => p.Event.PenyediaId == user.Id),
            _ => q
        };
        var list = await q.ToListAsync();
        return list.OrderByDescending(p => p.Waktu).ToList();
    }

    public async Task<List<Pesanan>> PesananSayaAsync()
    {
        if (Sesi.Wajib.Peran != Peran.Pemesan) return new();
        return await DaftarAsync();
    }

    /// <summary>Pesanan yang masuk ke satu event. Hanya untuk penyedia pemilik event atau admin.</summary>
    public async Task<List<Pesanan>> PesananEventAsync(Guid eventId)
    {
        var user = Sesi.Wajib;
        await using var db = new JastipDbContext();
        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId)
                 ?? throw new AppException("Event tidak ditemukan.");
        if (!EventService.BisaKelola(user, ev)) throw new AksesDitolakException();

        var list = await Lengkap(db).Where(p => p.EventId == eventId).ToListAsync();
        return list.OrderBy(p => p.Status == StatusPesanan.Dibatalkan).ThenByDescending(p => p.Waktu).ToList();
    }

    // =====================================================================
    //  Membuat & mengubah isi pesanan (pemesan)
    // =====================================================================

    /// <summary>Membuat pesanan. qtyPerItem: Id barang → jumlah. Harga diambil dari database, bukan dari UI.</summary>
    public async Task<Pesanan> BuatAsync(Guid eventId, IReadOnlyDictionary<int, int> qtyPerItem, string catatan = "")
    {
        var user = Sesi.Wajib;
        if (user.Peran != Peran.Pemesan)
            throw new AksesDitolakException("Hanya akun pemesan yang bisa membuat pesanan.");

        await using var db = new JastipDbContext();
        await using var trx = await db.Database.BeginTransactionAsync();

        var ev = await db.Events.Include(e => e.Items).FirstOrDefaultAsync(e => e.Id == eventId)
                 ?? throw new AppException("Event tidak ditemukan. Mungkin sudah dihapus.");

        if (ev.Deadline <= DateTime.Now) throw new AppException("Pemesanan untuk event ini sudah ditutup.");
        if (ev.Terisi >= ev.Kuota) throw new AppException("Maaf, kuota event ini sudah penuh.");
        if (await db.Pesanan.AnyAsync(p => p.EventId == eventId && p.PemesanId == user.Id && p.Status != StatusPesanan.Dibatalkan))
            throw new AppException("Kamu sudah punya pesanan di event ini. Ubah pesanan yang ada bila ingin menambah barang.");

        var pesanan = new Pesanan
        {
            Kode = "TMP-" + Guid.NewGuid().ToString("N")[..16],
            EventId = ev.Id,
            PemesanId = user.Id,
            Waktu = DateTime.Now,
            BiayaJasa = ev.BiayaJasa,
            OngkosKirim = ev.OngkosKirim,
            Catatan = (catatan ?? "").Trim(),
            Detail = BuatDetail(ev, qtyPerItem)
        };

        db.Pesanan.Add(pesanan);
        ev.Terisi++;                     // satu pesanan = satu slot kuota
        await db.SaveChangesAsync();

        pesanan.Kode = $"JT-{pesanan.Waktu:yyMMdd}-{pesanan.Id:D4}";
        AktivitasLog.Catat(db, user, "Tambah", "Pesanan", pesanan.Kode,
            $"{user.Nama} memesan di \"{ev.Judul}\" ({pesanan.Kode})", ev.PenyediaId, user.Id);
        await db.SaveChangesAsync();
        await trx.CommitAsync();
        return pesanan;
    }

    /// <summary>Pemesan mengubah barang pada pesanannya selama status masih Diterima dan event belum ditutup.</summary>
    public async Task UbahIsiAsync(int pesananId, IReadOnlyDictionary<int, int> qtyPerItem)
    {
        var user = Sesi.Wajib;
        await using var db = new JastipDbContext();
        await using var trx = await db.Database.BeginTransactionAsync();

        var p = await db.Pesanan.Include(x => x.Detail).Include(x => x.Pembayaran)
                    .Include(x => x.Event).ThenInclude(e => e.Items)
                    .FirstOrDefaultAsync(x => x.Id == pesananId)
                ?? throw new AppException("Pesanan tidak ditemukan.");

        if (user.Peran != Peran.Pemesan || p.PemesanId != user.Id)
            throw new AksesDitolakException("Kamu hanya bisa mengubah pesananmu sendiri.");
        if (p.Status != StatusPesanan.Diterima)
            throw new AppException("Pesanan yang sudah diproses penyedia tidak bisa diubah. Hubungi penyedia.");
        if (p.Event.Deadline <= DateTime.Now)
            throw new AppException("Pemesanan sudah ditutup, pesanan tidak bisa diubah lagi.");

        var detailBaru = BuatDetail(p.Event, qtyPerItem);
        var totalBaru = detailBaru.Sum(d => d.Harga * d.Qty) + p.BiayaJasa + p.OngkosKirim;
        if (totalBaru < p.TotalDibayar)
            throw new AppException($"Total baru ({Fmt.Rp(totalBaru)}) lebih kecil dari yang sudah dibayar ({Fmt.Rp(p.TotalDibayar)}). Hubungi penyedia.");

        p.Detail.Clear();
        p.Detail.AddRange(detailBaru);
        AktivitasLog.Catat(db, user, "Ubah", "Pesanan", p.Kode,
            $"{user.Nama} mengubah isi pesanan {p.Kode}", p.Event.PenyediaId, user.Id);
        await db.SaveChangesAsync();
        await trx.CommitAsync();
    }

    private static List<PesananDetail> BuatDetail(JastipEvent ev, IReadOnlyDictionary<int, int> qtyPerItem)
    {
        var dipilih = qtyPerItem.Where(x => x.Value > 0).ToList();
        if (dipilih.Count == 0) throw new AppException("Pilih minimal satu barang dulu.");
        if (dipilih.Any(x => x.Value > 99)) throw new AppException("Jumlah per barang maksimal 99.");

        return dipilih.Select(x =>
        {
            var item = ev.Items.FirstOrDefault(i => i.Id == x.Key)
                       ?? throw new AppException("Ada barang yang sudah tidak tersedia. Muat ulang lalu coba lagi.");
            return new PesananDetail { NamaBarang = item.Nama, Harga = item.Harga, Qty = x.Value };
        }).ToList();
    }

    // =====================================================================
    //  Status, batal, hapus
    // =====================================================================

    /// <summary>
    /// Membatalkan pesanan dan mengembalikan slot kuota.
    /// Pemesan: hanya pesanannya sendiri, status Diterima, sebelum deadline.
    /// Penyedia pemilik / admin: selama belum dikirim.
    /// </summary>
    public async Task BatalkanAsync(int pesananId)
    {
        var user = Sesi.Wajib;
        await using var db = new JastipDbContext();
        await using var trx = await db.Database.BeginTransactionAsync();

        var p = await db.Pesanan.Include(x => x.Event).FirstOrDefaultAsync(x => x.Id == pesananId)
                ?? throw new AppException("Pesanan tidak ditemukan.");

        var milikSendiri = user.Peran == Peran.Pemesan && p.PemesanId == user.Id;
        if (!milikSendiri && !EventService.BisaKelola(user, p.Event))
            throw new AksesDitolakException("Kamu tidak berhak membatalkan pesanan ini.");
        if (p.Status == StatusPesanan.Dibatalkan) throw new AppException("Pesanan ini sudah dibatalkan.");
        if (!p.Status.BisaDibatalkan()) throw new AppException("Pesanan yang sudah dikirim tidak bisa dibatalkan.");
        if (milikSendiri && (p.Status != StatusPesanan.Diterima || p.Event.Deadline <= DateTime.Now))
            throw new AppException("Pesanan sudah diproses atau pemesanan sudah ditutup. Hubungi penyedia untuk pembatalan.");

        p.Status = StatusPesanan.Dibatalkan;
        if (p.Event.Terisi > 0) p.Event.Terisi--;
        AktivitasLog.Catat(db, user, "Batal", "Pesanan", p.Kode,
            $"{user.Nama} membatalkan pesanan {p.Kode}", p.Event.PenyediaId, p.PemesanId);
        await db.SaveChangesAsync();
        await trx.CommitAsync();
    }

    /// <summary>Memajukan status satu langkah (Diterima → Dibeli → Dikirim → Selesai). Penyedia pemilik / admin.</summary>
    public async Task MajukanStatusAsync(int pesananId)
    {
        var user = Sesi.Wajib;
        await using var db = new JastipDbContext();
        var p = await db.Pesanan.Include(x => x.Event).Include(x => x.Detail).Include(x => x.Pembayaran)
                    .FirstOrDefaultAsync(x => x.Id == pesananId)
                ?? throw new AppException("Pesanan tidak ditemukan.");

        if (!EventService.BisaKelola(user, p.Event))
            throw new AksesDitolakException("Hanya penyedia pemilik event atau admin yang bisa mengubah status.");
        var berikut = p.Status.Berikutnya()
                      ?? throw new AppException($"Status \"{p.Status.Teks()}\" sudah tidak bisa dimajukan.");
        if (berikut == StatusPesanan.Selesai && !p.IsLunas)
            throw new AppException($"Pesanan belum lunas (sisa {Fmt.Rp(p.SisaTagihan)}). Catat pelunasan dulu sebelum menandai Selesai.");

        p.Status = berikut;
        AktivitasLog.Catat(db, user, "Status", "Pesanan", p.Kode,
            $"Status {p.Kode} menjadi \"{berikut.Teks()}\"", p.Event.PenyediaId, p.PemesanId);
        await db.SaveChangesAsync();
    }

    /// <summary>Menghapus pesanan permanen (beserta pembayarannya). Hanya admin.</summary>
    public async Task HapusAsync(int pesananId)
    {
        var user = Sesi.Wajib;
        if (user.Peran != Peran.Admin) throw new AksesDitolakException("Hanya admin yang bisa menghapus pesanan.");

        await using var db = new JastipDbContext();
        await using var trx = await db.Database.BeginTransactionAsync();
        var p = await db.Pesanan.Include(x => x.Event).FirstOrDefaultAsync(x => x.Id == pesananId)
                ?? throw new AppException("Pesanan tidak ditemukan.");

        if (p.Status != StatusPesanan.Dibatalkan && p.Event.Terisi > 0) p.Event.Terisi--;
        db.Pesanan.Remove(p);
        AktivitasLog.Catat(db, user, "Hapus", "Pesanan", p.Kode,
            $"{user.Nama} menghapus pesanan {p.Kode}", p.Event.PenyediaId, p.PemesanId);
        await db.SaveChangesAsync();
        await trx.CommitAsync();
    }

    // =====================================================================
    //  Pembayaran
    // =====================================================================

    /// <summary>Mencatat pembayaran (DP/cicilan/pelunasan). Tidak boleh melebihi sisa tagihan.</summary>
    public async Task TambahPembayaranAsync(int pesananId, decimal jumlah, string metode, string catatan)
    {
        var user = Sesi.Wajib;
        if (jumlah <= 0) throw new AppException("Jumlah pembayaran harus lebih dari 0.");

        await using var db = new JastipDbContext();
        await using var trx = await db.Database.BeginTransactionAsync();
        var p = await db.Pesanan.Include(x => x.Event).Include(x => x.Detail).Include(x => x.Pembayaran)
                    .FirstOrDefaultAsync(x => x.Id == pesananId)
                ?? throw new AppException("Pesanan tidak ditemukan.");

        if (!EventService.BisaKelola(user, p.Event))
            throw new AksesDitolakException("Hanya penyedia pemilik event atau admin yang bisa mencatat pembayaran.");
        if (p.Status == StatusPesanan.Dibatalkan) throw new AppException("Pesanan yang dibatalkan tidak bisa menerima pembayaran.");
        if (p.SisaTagihan == 0) throw new AppException("Pesanan ini sudah lunas.");
        if (jumlah > p.SisaTagihan)
            throw new AppException($"Jumlah melebihi sisa tagihan ({Fmt.Rp(p.SisaTagihan)}).");

        p.Pembayaran.Add(new Pembayaran
        {
            Tanggal = DateTime.Now,
            Jumlah = jumlah,
            Metode = string.IsNullOrWhiteSpace(metode) ? "Transfer" : metode.Trim(),
            Catatan = (catatan ?? "").Trim()
        });
        var lunas = p.SisaTagihan == 0 ? " (lunas)" : "";
        AktivitasLog.Catat(db, user, "Bayar", "Pembayaran", p.Kode,
            $"Pembayaran {Fmt.Rp(jumlah)} untuk {p.Kode}{lunas}", p.Event.PenyediaId, p.PemesanId);
        await db.SaveChangesAsync();
        await trx.CommitAsync();
    }

    /// <summary>Menghapus catatan pembayaran yang salah input. Penyedia pemilik / admin.</summary>
    public async Task HapusPembayaranAsync(int pembayaranId)
    {
        var user = Sesi.Wajib;
        await using var db = new JastipDbContext();
        var bayar = await db.Pembayaran.FirstOrDefaultAsync(x => x.Id == pembayaranId)
                    ?? throw new AppException("Pembayaran tidak ditemukan.");
        var p = await db.Pesanan.Include(x => x.Event).FirstAsync(x => x.Id == bayar.PesananId);

        if (!EventService.BisaKelola(user, p.Event))
            throw new AksesDitolakException("Hanya penyedia pemilik event atau admin yang bisa menghapus pembayaran.");

        db.Pembayaran.Remove(bayar);
        AktivitasLog.Catat(db, user, "Hapus", "Pembayaran", p.Kode,
            $"Pembayaran {Fmt.Rp(bayar.Jumlah)} pada {p.Kode} dihapus", p.Event.PenyediaId, p.PemesanId);
        await db.SaveChangesAsync();
    }
}
