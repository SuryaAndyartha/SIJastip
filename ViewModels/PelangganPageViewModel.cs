using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JastipApp.Helpers;
using JastipApp.Models;
using JastipApp.Services;

namespace JastipApp.ViewModels;

public partial class PelangganItem : ObservableObject
{
    private readonly Func<PelangganItem, Task> _toggle;

    public PelangganItem(PelangganRow row, bool bisaUbahAkun, Func<PelangganItem, Task> toggle)
    {
        Row = row;
        BisaUbahAkun = bisaUbahAkun;
        _toggle = toggle;
    }

    public PelangganRow Row { get; }
    public Pengguna Pengguna => Row.Pengguna;
    public string Nama => Pengguna.Nama;
    public string Username => "@" + Pengguna.Username;
    public string Kontak => string.IsNullOrWhiteSpace(Pengguna.Kontak) ? "-" : Pengguna.Kontak;
    public string Alamat => Pengguna.Alamat;
    public bool AdaAlamat => !string.IsNullOrWhiteSpace(Pengguna.Alamat);
    public string PeranText => Pengguna.Peran switch
    {
        Peran.Admin => "Admin",
        Peran.Penyedia => "Penyedia",
        _ => "Pemesan"
    };
    public string JumlahPesanan => Row.JumlahPesanan.ToString();
    public string TotalBelanja => Fmt.Rp(Row.TotalBelanja);
    public string SisaTagihan => Fmt.Rp(Row.SisaTagihan);
    public string TerakhirPesan => Row.TerakhirPesan is { } t ? Fmt.Relatif(t) : "-";
    public string StatusAkun => Pengguna.IsActive ? "Aktif" : "Nonaktif";
    public bool IsActive => Pengguna.IsActive;
    public bool BisaUbahAkun { get; }
    public string TombolAkun => Pengguna.IsActive ? "Nonaktifkan" : "Aktifkan";

    [RelayCommand]
    private Task UbahStatusAkun() => _toggle(this);
}

/// <summary>Penyedia: daftar pelanggannya. Admin: semua akun + aktif/nonaktif.</summary>
public partial class PelangganPageViewModel : ObservableObject
{
    private readonly PenggunaService _service = new();
    private List<PelangganItem> _semua = new();

    public PelangganPageViewModel()
    {
        User = Sesi.Wajib;
        _ = MuatAsync();
    }

    public Pengguna User { get; }
    public bool IsAdmin => User.Peran == Peran.Admin;
    public string JudulHalaman => IsAdmin ? "Pengguna" : "Pelanggan";
    public string Subjudul => IsAdmin
        ? "Semua akun. Nonaktifkan akun agar tidak bisa login."
        : "Pemesan yang pernah memesan di event milikmu.";
    public string[] FilterPeran { get; } = { "Semua peran", "Admin", "Penyedia", "Pemesan" };

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedPeran = "Semua peran";
    [ObservableProperty] private ObservableCollection<PelangganItem> _baris = new();
    [ObservableProperty] private bool _kosong;
    [ObservableProperty] private string _ringkasan = "";

    partial void OnSearchTextChanged(string value) => Terapkan();
    partial void OnSelectedPeranChanged(string value) => Terapkan();

    private async Task MuatAsync()
    {
        try
        {
            _semua = (await _service.DaftarAsync())
                .Select(r => new PelangganItem(r, IsAdmin && r.Pengguna.Id != User.Id, ToggleAsync)).ToList();
            Terapkan();
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, "Memuat pelanggan"));
        }
    }

    private void Terapkan()
    {
        var q = (SearchText ?? "").Trim();
        var data = _semua.Where(p =>
            q.Length == 0 ||
            p.Nama.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            p.Pengguna.Username.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            p.Pengguna.Kontak.Contains(q, StringComparison.OrdinalIgnoreCase));
        if (SelectedPeran != "Semua peran") data = data.Where(p => p.PeranText == SelectedPeran);

        Baris = new ObservableCollection<PelangganItem>(data);
        Kosong = Baris.Count == 0;
        Ringkasan = $"{Baris.Count} orang · total belanja {Fmt.Rp(Baris.Sum(b => b.Row.TotalBelanja))} · belum dibayar {Fmt.Rp(Baris.Sum(b => b.Row.SisaTagihan))}";
    }

    private async Task ToggleAsync(PelangganItem item)
    {
        try
        {
            await _service.SetAktifAsync(item.Pengguna.Id, !item.IsActive);
            UiBus.Info($"Akun {item.Nama} {(item.IsActive ? "dinonaktifkan" : "diaktifkan")}.");
            await MuatAsync();
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, "Mengubah status akun"));
        }
    }
}
