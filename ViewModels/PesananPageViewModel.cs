using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JastipApp.Helpers;
using JastipApp.Models;
using JastipApp.Services;

namespace JastipApp.ViewModels;

/// <summary>
/// Halaman Pesanan: tabel pesanan (cari, filter, urut, halaman) + panel detail
/// untuk status, pembayaran, pembatalan, dan penghapusan.
/// </summary>
public partial class PesananPageViewModel : ObservableObject
{
    private const int UkuranHalaman = 10;
    private readonly PesananService _service = new();
    private List<PesananViewModel> _semua = new();
    private List<PesananViewModel> _hasil = new();

    public PesananPageViewModel()
    {
        User = Sesi.Wajib;
        _ = MuatAsync();
    }

    public Pengguna User { get; }
    public bool IsPemesan => User.Peran == Peran.Pemesan;
    public bool IsAdmin => User.Peran == Peran.Admin;
    public string JudulHalaman => IsPemesan ? "Pesanan saya" : "Pesanan";
    public string Subjudul => User.Peran switch
    {
        Peran.Admin => "Semua pesanan di seluruh event.",
        Peran.Penyedia => "Pesanan yang masuk ke event milikmu. Perbarui status dan catat pembayaran di sini.",
        _ => "Pantau status dan tagihan pesananmu."
    };
    public string KolomPihak => IsPemesan ? "PENYEDIA" : "PEMESAN";

    public string[] FilterStatus { get; } =
        { "Semua status", "Diterima", "Sudah dibeli", "Dikirim", "Selesai", "Dibatalkan" };
    public string[] FilterBayar { get; } = { "Semua pembayaran", "Belum lunas", "Lunas" };
    public string[] Urutan { get; } = { "Terbaru", "Terlama", "Total terbesar", "Sisa tagihan terbesar", "Kode" };
    public string[] MetodeBayar => PesananService.MetodePembayaran;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedStatus = "Semua status";
    [ObservableProperty] private string _selectedBayar = "Semua pembayaran";
    [ObservableProperty] private string _selectedUrutan = "Terbaru";
    [ObservableProperty] private bool _isLoading = true;

