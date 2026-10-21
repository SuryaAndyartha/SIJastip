using JastipApp.Data;

namespace JastipApp.Helpers;

/// <summary>
/// Mencatat error teknis ke file error.log di folder data aplikasi.
/// Pengguna hanya melihat pesan yang ramah; detail teknis (stack trace) masuk ke sini.
/// </summary>
public static class Log
{
    private static readonly object Kunci = new();

    public static void Error(string konteks, Exception ex)
    {
        try
        {
            lock (Kunci)
            {
                Directory.CreateDirectory(JastipDbContext.Folder);
                File.AppendAllText(Path.Combine(JastipDbContext.Folder, "error.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {konteks}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging tidak boleh membuat aplikasi ikut gagal.
        }
    }
}
