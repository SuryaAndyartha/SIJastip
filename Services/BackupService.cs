using JastipApp.Data;
using JastipApp.Models;
using Microsoft.Data.Sqlite;

namespace JastipApp.Services;

/// <summary>
/// Cadangan & pemulihan file database (hanya admin). Memakai SQLite Online Backup API,
/// sehingga aman walau database sedang terbuka.
/// </summary>
public class BackupService
{
    public static string FolderCadangan => Path.Combine(JastipDbContext.Folder, "backup");

    private static void WajibAdmin()
    {
        if (Sesi.Wajib.Peran != Peran.Admin)
            throw new AksesDitolakException("Hanya admin yang bisa mencadangkan atau memulihkan database.");
    }

    public List<FileInfo> Daftar()
    {
        WajibAdmin();
        Directory.CreateDirectory(FolderCadangan);
        return new DirectoryInfo(FolderCadangan).GetFiles("*.db")
            .OrderByDescending(f => f.LastWriteTime).ToList();
    }

    public Task<string> CadangkanAsync() => Task.Run(() =>
    {
        WajibAdmin();
        return SalinKeCadangan("cadangan");
    });

    /// <summary>
    /// Memulihkan database dari file cadangan. Database saat ini dicadangkan otomatis dulu,
    /// jadi pemulihan yang salah pun masih bisa dibatalkan.
    /// </summary>
    public Task PulihkanAsync(string pathCadangan) => Task.Run(() =>
    {
        WajibAdmin();
        if (!File.Exists(pathCadangan)) throw new AppException("File cadangan tidak ditemukan.");

        int versi;
        try
        {
            using var cek = new SqliteConnection($"Data Source={pathCadangan};Mode=ReadOnly");
            cek.Open();
            using var cmd = cek.CreateCommand();
            cmd.CommandText = "PRAGMA user_version;";
            versi = Convert.ToInt32(cmd.ExecuteScalar());
        }
        catch (SqliteException)
        {
            throw new AppException("File tersebut bukan database SIJastip yang valid.");
        }
        if (versi != DbInit.VersiSkema)
            throw new AppException($"Versi cadangan ({versi}) tidak cocok dengan versi aplikasi ({DbInit.VersiSkema}).");

        SalinKeCadangan("sebelum-pulih");

        SqliteConnection.ClearAllPools();
        using (var sumber = new SqliteConnection($"Data Source={pathCadangan};Mode=ReadOnly"))
        using (var tujuan = new SqliteConnection($"Data Source={JastipDbContext.DbPath}"))
        {
            sumber.Open();
            tujuan.Open();
            sumber.BackupDatabase(tujuan);
        }
        SqliteConnection.ClearAllPools();
    });

    private static string SalinKeCadangan(string awalan)
    {
        Directory.CreateDirectory(FolderCadangan);
        var tujuanPath = Path.Combine(FolderCadangan, $"{awalan}-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        using var sumber = new SqliteConnection($"Data Source={JastipDbContext.DbPath}");
        using var tujuan = new SqliteConnection($"Data Source={tujuanPath}");
        sumber.Open();
        tujuan.Open();
        sumber.BackupDatabase(tujuan);
        SqliteConnection.ClearPool(tujuan);
        return tujuanPath;
    }
}
