using System.ComponentModel.DataAnnotations.Schema;

namespace JastipApp.Models;

/// <summary>Alur status pesanan: Diterima → Dibeli → Dikirim → Selesai. Dibatalkan bisa terjadi sebelum Dikirim.</summary>
public enum StatusPesanan
{
    Diterima,
    Dibeli,
    Dikirim,
    Selesai,
    Dibatalkan
}

public static class StatusPesananExt
{
    public static string Teks(this StatusPesanan s) => s switch
    {
        StatusPesanan.Diterima => "Diterima",
        StatusPesanan.Dibeli => "Sudah dibeli",
        StatusPesanan.Dikirim => "Dikirim",
        StatusPesanan.Selesai => "Selesai",
        _ => "Dibatalkan"
    };

    /// <summary>Status berikutnya dalam alur, atau null bila sudah di ujung.</summary>
    public static StatusPesanan? Berikutnya(this StatusPesanan s) => s switch
    {
        StatusPesanan.Diterima => StatusPesanan.Dibeli,
        StatusPesanan.Dibeli => StatusPesanan.Dikirim,
        StatusPesanan.Dikirim => StatusPesanan.Selesai,
        _ => null
    };

    /// <summary>Pesanan yang masih berjalan (belum selesai dan tidak dibatalkan).</summary>
    public static bool MasihBerjalan(this StatusPesanan s) =>
        s is StatusPesanan.Diterima or StatusPesanan.Dibeli or StatusPesanan.Dikirim;

    /// <summary>Pembatalan hanya boleh sebelum barang dikirim.</summary>
    public static bool BisaDibatalkan(this StatusPesanan s) =>
        s is StatusPesanan.Diterima or StatusPesanan.Dibeli;
}

/// <summary>
/// Pesanan seorang pemesan pada satu event. Satu pesanan memakai satu slot kuota.
/// Harga, biaya jasa, dan ongkir disalin saat memesan, jadi tidak berubah walau event diedit.
/// </summary>
public class Pesanan
{
    public int Id { get; set; }

    /// <summary>Kode unik yang bisa dicari, mis. JT-261021-0012.</summary>
    public string Kode { get; set; } = "";

    public Guid EventId { get; set; }
    public JastipEvent Event { get; set; } = null!;

    public int PemesanId { get; set; }
    public Pengguna Pemesan { get; set; } = null!;

    public DateTime Waktu { get; set; }
    public StatusPesanan Status { get; set; } = StatusPesanan.Diterima;

    public decimal BiayaJasa { get; set; }
    public decimal OngkosKirim { get; set; }
    public string Catatan { get; set; } = "";

    public List<PesananDetail> Detail { get; set; } = new();
    public List<Pembayaran> Pembayaran { get; set; } = new();

    [NotMapped] public decimal TotalBarang => Detail.Sum(d => d.Harga * d.Qty);
    [NotMapped] public decimal Total => TotalBarang + BiayaJasa + OngkosKirim;
    [NotMapped] public decimal TotalDibayar => Pembayaran.Sum(p => p.Jumlah);
    [NotMapped] public decimal SisaTagihan =>
        Status == StatusPesanan.Dibatalkan ? 0 : Math.Max(0, Total - TotalDibayar);
    [NotMapped] public bool IsLunas => Status != StatusPesanan.Dibatalkan && SisaTagihan == 0;
}

/// <summary>Satu baris barang dalam pesanan.</summary>
public class PesananDetail
{
    public int Id { get; set; }
    public int PesananId { get; set; }
    public string NamaBarang { get; set; } = "";
    public decimal Harga { get; set; }
    public int Qty { get; set; }
}

/// <summary>Pembayaran (DP, cicilan, atau pelunasan) untuk satu pesanan.</summary>
public class Pembayaran
{
    public int Id { get; set; }
    public int PesananId { get; set; }
    public DateTime Tanggal { get; set; }
    public decimal Jumlah { get; set; }
    public string Metode { get; set; } = "Transfer";
    public string Catatan { get; set; } = "";
}
