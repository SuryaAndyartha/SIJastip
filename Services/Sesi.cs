using JastipApp.Models;

namespace JastipApp.Services;

/// <summary>Menyimpan pengguna yang sedang login selama aplikasi berjalan.</summary>
public static class Sesi
{
    public static Pengguna? Pengguna { get; private set; }

    public static void Masuk(Pengguna p) => Pengguna = p;
    public static void Keluar() => Pengguna = null;

    /// <summary>Pengguna aktif, atau error bila belum login.</summary>
    public static Pengguna Wajib =>
        Pengguna ?? throw new AksesDitolakException("Sesi berakhir. Silakan login kembali.");
}
