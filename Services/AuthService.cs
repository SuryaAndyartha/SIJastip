using JastipApp.Data;
using JastipApp.Helpers;
using JastipApp.Models;
using Microsoft.EntityFrameworkCore;

namespace JastipApp.Services;

/// <summary>Login, pendaftaran akun, dan logout.</summary>
public class AuthService
{
    public async Task<Pengguna> LoginAsync(string username, string password)
    {
        var u = (username ?? "").Trim().ToLowerInvariant();
        if (u.Length == 0 || string.IsNullOrEmpty(password))
            throw new AppException("Username dan password wajib diisi.");

        await using var db = new JastipDbContext();
        var akun = await db.Pengguna.AsNoTracking().FirstOrDefaultAsync(p => p.Username == u);

        // Pesan sengaja sama untuk username tidak ada / password salah.
        if (akun is null || !PasswordHasher.Verifikasi(password, akun.PasswordHash))
            throw new AppException("Username atau password salah.");
        if (!akun.IsActive)
            throw new AppException("Akun ini sedang dinonaktifkan. Hubungi admin.");

        Sesi.Masuk(akun);
        return akun;
    }

    public async Task<Pengguna> DaftarAsync(string nama, string username, string password,
                                            string konfirmasi, Peran peran, string kontak)
    {
        nama = (nama ?? "").Trim();
        var u = (username ?? "").Trim().ToLowerInvariant();
        kontak = (kontak ?? "").Trim();

        if (nama.Length == 0) throw new AppException("Nama wajib diisi.");
        if (nama.Length > 60) throw new AppException("Nama maksimal 60 karakter.");
        if (u.Length < 3 || u.Length > 30 || !u.All(c => char.IsAsciiLetterOrDigit(c) || c == '_' || c == '.'))
            throw new AppException("Username 3–30 karakter, hanya huruf, angka, titik, atau garis bawah.");
        if ((password ?? "").Length < 6) throw new AppException("Password minimal 6 karakter.");
        if (password != konfirmasi) throw new AppException("Konfirmasi password tidak sama.");
        if (peran == Peran.Admin) throw new AksesDitolakException("Akun admin tidak bisa didaftarkan dari sini.");
        if (peran == Peran.Penyedia && kontak.Length == 0)
            throw new AppException("Penyedia jasa wajib mengisi kontak WhatsApp.");

        await using var db = new JastipDbContext();
        if (await db.Pengguna.AnyAsync(p => p.Username == u))
            throw new AppException($"Username \"{u}\" sudah dipakai.");

        var akun = new Pengguna
        {
            Nama = nama,
            Username = u,
            PasswordHash = PasswordHasher.Hash(password!),
            Peran = peran,
            Kontak = kontak
        };
        db.Pengguna.Add(akun);
        await db.SaveChangesAsync();
        AktivitasLog.Catat(db, akun, "Akun", "Pengguna", akun.Id.ToString(),
            $"{akun.Nama} mendaftar sebagai {(peran == Peran.Penyedia ? "penyedia" : "pemesan")}", null, null);
        await db.SaveChangesAsync();

        Sesi.Masuk(akun);
        return akun;
    }

    public void Logout() => Sesi.Keluar();
}
