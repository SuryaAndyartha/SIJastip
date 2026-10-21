using JastipApp.Data;
using JastipApp.Helpers;
using JastipApp.Models;
using Microsoft.EntityFrameworkCore;

namespace JastipApp.Services;

/// <summary>Satu baris di halaman Pelanggan/Pengguna.</summary>
public record PelangganRow(Pengguna Pengguna, int JumlahPesanan, decimal TotalBelanja, decimal SisaTagihan, DateTime? TerakhirPesan);

/// <summary>Data pelanggan & akun: daftar pelanggan, aktif/nonaktif akun, profil, dan ganti password.</summary>
public class PenggunaService
{
    /// <summary>
    /// Penyedia: pemesan yang pernah memesan di event miliknya (statistik hanya dari event miliknya).
    /// Admin: semua akun, statistik dari pesanan yang ia buat sebagai pemesan.
    /// </summary>
    public async Task<List<PelangganRow>> DaftarAsync()
    {
        var user = Sesi.Wajib;
        if (user.Peran == Peran.Pemesan) throw new AksesDitolakException("Halaman ini untuk penyedia dan admin.");

        await using var db = new JastipDbContext();
        var pesananQ = db.Pesanan.AsNoTracking().AsSplitQuery()
            .Include(p => p.Detail).Include(p => p.Pembayaran)
            .Where(p => p.Status != StatusPesanan.Dibatalkan);
        if (user.Peran == Peran.Penyedia) pesananQ = pesananQ.Where(p => p.Event.PenyediaId == user.Id);
        var pesanan = await pesananQ.ToListAsync();
        var perPemesan = pesanan.GroupBy(p => p.PemesanId).ToDictionary(g => g.Key, g => g.ToList());

        var akunQ = db.Pengguna.AsNoTracking();
        if (user.Peran == Peran.Penyedia)
        {
            var ids = perPemesan.Keys.ToList();
            akunQ = akunQ.Where(u => ids.Contains(u.Id));
        }
        var akun = await akunQ.OrderBy(u => u.Peran).ThenBy(u => u.Nama).ToListAsync();

        return akun.Select(u =>
        {
            var list = perPemesan.GetValueOrDefault(u.Id) ?? new List<Pesanan>();
            return new PelangganRow(u, list.Count, list.Sum(p => p.Total), list.Sum(p => p.SisaTagihan),
                                    list.Count == 0 ? null : list.Max(p => p.Waktu));
        }).ToList();
    }

    /// <summary>Mengaktifkan/menonaktifkan akun. Hanya admin, dan tidak bisa untuk akunnya sendiri.</summary>
    public async Task SetAktifAsync(int penggunaId, bool aktif)
    {
        var user = Sesi.Wajib;
        if (user.Peran != Peran.Admin) throw new AksesDitolakException("Hanya admin yang bisa mengubah status akun.");
        if (penggunaId == user.Id) throw new AppException("Kamu tidak bisa menonaktifkan akunmu sendiri.");

        await using var db = new JastipDbContext();
        var akun = await db.Pengguna.FirstOrDefaultAsync(u => u.Id == penggunaId)
                   ?? throw new AppException("Akun tidak ditemukan.");
        akun.IsActive = aktif;
        AktivitasLog.Catat(db, user, "Akun", "Pengguna", akun.Id.ToString(),
            $"Akun {akun.Nama} (@{akun.Username}) {(aktif ? "diaktifkan" : "dinonaktifkan")}");
        await db.SaveChangesAsync();
    }

    public async Task UbahProfilAsync(string nama, string kontak, string alamat)
    {
        var user = Sesi.Wajib;
        nama = (nama ?? "").Trim();
        if (nama.Length == 0) throw new AppException("Nama wajib diisi.");
        if (nama.Length > 60) throw new AppException("Nama maksimal 60 karakter.");
        if (user.Peran == Peran.Penyedia && string.IsNullOrWhiteSpace(kontak))
            throw new AppException("Penyedia jasa wajib mengisi kontak WhatsApp.");

        await using var db = new JastipDbContext();
        var akun = await db.Pengguna.FirstAsync(u => u.Id == user.Id);
        akun.Nama = nama;
        akun.Kontak = (kontak ?? "").Trim();
        akun.Alamat = (alamat ?? "").Trim();
        await db.SaveChangesAsync();
        Sesi.Masuk(akun);   // perbarui data sesi
    }

    public async Task GantiPasswordAsync(string lama, string baru, string konfirmasi)
    {
        var user = Sesi.Wajib;
        if ((baru ?? "").Length < 6) throw new AppException("Password baru minimal 6 karakter.");
        if (baru != konfirmasi) throw new AppException("Konfirmasi password baru tidak sama.");

        await using var db = new JastipDbContext();
        var akun = await db.Pengguna.FirstAsync(u => u.Id == user.Id);
        if (!PasswordHasher.Verifikasi(lama ?? "", akun.PasswordHash))
            throw new AppException("Password lama salah.");
        akun.PasswordHash = PasswordHasher.Hash(baru!);
        AktivitasLog.Catat(db, user, "Akun", "Pengguna", akun.Id.ToString(), $"{akun.Nama} mengganti password");
        await db.SaveChangesAsync();
    }
}
