using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using JastipApp.ViewModels;

namespace JastipApp.Views;

public partial class LoginWindow : Window
{
    public LoginWindow() : this(null) { }

    public LoginWindow(string? errorAwal)
    {
        InitializeComponent();
        var vm = new LoginViewModel(errorAwal);
        vm.Berhasil += BukaAplikasi;
        DataContext = vm;
    }

    // Setelah login berhasil: buka jendela utama, lalu tutup jendela login.
    private void BukaAplikasi()
    {
        var main = new MainWindow();
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = main;
        main.Show();
        Close();
    }
}
