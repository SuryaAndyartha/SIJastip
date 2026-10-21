using JastipApp.Data;
using JastipApp.Models;
using Microsoft.EntityFrameworkCore;

namespace JastipApp.Services;

public record BarangInput(string Nama, decimal Harga, string Catatan);

/// <summary>Data dari form event (buat baru maupun edit).</summary>
public class EventInput
{
    public string Judul { get; init; } = "";
    public KategoriJastip Kategori { get; init; }
    public string Lokasi { get; init; } = "";
    public string Deskripsi { get; init; } = "";
    public string Kontak { get; init; } = "";
    public DateTime Deadline { get; init; }
    public DateTime Estimasi { get; init; }
    public decimal Ongkir { get; init; }
    public decimal Jasa { get; init; }
    public int Kuota { get; init; }
    public List<BarangInput> Barang { get; init; } = new();
}

/// <summary>Aturan dan akses data untuk event jastip. Setiap operasi memakai DbContext sendiri.</summary>
public class EventService
{
    /// <summary>Admin boleh mengelola semua event; penyedia hanya event miliknya.</summary>
    public static bool BisaKelola(Pengguna? u, JastipEvent e) =>
        u is not null && (u.Peran == Peran.Admin || (u.Peran == Peran.Penyedia && e.PenyediaId == u.Id));

    public async Task<List<JastipEvent>> DaftarAsync()
    {
        await using var db = new JastipDbContext();
        var list = await db.Events.AsNoTracking()
            .Include(e => e.Items)
            .Include(e => e.Penyedia)
            .ToListAsync();
        foreach (var e in list) e.Items = e.Items.OrderBy(i => i.Id).ToList();
        return list;
    }

    public async Task<JastipEvent> BuatAsync(EventInput input)
    {
        var user = Sesi.Wajib;
        if (user.Peran != Peran.Penyedia)
            throw new AksesDitolakException("Hanya akun penyedia jasa yang bisa membuat event.");

        Validasi(input, baru: true, terisi: 0);

        await using var db = new JastipDbContext();
        var ev = new JastipEvent { PenyediaId = user.Id };
        Terapkan(ev, input);
        db.Events.Add(ev);
        AktivitasLog.Catat(db, user, "Tambah", "Event", ev.Id.ToString(),
            $"{user.Nama} membuka event \"{ev.Judul}\"", user.Id);
        await db.SaveChangesAsync();
        return ev;
    }

    public async Task UbahAsync(Guid id, EventInput input)
    {
        var user = Sesi.Wajib;
        await using var db = new JastipDbContext();
        var ev = await db.Events.Include(e => e.Items).FirstOrDefaultAsync(e => e.Id == id)
                 ?? throw new AppException("Event tidak ditemukan. Mungkin sudah dihapus.");

        if (!BisaKelola(user, ev)) throw new AksesDitolakException("Kamu hanya bisa mengedit event milikmu sendiri.");

        Validasi(input, baru: false, terisi: ev.Terisi);

        ev.Items.Clear();   // barang lama dihapus (pesanan menyimpan salinan nama & harga sendiri)
        Terapkan(ev, input);
        AktivitasLog.Catat(db, user, "Ubah", "Event", ev.Id.ToString(),
            $"{user.Nama} mengubah event \"{ev.Judul}\"", ev.PenyediaId);
        await db.SaveChangesAsync();
    }

    /// <summary>Menghapus event beserta barang dan semua pesanannya. Mengembalikan jumlah pesanan (tidak termasuk yang dibatalkan) yang ikut terhapus.</summary>
    public async Task<int> HapusAsync(Guid id)
    {
        var user = Sesi.Wajib;
        await using var db = new JastipDbContext();
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == id)
                 ?? throw new AppException("Event tidak ditemukan. Mungkin sudah dihapus.");

        if (!BisaKelola(user, ev)) throw new AksesDitolakException("Kamu hanya bisa menghapus event milikmu sendiri.");

        var aktif = await db.Pesanan.CountAsync(p => p.EventId == id && p.Status != StatusPesanan.Dibatalkan);
        db.Events.Remove(ev);   // barang, pesanan, dan pembayaran ikut terhapus (cascade)
        AktivitasLog.Catat(db, user, "Hapus", "Event", ev.Id.ToString(),
            $"{user.Nama} menghapus event \"{ev.Judul}\" ({aktif} pesanan ikut terhapus)", ev.PenyediaId);
        await db.SaveChangesAsync();
        return aktif;
    }

    private static void Validasi(EventInput i, bool baru, int terisi)
    {
        if (string.IsNullOrWhiteSpace(i.Judul)) throw new AppException("Judul event wajib diisi.");
        if (i.Judul.Trim().Length > 120) throw new AppException("Judul event maksimal 120 karakter.");
        if (string.IsNullOrWhiteSpace(i.Lokasi)) throw new AppException("Lokasi belanja wajib diisi.");
        if (baru && i.Deadline <= DateTime.Now) throw new AppException("Deadline pemesanan harus di masa depan.");
        if (i.Estimasi <= i.Deadline) throw new AppException("Estimasi sampai harus setelah deadline pemesanan.");
        if (i.Ongkir < 0 || i.Jasa < 0) throw new AppException("Ongkos kirim dan biaya jasa tidak boleh negatif.");
        if (i.Kuota < 1) throw new AppException("Kuota minimal 1 slot.");
        if (i.Kuota < terisi)
            throw new AppException($"Kuota tidak boleh kurang dari jumlah pesanan yang sudah masuk ({terisi}).");
        if (i.Barang.Count == 0) throw new AppException("Tambahkan minimal satu barang.");
    }

    private static void Terapkan(JastipEvent ev, EventInput i)
    {
        ev.Judul = i.Judul.Trim();
        ev.Kategori = i.Kategori;
        ev.LokasiBelanja = i.Lokasi.Trim();
        ev.Deskripsi = i.Deskripsi.Trim();
        ev.Kontak = i.Kontak.Trim();
        ev.Deadline = i.Deadline;
        ev.EstimasiSampai = i.Estimasi;
        ev.OngkosKirim = i.Ongkir;
        ev.BiayaJasa = i.Jasa;
        ev.Kuota = i.Kuota;
        foreach (var b in i.Barang)
            ev.Items.Add(new JastipItem { Nama = b.Nama, Harga = b.Harga, Catatan = b.Catatan });
    }
}
