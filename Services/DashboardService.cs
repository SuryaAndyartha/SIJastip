using JastipApp.Data;
using JastipApp.Models;
using Microsoft.EntityFrameworkCore;

namespace JastipApp.Services;

public record TitikGrafik(string Label, decimal Nilai);

public class DashboardData
{
    public int TotalPesanan { get; init; }
    public int PesananBerjalan { get; init; }
    public int PesananSelesai { get; init; }
    public decimal Omzet { get; init; }          // total tagihan pesanan yang tidak dibatalkan
    public decimal Terbayar { get; init; }
    public decimal Piutang { get; init; }        // sisa tagihan yang belum dibayar
    public decimal Keuntungan { get; init; }     // biaya jasa = pendapatan penyedia
    public int EventDibuka { get; init; }
    public List<TitikGrafik> PerHari { get; init; } = new();
    public List<TitikGrafik> PerStatus { get; init; } = new();
    public List<Aktivitas> AktivitasTerbaru { get; init; } = new();
}

/// <summary>Menghitung statistik dashboard/laporan dari database sesuai hak akses pengguna yang login.</summary>
public class DashboardService
{
    private readonly PesananService _pesanan = new();

    /// <param name="hari">Periode dalam hari ke belakang (mis. 7 atau 30); null = semua data.</param>
    public async Task<DashboardData> HitungAsync(int? hari)
    {
        var user = Sesi.Wajib;
        var semua = await _pesanan.DaftarAsync();   // sudah tersaring sesuai peran
        var mulai = hari is null ? DateTime.MinValue : DateTime.Today.AddDays(-(hari.Value - 1));
        var periode = semua.Where(p => p.Waktu >= mulai).ToList();
        var sah = periode.Where(p => p.Status != StatusPesanan.Dibatalkan).ToList();

        await using var db = new JastipDbContext();
        var now = DateTime.Now;
        var eventQ = db.Events.AsNoTracking().Where(e => e.Deadline > now && e.Terisi < e.Kuota);
        if (user.Peran == Peran.Penyedia) eventQ = eventQ.Where(e => e.PenyediaId == user.Id);
        var eventDibuka = await eventQ.CountAsync();

        // Grafik per hari: maksimal 14 batang agar tetap terbaca.
        var jumlahHari = Math.Min(hari ?? 14, 14);
        var perHari = Enumerable.Range(0, jumlahHari)
            .Select(i => DateTime.Today.AddDays(-(jumlahHari - 1 - i)))
            .Select(tgl => new TitikGrafik(
                tgl.ToString("d MMM", Helpers.Fmt.Id),
                sah.Where(p => p.Waktu.Date == tgl).Sum(p => p.Total)))
            .ToList();

        var perStatus = Enum.GetValues<StatusPesanan>()
            .Select(s => new TitikGrafik(s.Teks(), periode.Count(p => p.Status == s)))
            .ToList();

        return new DashboardData
        {
            TotalPesanan = sah.Count,
            PesananBerjalan = sah.Count(p => p.Status.MasihBerjalan()),
            PesananSelesai = sah.Count(p => p.Status == StatusPesanan.Selesai),
            Omzet = sah.Sum(p => p.Total),
            Terbayar = sah.Sum(p => p.TotalDibayar),
            Piutang = sah.Sum(p => p.SisaTagihan),
            Keuntungan = sah.Sum(p => p.BiayaJasa),
            EventDibuka = eventDibuka,
            PerHari = perHari,
            PerStatus = perStatus,
            AktivitasTerbaru = await AktivitasAsync(db, user, 8)
        };
    }

    /// <summary>Aktivitas yang relevan untuk pengguna: admin semua; penyedia terkait event miliknya; pemesan miliknya.</summary>
    private static async Task<List<Aktivitas>> AktivitasAsync(JastipDbContext db, Pengguna user, int jumlah)
    {
        var q = db.Aktivitas.AsNoTracking();
        q = user.Peran switch
        {
            Peran.Penyedia => q.Where(a => a.PenyediaTerkaitId == user.Id || a.PenggunaId == user.Id),
            Peran.Pemesan => q.Where(a => a.PemesanTerkaitId == user.Id || a.PenggunaId == user.Id),
            _ => q
        };
        return await q.OrderByDescending(a => a.Waktu).Take(jumlah).ToListAsync();
    }
}
