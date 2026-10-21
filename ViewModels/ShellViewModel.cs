using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JastipApp.Helpers;
using JastipApp.Models;
using JastipApp.Services;

namespace JastipApp.ViewModels;

/// <summary>Satu menu di sidebar.</summary>
public record MenuNav(string Judul, string Keterangan, Func<object> BuatHalaman);

/// <summary>
/// Kerangka aplikasi (MainWindow): sidebar, halaman aktif, notifikasi, tema, dan logout.
/// Menu yang tampil tergantung peran; halaman dibuat ulang setiap dibuka agar datanya segar.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly DispatcherTimer _toastTimer;

    /// <summary>MainWindow menanggapi dengan kembali ke jendela login.</summary>
    public event Action? KeluarDiminta;

    public ShellViewModel()
    {
        User = Sesi.Wajib;

        Menu = new List<MenuNav>
        {
            new("Dashboard", "Ringkasan & laporan", () => new DashboardViewModel()),
            new("Event", "Event jastip", () => new EventPageViewModel()),
            new(User.Peran == Peran.Pemesan ? "Pesanan saya" : "Pesanan", "Status & pembayaran", () => new PesananPageViewModel()),
        };
        if (User.Peran == Peran.Penyedia) Menu.Add(new("Pelanggan", "Data pemesan", () => new PelangganPageViewModel()));
        if (User.Peran == Peran.Admin) Menu.Add(new("Pengguna", "Akun & akses", () => new PelangganPageViewModel()));
        Menu.Add(new("Pengaturan", "Tema, profil, cadangan", () => new PengaturanViewModel()));

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); Notifikasi = ""; };

        UiBus.NotifDiminta += TampilkanNotif;
        UiBus.LogoutDiminta += Keluar;

        _temaGelap = PengaturanService.TemaGelap;
        PengaturanService.TemaBerubah += SinkronTema;
        MenuTerpilih = Menu[0];
    }

    public Pengguna User { get; }
    public string UserText => User.Nama;
    public string PeranText => User.Peran switch
    {
        Peran.Admin => "Admin",
        Peran.Penyedia => "Penyedia jasa",
        _ => "Pemesan"
    };
    public string Inisial => string.Concat(User.Nama.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                 .Take(2).Select(k => char.ToUpperInvariant(k[0])));

    public List<MenuNav> Menu { get; }

    [ObservableProperty] private MenuNav? _menuTerpilih;
    [ObservableProperty] private object? _halamanAktif;

    partial void OnMenuTerpilihChanged(MenuNav? value)
    {
        if (value is null) return;
        (HalamanAktif as EventPageViewModel)?.Berhenti();
        HalamanAktif = value.BuatHalaman();
    }

    // ---- Tema (sama dengan pengaturan di halaman Pengaturan) ----
    [ObservableProperty] private bool _temaGelap;
    partial void OnTemaGelapChanged(bool value) => PengaturanService.SetTemaGelap(value);
    private void SinkronTema() => TemaGelap = PengaturanService.TemaGelap;

    // ---- Notifikasi ----
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNotifikasi))]
    private string _notifikasi = "";
    [ObservableProperty] private bool _notifikasiError;
    public bool ShowNotifikasi => Notifikasi.Length > 0;

    private void TampilkanNotif(string pesan, bool error)
    {
        NotifikasiError = error;
        Notifikasi = pesan;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    [RelayCommand]
    private void TutupNotif() => Notifikasi = "";

    [RelayCommand]
    private void Keluar()
    {
        UiBus.NotifDiminta -= TampilkanNotif;
        UiBus.LogoutDiminta -= Keluar;
        PengaturanService.TemaBerubah -= SinkronTema;
        _toastTimer.Stop();
        (HalamanAktif as EventPageViewModel)?.Berhenti();
        Sesi.Keluar();
        KeluarDiminta?.Invoke();
    }
}
