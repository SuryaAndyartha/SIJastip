using System.Text;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JastipApp.Helpers;
using JastipApp.Models;
using JastipApp.Services;

namespace JastipApp.ViewModels;

/// <summary>Satu pesanan untuk ditampilkan di halaman Pesanan, panel detail event, dan sebagainya.</summary>
public partial class PesananViewModel : ObservableObject
{
    private static readonly IBrush Biru = Brush.Parse("#2563EB");
    private static readonly IBrush Kuning = Brush.Parse("#B45309");
    private static readonly IBrush Ungu = Brush.Parse("#7C3AED");
    private static readonly IBrush Hijau = Brush.Parse("#0F7B5F");
    private static readonly IBrush Abu = Brush.Parse("#6B7280");
    private static readonly IBrush Merah = Brush.Parse("#9F1239");

    private readonly Pengguna _user;
    private readonly Func<PesananViewModel, Task> _batalkan;

    public PesananViewModel(Pesanan model, Pengguna user, Func<PesananViewModel, Task> batalkan)
    {
        Model = model;
        _user = user;
        _batalkan = batalkan;
        Pembayaran = model.Pembayaran.OrderBy(b => b.Tanggal)
            .Select(b => new PembayaranViewModel(b, BisaKelola)).ToList();
    }

    public Pesanan Model { get; }
    public List<PembayaranViewModel> Pembayaran { get; }

    // ---- Identitas ----
    public string Kode => Model.Kode;
    public string JudulEvent => Model.Event.Judul;
    public string NamaPenyedia => Model.Event.Penyedia.Nama;
    public string PenyediaText => $"Penyedia: {NamaPenyedia} · {Model.Event.Kontak}";
    public string NamaPemesan => Model.Pemesan.Nama;
    public string PemesanText => string.IsNullOrWhiteSpace(Model.Pemesan.Kontak)
        ? Model.Pemesan.Nama
        : $"{Model.Pemesan.Nama} · {Model.Pemesan.Kontak}";
    public string AlamatPemesan => Model.Pemesan.Alamat;
    public bool AdaAlamat => !string.IsNullOrWhiteSpace(Model.Pemesan.Alamat);

    /// <summary>Kolom "pihak lain" di tabel: pemesan melihat nama penyedia, selain itu nama pemesan.</summary>
    public string PihakLain => _user.Peran == Peran.Pemesan ? NamaPenyedia : NamaPemesan;

    public string WaktuText => "Dipesan " + Fmt.Tanggal(Model.Waktu);
    public string TanggalPendek => Fmt.TanggalPendek(Model.Waktu);

    // ---- Status ----
    public bool IsAktif => Model.Status != StatusPesanan.Dibatalkan;
    public string StatusText => Model.Status.Teks();
    public IBrush StatusBrush => Model.Status switch
    {
        StatusPesanan.Diterima => Biru,
        StatusPesanan.Dibeli => Kuning,
        StatusPesanan.Dikirim => Ungu,
        StatusPesanan.Selesai => Hijau,
        _ => Abu
    };

    /// <summary>Langkah alur sebagai teks, mis. "Diterima › Sudah dibeli › [Dikirim] › Selesai".</summary>
    public string AlurText => Model.Status == StatusPesanan.Dibatalkan
        ? "Pesanan dibatalkan"
        : string.Join("  ›  ", new[] { StatusPesanan.Diterima, StatusPesanan.Dibeli, StatusPesanan.Dikirim, StatusPesanan.Selesai }
            .Select(s => s == Model.Status ? $"[{s.Teks()}]" : s.Teks()));

    // ---- Uang ----
    public string RincianText => string.Join(Environment.NewLine,
        Model.Detail.Select(d => $"{d.Qty} x {d.NamaBarang} = {Fmt.Rp(d.Harga * d.Qty)}"));
    public string TotalBarangText => Fmt.Rp(Model.TotalBarang);
    public string BiayaJasaText => Fmt.Rp(Model.BiayaJasa);
    public string OngkirText => Fmt.Rp(Model.OngkosKirim);
    public string TotalRp => Fmt.Rp(Model.Total);
    public string TotalText => "Total " + Fmt.Rp(Model.Total);
    public string DibayarText => Fmt.Rp(Model.TotalDibayar);
    public string SisaRp => Fmt.Rp(Model.SisaTagihan);

