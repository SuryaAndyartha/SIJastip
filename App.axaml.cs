using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using JastipApp.Data;
using JastipApp.Helpers;
using JastipApp.Services;
using JastipApp.Views;

namespace JastipApp;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Siapkan database dulu. Bila gagal, aplikasi tetap terbuka dan menampilkan pesan, bukan crash.
            string? error = null;
            try
            {
                DbInit.Siapkan();
            }
            catch (Exception ex)
            {
                Log.Error("Menyiapkan database", ex);
                error = "Database tidak bisa disiapkan. Tutup aplikasi lalu coba lagi. Detail ada di error.log.";
            }

            PengaturanService.Muat();   // terapkan tema terang/gelap tersimpan
            desktop.MainWindow = new LoginWindow(error);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
