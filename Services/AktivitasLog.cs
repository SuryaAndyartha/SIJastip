using JastipApp.Data;
using JastipApp.Models;

namespace JastipApp.Services;

/// <summary>Menambahkan catatan aktivitas ke DbContext; ikut tersimpan pada SaveChanges yang sama dengan datanya.</summary>
public static class AktivitasLog
{
    public static void Catat(JastipDbContext db, Pengguna? pelaku, string jenis, string entitas, string entitasId,
                             string keterangan, int? penyediaId = null, int? pemesanId = null, DateTime? waktu = null)
    {
        db.Aktivitas.Add(new Aktivitas
        {
            PenggunaId = pelaku?.Id,
            NamaPengguna = pelaku?.Nama ?? "Sistem",
            Waktu = waktu ?? DateTime.Now,
            Jenis = jenis,
            Entitas = entitas,
            EntitasId = entitasId,
            Keterangan = keterangan,
            PenyediaTerkaitId = penyediaId,
            PemesanTerkaitId = pemesanId
        });
    }
}
