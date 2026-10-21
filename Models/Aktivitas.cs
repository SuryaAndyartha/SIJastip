namespace JastipApp.Models;

/// <summary>
/// Catatan aktivitas (audit ringkas): siapa melakukan apa, kapan.
/// PenyediaTerkaitId / PemesanTerkaitId dipakai untuk menyaring aktivitas yang boleh dilihat tiap pengguna.
/// Tidak pernah menyimpan password atau data rahasia.
/// </summary>
public class Aktivitas
{
    public int Id { get; set; }
    public int? PenggunaId { get; set; }
    public string NamaPengguna { get; set; } = "";
    public DateTime Waktu { get; set; }
    public string Jenis { get; set; } = "";      // Tambah, Ubah, Hapus, Status, Bayar, Batal, Akun
    public string Entitas { get; set; } = "";    // Event, Pesanan, Pembayaran, Pengguna, Database
    public string EntitasId { get; set; } = "";
    public string Keterangan { get; set; } = "";
    public int? PenyediaTerkaitId { get; set; }
    public int? PemesanTerkaitId { get; set; }
}
