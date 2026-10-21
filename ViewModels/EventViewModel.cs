using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JastipApp.Helpers;
using JastipApp.Models;
using JastipApp.Services;

namespace JastipApp.ViewModels;

/// <summary>Membungkus JastipEvent dengan data turunan untuk tampilan (status, sisa waktu, total, hak akses).</summary>
public partial class EventViewModel : ObservableObject
{
    private static readonly IBrush Hijau = Brush.Parse("#0F7B5F");
    private static readonly IBrush Kuning = Brush.Parse("#B45309");
    private static readonly IBrush Abu = Brush.Parse("#6B7280");
    private static readonly IBrush Merah = Brush.Parse("#9F1239");

    private readonly Pengguna? _user;

    public EventViewModel(JastipEvent model, Pengguna? user, PesananViewModel? pesananSaya)
    {
        Model = model;
        _user = user;
        PesananSaya = pesananSaya;
        Items = new ObservableCollection<ItemViewModel>(
            model.Items.Select(i => new ItemViewModel(i, OnItemChanged, () => BisaPesan)));
    }

    public JastipEvent Model { get; }
    public ObservableCollection<ItemViewModel> Items { get; }

    [ObservableProperty]
    private string _pesanInfo = "";

    // ---- Data dasar ----
    public string Judul => Model.Judul;
    public string Deskripsi => Model.Deskripsi;
    public string LokasiBelanja => Model.LokasiBelanja;
    public string Kontak => Model.Kontak;
    public string NamaPenyedia => Model.Penyedia.Nama;
    public string PenyediaLokasi => $"oleh {NamaPenyedia}, belanja di {Model.LokasiBelanja}";
    public string OngkirText => Model.OngkosKirim == 0 ? "Gratis" : Fmt.Rp(Model.OngkosKirim);
    public string JasaText => Model.BiayaJasa == 0 ? "Gratis" : Fmt.Rp(Model.BiayaJasa);
    public string EstimasiText => Fmt.Tanggal(Model.EstimasiSampai);

    // ---- Status & waktu (berubah seiring waktu) ----
    public bool IsPenuh => Model.Terisi >= Model.Kuota;
    public bool IsExpired => Model.Deadline <= DateTime.Now;
    public bool IsOpen => !IsExpired && !IsPenuh;

    public string StatusText =>
        IsExpired ? "Ditutup" :
        IsPenuh ? "Kuota penuh" :
        (Model.Deadline - DateTime.Now).TotalHours < 3 ? "Segera tutup" : "Dibuka";

    public IBrush StatusBrush =>
        IsExpired ? Abu :
        IsPenuh ? Merah :
        (Model.Deadline - DateTime.Now).TotalHours < 3 ? Kuning : Hijau;

    public string DeadlineText => Fmt.Tanggal(Model.Deadline);
    public string SisaText => Fmt.SisaWaktu(Model.Deadline);
    public string DeadlineRingkas => $"{DeadlineText} ({SisaText})";

    public int SisaSlot => Math.Max(0, Model.Kuota - Model.Terisi);
    public string KuotaText => $"{Model.Terisi} dari {Model.Kuota} slot terisi · sisa {SisaSlot}";
    public double KuotaPersen => Model.Kuota == 0 ? 0 : 100.0 * Model.Terisi / Model.Kuota;

    // ---- Hak akses untuk pengguna yang sedang login ----
    public bool IsPemesan => _user?.Peran == Peran.Pemesan;
    public bool BisaKelola => EventService.BisaKelola(_user, Model);
    public bool IsMilikSaya => _user is not null && Model.PenyediaId == _user.Id;

    /// <summary>Pesanan aktif milik pemesan yang login di event ini (null bila belum memesan).</summary>
    public PesananViewModel? PesananSaya { get; }
    public bool AdaPesananSaya => PesananSaya is not null;

