using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JastipApp.Helpers;
using JastipApp.Models;
using JastipApp.Services;

namespace JastipApp.ViewModels;

/// <summary>Halaman login dan daftar akun.</summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth = new();

    /// <summary>Dipanggil setelah login/daftar berhasil; jendela akan membuka MainWindow.</summary>
    public event Action? Berhasil;

    public LoginViewModel(string? errorAwal = null)
    {
        ErrorMessage = errorAwal ?? "";
        DatabaseGagal = errorAwal is not null;
    }

    public bool DatabaseGagal { get; }

    public string[] PeranDaftar { get; } = { "Pemesan", "Penyedia jasa" };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModeMasuk))]
    private bool _isModeDaftar;

    public bool IsModeMasuk => !IsModeDaftar;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MasukCommand), nameof(DaftarCommand))]
    private bool _sedangProses;

    [ObservableProperty] private string _errorMessage = "";

    // ---- Masuk ----
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";

    // ---- Daftar ----
    [ObservableProperty] private string _daftarNama = "";
    [ObservableProperty] private string _daftarUsername = "";
    [ObservableProperty] private string _daftarPassword = "";
    [ObservableProperty] private string _daftarKonfirmasi = "";
    [ObservableProperty] private string _daftarKontak = "";
    [ObservableProperty] private string _daftarPeran = "Pemesan";

    private bool BisaKirim() => !SedangProses && !DatabaseGagal;

    [RelayCommand(CanExecute = nameof(BisaKirim))]
    private Task Masuk() => Jalankan(() => _auth.LoginAsync(Username, Password));

    [RelayCommand(CanExecute = nameof(BisaKirim))]
    private Task Daftar() => Jalankan(() => _auth.DaftarAsync(
        DaftarNama, DaftarUsername, DaftarPassword, DaftarKonfirmasi,
        DaftarPeran == "Penyedia jasa" ? Peran.Penyedia : Peran.Pemesan,
        DaftarKontak));

    [RelayCommand]
    private void GantiMode()
    {
        IsModeDaftar = !IsModeDaftar;
        if (!DatabaseGagal) ErrorMessage = "";
    }

    private async Task Jalankan(Func<Task<Pengguna>> aksi)
    {
        ErrorMessage = "";
        SedangProses = true;
        try
        {
            await aksi();
            Password = DaftarPassword = DaftarKonfirmasi = "";
            Berhasil?.Invoke();
        }
        catch (AppException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            Log.Error("Login/daftar", ex);
            ErrorMessage = "Terjadi kesalahan saat menghubungi database. Coba lagi.";
        }
        finally
        {
            SedangProses = false;
        }
    }
}
