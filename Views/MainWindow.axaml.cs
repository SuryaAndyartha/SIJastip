using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using JastipApp.ViewModels;

namespace JastipApp.Views;

/// <summary>Kerangka aplikasi: sidebar + halaman aktif. Isi halaman ditentukan DataTemplates di App.axaml.</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new ShellViewModel();
        vm.KeluarDiminta += KembaliKeLogin;
        DataContext = vm;
    }

    // Logout: buka lagi jendela login, lalu tutup jendela ini.
    private void KembaliKeLogin()
    {
        var login = new LoginWindow();
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = login;
        login.Show();
        Close();
    }
}
