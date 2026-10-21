using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JastipApp.Data;
using JastipApp.Helpers;
using JastipApp.Models;
using JastipApp.Services;

namespace JastipApp.ViewModels;

public class CadanganItem
{
    public CadanganItem(FileInfo f)
    {
        File = f;
        Nama = f.Name;
        Info = $"{Fmt.TanggalPendek(f.LastWriteTime)} · {f.Length / 1024.0:0.#} KB";
    }

    public FileInfo File { get; }
    public string Nama { get; }
    public string Info { get; }
}

/// <summary>Halaman Pengaturan: tema, profil, ganti password, dan cadangan database (admin).</summary>
public partial class PengaturanViewModel : ObservableObject
{
    private readonly PenggunaService _pengguna = new();
    private readonly BackupService _backup = new();

    public PengaturanViewModel()
    {
        var u = Sesi.Wajib;
        IsAdmin = u.Peran == Peran.Admin;
        _temaGelap = PengaturanService.TemaGelap;
        _nama = u.Nama;
        _kontak = u.Kontak;
        _alamat = u.Alamat;
        Username = "@" + u.Username;
        if (IsAdmin) MuatCadangan();
    }

    public bool IsAdmin { get; }
    public string Username { get; }
    public string LokasiDatabase => JastipDbContext.DbPath;
    public string LokasiCadangan => BackupService.FolderCadangan;

    // ---- Tema ----
    [ObservableProperty] private bool _temaGelap;
    partial void OnTemaGelapChanged(bool value) => PengaturanService.SetTemaGelap(value);

    // ---- Profil ----
    [ObservableProperty] private string _nama;
    [ObservableProperty] private string _kontak;
    [ObservableProperty] private string _alamat;
    [ObservableProperty] private string _profilPesan = "";
    [ObservableProperty] private bool _profilError;

    [RelayCommand]
    private async Task SimpanProfil()
    {
        try
        {
            await _pengguna.UbahProfilAsync(Nama, Kontak, Alamat);
            ProfilError = false;
            ProfilPesan = "Profil tersimpan.";
            UiBus.Info("Profil tersimpan.");
        }
        catch (Exception ex)
        {
            ProfilError = true;
            ProfilPesan = UiBus.PesanError(ex, "Menyimpan profil");
        }
    }

    // ---- Password ----
    [ObservableProperty] private string _passwordLama = "";
    [ObservableProperty] private string _passwordBaru = "";
    [ObservableProperty] private string _passwordKonfirmasi = "";
    [ObservableProperty] private string _passwordPesan = "";
    [ObservableProperty] private bool _passwordError;

    [RelayCommand]
    private async Task GantiPassword()
    {
        try
        {
            await _pengguna.GantiPasswordAsync(PasswordLama, PasswordBaru, PasswordKonfirmasi);
            PasswordLama = PasswordBaru = PasswordKonfirmasi = "";
            PasswordError = false;
            PasswordPesan = "Password berhasil diganti.";
            UiBus.Info("Password berhasil diganti.");
        }
        catch (Exception ex)
        {
            PasswordError = true;
            PasswordPesan = UiBus.PesanError(ex, "Mengganti password");
        }
    }

    // ---- Cadangan database (admin) ----
    public ObservableCollection<CadanganItem> Cadangan { get; } = new();
    [ObservableProperty] private bool _cadanganKosong;
    [ObservableProperty] private CadanganItem? _akanDipulihkan;
    [ObservableProperty] private bool _sedangProses;

    private void MuatCadangan()
    {
        try
        {
            Cadangan.Clear();
            foreach (var f in _backup.Daftar()) Cadangan.Add(new CadanganItem(f));
            CadanganKosong = Cadangan.Count == 0;
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, "Membaca daftar cadangan"));
        }
    }

    [RelayCommand]
    private async Task Cadangkan()
    {
        SedangProses = true;
        try
        {
            var path = await _backup.CadangkanAsync();
            UiBus.Info($"Cadangan dibuat: {Path.GetFileName(path)}");
            MuatCadangan();
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, "Mencadangkan database"));
        }
        finally
        {
            SedangProses = false;
        }
    }

    [RelayCommand]
    private void MintaPulihkan(CadanganItem? item) => AkanDipulihkan = item;

    [RelayCommand]
    private void BatalPulihkan() => AkanDipulihkan = null;

    [RelayCommand]
    private async Task Pulihkan()
    {
        if (AkanDipulihkan is not { } item) return;
        SedangProses = true;
        try
        {
            await _backup.PulihkanAsync(item.File.FullName);
            AkanDipulihkan = null;
            UiBus.Info("Database dipulihkan. Silakan login kembali.");
            UiBus.Logout();   // data akun bisa berbeda, jadi sesi diulang
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, "Memulihkan database"));
        }
        finally
        {
            SedangProses = false;
        }
    }
}
