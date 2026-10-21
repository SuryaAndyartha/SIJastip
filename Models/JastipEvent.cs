namespace JastipApp.Models;

public enum KategoriJastip
{
    Makanan,
    Barang
}

/// <summary>Event jastip yang dibuka oleh seorang penyedia jasa.</summary>
public class JastipEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Judul { get; set; } = "";

    // Penyedia sekarang adalah akun pengguna (bukan teks bebas), supaya hak akses bisa dicek.
    public int PenyediaId { get; set; }
    public Pengguna Penyedia { get; set; } = null!;

    public KategoriJastip Kategori { get; set; }
    public string LokasiBelanja { get; set; } = "";
    public string Deskripsi { get; set; } = "";
    public string Kontak { get; set; } = "";

    public DateTime Deadline { get; set; }          // batas akhir pemesanan
    public DateTime EstimasiSampai { get; set; }    // perkiraan barang tiba di pemesan

    public decimal OngkosKirim { get; set; }
    public decimal BiayaJasa { get; set; }          // fee penyedia per pesanan

    public int Kuota { get; set; }
    public int Terisi { get; set; }                 // jumlah pesanan aktif; diubah otomatis oleh PesananService

    public List<JastipItem> Items { get; set; } = new();
}