    [ObservableProperty] private ObservableCollection<PesananViewModel> _baris = new();
    [ObservableProperty] private int _halaman = 1;
    [ObservableProperty] private string _infoHalaman = "";
    [ObservableProperty] private bool _bisaSebelumnya;
    [ObservableProperty] private bool _bisaBerikutnya;
    [ObservableProperty] private bool _kosong;
    [ObservableProperty] private string _ringkasan = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AdaPilihan), nameof(TanpaPilihan))]
    private PesananViewModel? _selected;

    public bool AdaPilihan => Selected is not null;
    public bool TanpaPilihan => Selected is null;

    // ---- Form pembayaran ----
    [ObservableProperty] private decimal? _bayarJumlah;
    [ObservableProperty] private string _bayarMetode = "Transfer";
    [ObservableProperty] private string _bayarCatatan = "";
    [ObservableProperty] private string _bayarError = "";

    // ---- Konfirmasi hapus ----
    [ObservableProperty] private bool _konfirmasiHapus;

    partial void OnSearchTextChanged(string value) => Terapkan(resetHalaman: true);
    partial void OnSelectedStatusChanged(string value) => Terapkan(resetHalaman: true);
    partial void OnSelectedBayarChanged(string value) => Terapkan(resetHalaman: true);
    partial void OnSelectedUrutanChanged(string value) => Terapkan(resetHalaman: true);

    partial void OnSelectedChanged(PesananViewModel? value)
    {
        KonfirmasiHapus = false;
        BayarError = "";
        BayarCatatan = "";
        BayarJumlah = value?.Model.SisaTagihan > 0 ? value.Model.SisaTagihan : null;
    }

    private async Task MuatAsync(int? pilihId = null)
    {
        IsLoading = true;
        try
        {
            var idTerpilih = pilihId ?? Selected?.Model.Id;
            _semua = (await _service.DaftarAsync()).Select(p => new PesananViewModel(p, User, BatalkanAsync)).ToList();
            Terapkan(resetHalaman: false, idTerpilih);
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, "Memuat pesanan"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Terapkan(bool resetHalaman, int? pilihId = null)
    {
        var q = (SearchText ?? "").Trim();
        IEnumerable<PesananViewModel> data = _semua;

        if (q.Length > 0)
            data = data.Where(p =>
                p.Kode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.NamaPemesan.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.NamaPenyedia.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.JudulEvent.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Model.Detail.Any(d => d.NamaBarang.Contains(q, StringComparison.OrdinalIgnoreCase)));

        if (SelectedStatus != "Semua status")
            data = data.Where(p => p.StatusText == SelectedStatus);

        data = SelectedBayar switch
        {
            "Lunas" => data.Where(p => p.Model.IsLunas),
            "Belum lunas" => data.Where(p => p.IsAktif && !p.Model.IsLunas),
            _ => data
        };

        data = SelectedUrutan switch
        {
            "Terlama" => data.OrderBy(p => p.Model.Waktu),
            "Total terbesar" => data.OrderByDescending(p => p.Model.Total),
            "Sisa tagihan terbesar" => data.OrderByDescending(p => p.Model.SisaTagihan),
            "Kode" => data.OrderBy(p => p.Kode),
            _ => data.OrderByDescending(p => p.Model.Waktu)
        };

        _hasil = data.ToList();
        var aktif = _hasil.Where(p => p.IsAktif).ToList();
        Ringkasan = $"{_hasil.Count} pesanan · total {Fmt.Rp(aktif.Sum(p => p.Model.Total))} · belum dibayar {Fmt.Rp(aktif.Sum(p => p.Model.SisaTagihan))}";
        Kosong = _hasil.Count == 0;

        if (resetHalaman) Halaman = 1;
        if (pilihId is not null)
        {
            var idx = _hasil.FindIndex(p => p.Model.Id == pilihId);
            if (idx >= 0) Halaman = idx / UkuranHalaman + 1;
        }
        TampilkanHalaman(pilihId ?? Selected?.Model.Id);
    }

    private void TampilkanHalaman(int? pilihId)
    {
        var total = Math.Max(1, (int)Math.Ceiling(_hasil.Count / (double)UkuranHalaman));
        Halaman = Math.Clamp(Halaman, 1, total);
        Baris = new ObservableCollection<PesananViewModel>(
            _hasil.Skip((Halaman - 1) * UkuranHalaman).Take(UkuranHalaman));
        InfoHalaman = $"Halaman {Halaman} dari {total}";
        BisaSebelumnya = Halaman > 1;
        BisaBerikutnya = Halaman < total;
        Selected = pilihId is null ? null : _hasil.FirstOrDefault(p => p.Model.Id == pilihId);
    }

    [RelayCommand]
    private void HalamanSebelumnya() { Halaman--; TampilkanHalaman(Selected?.Model.Id); }

    [RelayCommand]
    private void HalamanBerikutnya() { Halaman++; TampilkanHalaman(Selected?.Model.Id); }

    [RelayCommand]
    private Task MuatUlang() => MuatAsync();

    // =====================================================================
    //  Aksi
    // =====================================================================

    private async Task Jalankan(Func<Task> aksi, string berhasil, string konteks, int? pilihId)
    {
        try
        {
            await aksi();
            UiBus.Info(berhasil);
            await MuatAsync(pilihId);
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, konteks));
        }
    }

    private Task BatalkanAsync(PesananViewModel p) =>
        Jalankan(() => _service.BatalkanAsync(p.Model.Id),
            $"Pesanan {p.Kode} dibatalkan. Slot kuota dikembalikan.", "Membatalkan pesanan", p.Model.Id);

    [RelayCommand]
    private Task MajukanStatus()
    {
        if (Selected is not { } p) return Task.CompletedTask;
        var berikut = p.Model.Status.Berikutnya();
        return Jalankan(() => _service.MajukanStatusAsync(p.Model.Id),
            $"Status {p.Kode} menjadi \"{berikut?.Teks()}\".", "Mengubah status", p.Model.Id);
    }

    [RelayCommand]
    private async Task TambahPembayaran()
    {
        if (Selected is not { } p) return;
        BayarError = "";
        if (BayarJumlah is not { } jumlah || jumlah <= 0)
        {
            BayarError = "Isi jumlah pembayaran lebih dari 0.";
            return;
        }
        try
        {
            await _service.TambahPembayaranAsync(p.Model.Id, jumlah, BayarMetode, BayarCatatan);
            UiBus.Info($"Pembayaran {Fmt.Rp(jumlah)} untuk {p.Kode} tercatat.");
            await MuatAsync(p.Model.Id);
        }
        catch (Exception ex)
        {
            BayarError = UiBus.PesanError(ex, "Mencatat pembayaran");
        }
    }

    [RelayCommand]
    private Task HapusPembayaran(PembayaranViewModel? b)
    {
        if (b is null || Selected is not { } p) return Task.CompletedTask;
        return Jalankan(() => _service.HapusPembayaranAsync(b.Model.Id),
            $"Pembayaran {b.JumlahText} dihapus.", "Menghapus pembayaran", p.Model.Id);
    }

    [RelayCommand]
    private void MintaHapus() => KonfirmasiHapus = Selected is not null;

    [RelayCommand]
    private void BatalHapus() => KonfirmasiHapus = false;

    [RelayCommand]
    private Task HapusPesanan()
    {
        if (Selected is not { } p) return Task.CompletedTask;
        KonfirmasiHapus = false;
        return Jalankan(() => _service.HapusAsync(p.Model.Id),
            $"Pesanan {p.Kode} dihapus permanen.", "Menghapus pesanan", null);
    }
}