    /// <summary>Badge pembayaran tidak relevan untuk pesanan yang dibatalkan.</summary>
    public bool TampilBayar => Model.Status != StatusPesanan.Dibatalkan;

    public string BayarText =>
        Model.Status == StatusPesanan.Dibatalkan ? "-" :
        Model.IsLunas ? "Lunas" :
        Model.TotalDibayar > 0 ? "Sebagian" : "Belum bayar";
    public IBrush BayarBrush =>
        Model.Status == StatusPesanan.Dibatalkan ? Abu :
        Model.IsLunas ? Hijau :
        Model.TotalDibayar > 0 ? Kuning : Merah;

    // ---- Hak akses ----
    public bool BisaKelola => EventService.BisaKelola(_user, Model.Event);
    public bool IsMilikSaya => _user.Peran == Peran.Pemesan && Model.PemesanId == _user.Id;
    public bool IsAdmin => _user.Peran == Peran.Admin;

    /// <summary>Pemesan: status Diterima & sebelum deadline. Penyedia/admin: selama belum dikirim.</summary>
    public bool BisaDibatalkan =>
        (IsMilikSaya && Model.Status == StatusPesanan.Diterima && Model.Event.Deadline > DateTime.Now) ||
        (BisaKelola && Model.Status.BisaDibatalkan());

    public bool BisaMajukan => BisaKelola && Model.Status.Berikutnya() is not null;
    public string MajukanText => Model.Status.Berikutnya() is { } s ? $"Tandai: {s.Teks()}" : "";
    public bool BisaCatatBayar => BisaKelola && IsAktif && Model.SisaTagihan > 0;
    public bool AdaPembayaran => Pembayaran.Count > 0;

    [ObservableProperty] private bool _sedangProses;

    [RelayCommand]
    private async Task Batalkan()
    {
        if (SedangProses || !BisaDibatalkan) return;
        SedangProses = true;
        try { await _batalkan(this); }
        finally { SedangProses = false; }
    }

    /// <summary>Teks yang disalin ke clipboard untuk dikirim ke chat.</summary>
    public string Ringkasan
    {
        get
        {
            var e = Model.Event;
            var sb = new StringBuilder();
            sb.AppendLine($"Pesanan jastip {Model.Kode}: {e.Judul}");
            sb.AppendLine($"Penyedia: {e.Penyedia.Nama}");
            sb.AppendLine($"Pemesan: {PemesanText}");
            if (AdaAlamat) sb.AppendLine($"Alamat: {AlamatPemesan}");
            sb.AppendLine($"Status: {StatusText}");
            sb.AppendLine();
            foreach (var d in Model.Detail)
                sb.AppendLine($"- {d.Qty} x {d.NamaBarang} = {Fmt.Rp(d.Harga * d.Qty)}");
            sb.AppendLine();
            sb.AppendLine($"Subtotal barang: {Fmt.Rp(Model.TotalBarang)}");
            sb.AppendLine($"Biaya jasa: {Fmt.Rp(Model.BiayaJasa)}");
            sb.AppendLine($"Ongkos kirim: {Fmt.Rp(Model.OngkosKirim)}");
            sb.AppendLine($"Total: {Fmt.Rp(Model.Total)}");
            if (Model.TotalDibayar > 0)
            {
                sb.AppendLine($"Sudah dibayar: {Fmt.Rp(Model.TotalDibayar)}");
                sb.AppendLine($"Sisa tagihan: {Fmt.Rp(Model.SisaTagihan)}");
            }
            sb.AppendLine($"Estimasi sampai: {Fmt.Tanggal(e.EstimasiSampai)}");
            return sb.ToString();
        }
    }
}

/// <summary>Satu baris riwayat pembayaran.</summary>
public class PembayaranViewModel
{
    public PembayaranViewModel(Pembayaran model, bool bisaHapus)
    {
        Model = model;
        BisaHapus = bisaHapus;
    }

    public Pembayaran Model { get; }
    public bool BisaHapus { get; }
    public string JumlahText => Fmt.Rp(Model.Jumlah);
    public string InfoText => string.IsNullOrWhiteSpace(Model.Catatan)
        ? $"{Fmt.TanggalPendek(Model.Tanggal)} · {Model.Metode}"
        : $"{Fmt.TanggalPendek(Model.Tanggal)} · {Model.Metode} · {Model.Catatan}";
}
