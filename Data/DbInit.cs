using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JastipApp.Data;

/// <summary>
/// Menyiapkan database saat aplikasi dibuka.
/// EnsureCreated() tidak bisa menambah tabel ke database yang sudah ada, jadi versi skema
/// disimpan di PRAGMA user_version. Bila file lama berversi lebih rendah, file itu
/// dipindahkan menjadi cadangan (bukan dihapus) lalu database baru dibuat dan diisi data contoh.
/// </summary>
public static class DbInit
{
    public const int VersiSkema = 3;   // naikkan setiap kali struktur Model berubah

    public static void Siapkan()
    {
        Directory.CreateDirectory(JastipDbContext.Folder);
        var path = JastipDbContext.DbPath;

        if (File.Exists(path) && BacaVersi(path) < VersiSkema)
        {
            SqliteConnection.ClearAllPools();
            var cadangan = Path.Combine(JastipDbContext.Folder,
                $"jastip-lama-{DateTime.Now:yyyyMMdd-HHmmss}.db");
            File.Move(path, cadangan);
        }

        using var db = new JastipDbContext();
        if (db.Database.EnsureCreated())
        {
            db.Database.ExecuteSqlRaw("PRAGMA user_version = " + VersiSkema + ";");
            SampleData.Isi(db);
        }
    }

    private static int BacaVersi(string path)
    {
        using var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
