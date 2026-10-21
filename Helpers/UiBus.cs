namespace JastipApp.Helpers;

/// <summary>
/// Saluran sederhana dari halaman ke shell: menampilkan notifikasi (toast) dan meminta logout.
/// Dengan ini halaman tidak perlu tahu tentang MainWindow.
/// </summary>
public static class UiBus
{
    public static event Action<string, bool>? NotifDiminta;
    public static event Action? LogoutDiminta;

    public static void Info(string pesan) => NotifDiminta?.Invoke(pesan, false);
    public static void Error(string pesan) => NotifDiminta?.Invoke(pesan, true);
    public static void Logout() => LogoutDiminta?.Invoke();

    /// <summary>Pesan untuk pengguna. Error teknis dicatat ke error.log, bukan ditampilkan mentah.</summary>
    public static string PesanError(Exception ex, string konteks)
    {
        if (ex is Services.AppException) return ex.Message;
        Log.Error(konteks, ex);
        return "Terjadi kesalahan pada database. Coba lagi; detailnya dicatat di error.log.";
    }
}