    /// <summary>Mode ubah pesanan: pemesan mengubah jumlah barang pada pesanan yang sudah ada.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TampilkanPilihBarang), nameof(BisaPesan), nameof(BisaUbahPesanan),
                              nameof(TampilkanPesananSaya), nameof(TombolPesanText))]
    private bool _sedangUbah;

    /// <summary>Panel pilih barang + rincian biaya: pemesan yang belum memesan, atau sedang mengubah pesanannya.</summary>
    public bool TampilkanPilihBarang => IsPemesan && (!AdaPesananSaya || SedangUbah);
    public bool BisaPesan => TampilkanPilihBarang && (SedangUbah ? !IsExpired : IsOpen);
    public bool TampilkanPesananSaya => AdaPesananSaya && !SedangUbah;
    public bool BisaUbahPesanan => AdaPesananSaya && !SedangUbah && !IsExpired
                                   && PesananSaya!.Model.Status == StatusPesanan.Diterima;
    public bool TampilkanInfoBukanPemesan => !IsPemesan && !BisaKelola;
    public string TombolPesanText => SedangUbah ? "Simpan perubahan" : "Pesan dan salin ringkasan";

    [RelayCommand]
    private void MulaiUbah()
    {
        if (PesananSaya is null) return;
        foreach (var item in Items)
            item.Qty = PesananSaya.Model.Detail.FirstOrDefault(d => d.NamaBarang == item.Nama)?.Qty ?? 0;
        SedangUbah = true;
        foreach (var item in Items) item.Refresh();
    }

    [RelayCommand]
    private void BatalUbah()
    {
        foreach (var item in Items) item.Qty = 0;
        SedangUbah = false;
        PesanInfo = "";
        foreach (var item in Items) item.Refresh();
    }

    // ---- Pesanan masuk (penyedia pemilik / admin) ----
    public ObservableCollection<PesananViewModel> PesananMasuk { get; } = new();

    [ObservableProperty] private string _pesananMasukRingkas = "";

    public void IsiPesananMasuk(IEnumerable<PesananViewModel> list)
    {
        PesananMasuk.Clear();
        foreach (var p in list) PesananMasuk.Add(p);

        var aktif = PesananMasuk.Where(p => p.IsAktif).ToList();
        PesananMasukRingkas = aktif.Count == 0
            ? "Belum ada pesanan."
            : $"{aktif.Count} pesanan · total {Fmt.Rp(aktif.Sum(p => p.Model.Total))} · belum dibayar {Fmt.Rp(aktif.Sum(p => p.Model.SisaTagihan))}";
        OnPropertyChanged(nameof(AdaPesananMasuk));
    }

    public bool AdaPesananMasuk => PesananMasuk.Count > 0;

    // ---- Kalkulasi pesanan ----
    public bool AdaPesanan => Items.Any(i => i.Qty > 0);
    public decimal TotalBarang => Items.Sum(i => i.Subtotal);
    public string TotalBarangText => Fmt.Rp(TotalBarang);
    public decimal TotalBayar => AdaPesanan ? TotalBarang + Model.BiayaJasa + Model.OngkosKirim : 0;
    public string TotalBayarText => Fmt.Rp(TotalBayar);

    private void OnItemChanged()
    {
        OnPropertyChanged(nameof(AdaPesanan));
        OnPropertyChanged(nameof(TotalBarang));
        OnPropertyChanged(nameof(TotalBarangText));
        OnPropertyChanged(nameof(TotalBayar));
        OnPropertyChanged(nameof(TotalBayarText));
        PesanInfo = "";
    }

    /// <summary>Dipanggil timer agar status, sisa waktu, dan tombol ikut diperbarui.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(IsExpired));
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(BisaPesan));
        OnPropertyChanged(nameof(BisaUbahPesanan));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(SisaText));
        OnPropertyChanged(nameof(DeadlineRingkas));
        foreach (var item in Items) item.Refresh();
    }
}
