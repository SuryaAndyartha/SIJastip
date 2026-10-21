using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JastipApp.Helpers;
using JastipApp.Models;
using JastipApp.Services;

namespace JastipApp.ViewModels;

/// <summary>Halaman Event: daftar event, detail, pemesanan, dan form buat/edit event.</summary>
public partial class EventPageViewModel : ObservableObject
{
    private readonly EventService _eventService = new();
    private readonly PesananService _pesananService = new();

    private readonly DispatcherTimer _timer;
    private Dictionary<EventViewModel, bool> _statusTerakhir = new();
    private Guid? _editId;

    // ---- Pengguna yang login ----
    public Pengguna User { get; }
    public bool IsPemesan => User.Peran == Peran.Pemesan;
    public bool IsPenyedia => User.Peran == Peran.Penyedia;
    public bool IsAdmin => User.Peran == Peran.Admin;

    public string[] KategoriFilter { get; } = { "Semua", "Makanan", "Barang" };
    public string[] KategoriForm { get; } = { "Makanan", "Barang" };

    public ObservableCollection<EventViewModel> AllEvents { get; } = new();

    // ---- Daftar & filter ----
    [ObservableProperty] private ObservableCollection<EventViewModel> _filteredEvents = new();
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedKategori = "Semua";
    [ObservableProperty] private bool _hanyaDibuka;
    [ObservableProperty] private bool _hanyaMilikSaya;
    [ObservableProperty] private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDetail), nameof(ShowEmpty))]
    private EventViewModel? _selectedEvent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDetail), nameof(ShowEmpty))]
    private bool _isFormOpen;

    public bool ShowDetail => !IsFormOpen && SelectedEvent != null;
    public bool ShowEmpty => !IsFormOpen && SelectedEvent == null;
    public bool IsFilterKosong => !IsLoading && FilteredEvents.Count == 0;
    public string HasilText => IsLoading ? "Memuat data…" : $"{FilteredEvents.Count} dari {AllEvents.Count} event";

    // ---- Hapus event (konfirmasi di panel detail) ----
    [ObservableProperty] private bool _konfirmasiHapus;
    [ObservableProperty] private string _hapusInfo = "";

    // ---- Form event (buat baru / edit) ----
    [ObservableProperty] private string _formJudul = "Buat event jastip";
    [ObservableProperty] private string _newJudul = "";
    [ObservableProperty] private string _newKategori = "Makanan";
    [ObservableProperty] private string _newLokasi = "";
    [ObservableProperty] private string _newDeskripsi = "";
    [ObservableProperty] private string _newKontak = "";
    [ObservableProperty] private DateTimeOffset? _newDeadlineDate;
    [ObservableProperty] private TimeSpan? _newDeadlineTime;
    [ObservableProperty] private DateTimeOffset? _newEstimasiDate;
    [ObservableProperty] private TimeSpan? _newEstimasiTime;
    [ObservableProperty] private decimal? _newOngkir = 0;
    [ObservableProperty] private decimal? _newJasa = 0;
    [ObservableProperty] private decimal? _newKuota = 10;
    [ObservableProperty] private string _newItemsText = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _sedangMenyimpan;

    public EventPageViewModel()
    {
        User = Sesi.Wajib;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => PerbaruiStatus();
        _timer.Start();

        _ = MuatAsync();
    }

    // =====================================================================
    //  Memuat data
    // =====================================================================

    private async Task MuatAsync(Guid? pilihId = null)
    {
        IsLoading = true;
        OnPropertyChanged(nameof(HasilText));
        try
        {
            var events = await _eventService.DaftarAsync();

            // Pemesan: tandai event yang sudah ia pesan (pesanan aktif).
            var milikSaya = new Dictionary<Guid, PesananViewModel>();
            if (IsPemesan)
                foreach (var p in (await _pesananService.PesananSayaAsync()).Where(p => p.Status != StatusPesanan.Dibatalkan))
                    milikSaya[p.EventId] = new PesananViewModel(p, User, BatalkanPesananAsync);

            var idTerpilih = pilihId ?? SelectedEvent?.Model.Id;
            AllEvents.Clear();
            foreach (var e in events)
                AllEvents.Add(new EventViewModel(e, User, milikSaya.GetValueOrDefault(e.Id)));

            IsLoading = false;
            ApplyFilter(idTerpilih);
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, "Memuat daftar event"));
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasilText));
            OnPropertyChanged(nameof(IsFilterKosong));
        }
    }

    private void PerbaruiStatus()
    {
        foreach (var e in AllEvents) e.Refresh();

        // Bila ada event yang baru tutup/buka, susun ulang daftar (filter & urutan ikut berubah).
        var berubah = AllEvents.Any(e => !_statusTerakhir.TryGetValue(e, out var lama) || lama != e.IsOpen);
        if (berubah) ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedKategoriChanged(string value) => ApplyFilter();
    partial void OnHanyaDibukaChanged(bool value) => ApplyFilter();
    partial void OnHanyaMilikSayaChanged(bool value) => ApplyFilter();

    private void ApplyFilter(Guid? pilihId = null)
    {
        var q = (SearchText ?? "").Trim();
        var idTerpilih = pilihId ?? SelectedEvent?.Model.Id;

        var hasil = AllEvents
            .Where(e => SelectedKategori == "Semua" || e.Model.Kategori.ToString() == SelectedKategori)
            .Where(e => !HanyaDibuka || e.IsOpen)
            .Where(e => !HanyaMilikSaya || e.IsMilikSaya)
            .Where(e => q.Length == 0 || Cocok(e, q))
            .OrderByDescending(e => e.IsOpen)
            .ThenBy(e => e.Model.Deadline)
            .ToList();

        _statusTerakhir = AllEvents.ToDictionary(e => e, e => e.IsOpen);
        FilteredEvents = new ObservableCollection<EventViewModel>(hasil);
        SelectedEvent = idTerpilih is null ? null : hasil.FirstOrDefault(e => e.Model.Id == idTerpilih);

        OnPropertyChanged(nameof(IsFilterKosong));
        OnPropertyChanged(nameof(HasilText));
    }

    private static bool Cocok(EventViewModel e, string q)
    {
        bool Has(string s) => s.Contains(q, StringComparison.OrdinalIgnoreCase);
        return Has(e.Model.Judul) || Has(e.NamaPenyedia) || Has(e.Model.LokasiBelanja)
               || e.Model.Items.Any(i => Has(i.Nama));
    }

    partial void OnSelectedEventChanged(EventViewModel? value)
    {
        KonfirmasiHapus = false;
        if (value is null) return;

        if (value.BisaKelola) _ = MuatPesananMasukAsync(value);
    }

    private async Task MuatPesananMasukAsync(EventViewModel ev)
    {
        try
        {
            var list = await _pesananService.PesananEventAsync(ev.Model.Id);
            ev.IsiPesananMasuk(list.Select(p => new PesananViewModel(p, User, BatalkanPesananAsync)));
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, "Memuat pesanan masuk"));
        }
    }

    // =====================================================================
    //  Pesanan
    // =====================================================================

    /// <summary>
    /// Menyimpan pesanan baru (kuota berkurang satu) atau perubahan pesanan, lalu mengembalikan
    /// ringkasan untuk disalin ke clipboard. Null bila gagal; pesan error tampil di panel detail.
    /// </summary>
    public async Task<string?> KirimPesananAsync(EventViewModel ev)
    {
        if (!ev.AdaPesanan)
        {
            ev.PesanInfo = "Pilih minimal satu barang dulu.";
            return null;
        }

        try
        {
            var qty = ev.Items.Where(i => i.Qty > 0).ToDictionary(i => i.Item.Id, i => i.Qty);
            int pesananId;
            if (ev.SedangUbah && ev.PesananSaya is { } lama)
            {
                await _pesananService.UbahIsiAsync(lama.Model.Id, qty);
                pesananId = lama.Model.Id;
            }
            else
            {
                pesananId = (await _pesananService.BuatAsync(ev.Model.Id, qty)).Id;
            }

            await MuatAsync(ev.Model.Id);
            var baru = SelectedEvent?.PesananSaya;
            UiBus.Info(ev.SedangUbah
                ? "Perubahan pesanan tersimpan dan ringkasannya tersalin."
                : $"Pesanan tersimpan dan ringkasannya tersalin. Tempel ke chat {ev.NamaPenyedia} untuk konfirmasi.");
            return baru?.Model.Id == pesananId ? baru.Ringkasan : null;
        }
        catch (Exception ex)
        {
            ev.PesanInfo = UiBus.PesanError(ex, "Menyimpan pesanan");
            return null;
        }
    }

    private async Task BatalkanPesananAsync(PesananViewModel p)
    {
        try
        {
            await _pesananService.BatalkanAsync(p.Model.Id);
            UiBus.Info("Pesanan dibatalkan. Slot kuota dikembalikan.");
            await MuatAsync();
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, "Membatalkan pesanan"));
        }
    }

    // =====================================================================
    //  Form event: buat, edit
    // =====================================================================

    [RelayCommand]
    private void BukaForm()
    {
        if (!IsPenyedia) return;

        _editId = null;
        FormJudul = "Buat event jastip";
        NewJudul = NewLokasi = NewDeskripsi = NewItemsText = ErrorMessage = "";
        NewKontak = User.Kontak;
        NewKategori = "Makanan";
        NewOngkir = 0;
        NewJasa = 0;
        NewKuota = 10;
        NewDeadlineDate = DateTimeOffset.Now.AddDays(1);
        NewDeadlineTime = new TimeSpan(20, 0, 0);
        NewEstimasiDate = DateTimeOffset.Now.AddDays(4);
        NewEstimasiTime = new TimeSpan(12, 0, 0);
        IsFormOpen = true;
    }

    [RelayCommand]
    private void EditEvent()
    {
        var ev = SelectedEvent;
        if (ev is null || !ev.BisaKelola) return;
        var m = ev.Model;

        _editId = m.Id;
        FormJudul = "Edit event";
        ErrorMessage = "";
        NewJudul = m.Judul;
        NewKategori = m.Kategori.ToString();
        NewLokasi = m.LokasiBelanja;
        NewDeskripsi = m.Deskripsi;
        NewKontak = m.Kontak;
        NewDeadlineDate = new DateTimeOffset(m.Deadline.Date);
        NewDeadlineTime = new TimeSpan(m.Deadline.Hour, m.Deadline.Minute, 0);
        NewEstimasiDate = new DateTimeOffset(m.EstimasiSampai.Date);
        NewEstimasiTime = new TimeSpan(m.EstimasiSampai.Hour, m.EstimasiSampai.Minute, 0);
        NewOngkir = m.OngkosKirim;
        NewJasa = m.BiayaJasa;
        NewKuota = m.Kuota;
        NewItemsText = string.Join(Environment.NewLine, m.Items.Select(i =>
            string.IsNullOrWhiteSpace(i.Catatan)
                ? $"{i.Nama} | {Fmt.AngkaPolos(i.Harga)}"
                : $"{i.Nama} | {Fmt.AngkaPolos(i.Harga)} | {i.Catatan}"));
        IsFormOpen = true;
    }

    [RelayCommand]
    private void BatalForm() => IsFormOpen = false;

    [RelayCommand]
    private async Task SimpanEvent()
    {
        if (SedangMenyimpan) return;
        ErrorMessage = "";

        if (NewDeadlineDate is null || NewDeadlineTime is null) { ErrorMessage = "Tanggal dan jam deadline wajib diisi."; return; }
        if (NewEstimasiDate is null || NewEstimasiTime is null) { ErrorMessage = "Tanggal dan jam estimasi sampai wajib diisi."; return; }

        // Format barang: satu baris per barang -> Nama | Harga | Catatan (opsional)
        var barang = new List<BarangInput>();
        var baris = (NewItemsText ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int n = 0; n < baris.Length; n++)
        {
            var bagian = baris[n].Split('|', StringSplitOptions.TrimEntries);
            if (bagian[0].Length == 0 || bagian.Length < 2 || !Fmt.TryParseRupiah(bagian[1], out var harga) || harga <= 0)
            {
                ErrorMessage = $"Barang baris {n + 1} belum benar. Gunakan format: Nama | Harga | Catatan.";
                return;
            }
            barang.Add(new BarangInput(bagian[0], harga, bagian.Length > 2 ? bagian[2] : ""));
        }

        var input = new EventInput
        {
            Judul = NewJudul ?? "",
            Kategori = NewKategori == "Barang" ? KategoriJastip.Barang : KategoriJastip.Makanan,
            Lokasi = NewLokasi ?? "",
            Deskripsi = NewDeskripsi ?? "",
            Kontak = NewKontak ?? "",
            Deadline = NewDeadlineDate.Value.Date + NewDeadlineTime.Value,
            Estimasi = NewEstimasiDate.Value.Date + NewEstimasiTime.Value,
            Ongkir = NewOngkir ?? 0,
            Jasa = NewJasa ?? 0,
            Kuota = (int)(NewKuota ?? 0),
            Barang = barang
        };

        SedangMenyimpan = true;
        try
        {
            Guid id;
            if (_editId is { } editId)
            {
                await _eventService.UbahAsync(editId, input);
                id = editId;
                UiBus.Info("Perubahan event tersimpan.");
            }
            else
            {
                id = (await _eventService.BuatAsync(input)).Id;
                SearchText = "";
                SelectedKategori = "Semua";
                HanyaDibuka = false;
                UiBus.Info("Event baru tersimpan.");
            }

            IsFormOpen = false;
            await MuatAsync(id);
        }
        catch (Exception ex)
        {
            ErrorMessage = UiBus.PesanError(ex, "Menyimpan event");
        }
        finally
        {
            SedangMenyimpan = false;
        }
    }

    // =====================================================================
    //  Hapus event
    // =====================================================================

    [RelayCommand]
    private void MintaHapus()
    {
        var ev = SelectedEvent;
        if (ev is null || !ev.BisaKelola) return;
        HapusInfo = ev.Model.Terisi > 0
            ? $"Event ini punya {ev.Model.Terisi} pesanan aktif yang akan ikut terhapus."
            : "Event dan daftar barangnya akan dihapus permanen.";
        KonfirmasiHapus = true;
    }

    [RelayCommand]
    private void BatalHapus() => KonfirmasiHapus = false;

    [RelayCommand]
    private async Task HapusEvent()
    {
        var ev = SelectedEvent;
        if (ev is null) return;
        try
        {
            await _eventService.HapusAsync(ev.Model.Id);
            KonfirmasiHapus = false;
            SelectedEvent = null;
            UiBus.Info($"Event \"{ev.Judul}\" dihapus.");
            await MuatAsync();
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, "Menghapus event"));
        }
    }

    // =====================================================================
    //  Lain-lain
    // =====================================================================

    /// <summary>Dipanggil saat halaman ditinggalkan.</summary>
    public void Berhenti() => _timer.Stop();
}
