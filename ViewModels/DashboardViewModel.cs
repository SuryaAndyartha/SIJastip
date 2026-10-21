using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using JastipApp.Helpers;
using JastipApp.Models;
using JastipApp.Services;

namespace JastipApp.ViewModels;

/// <summary>Kartu statistik di dashboard.</summary>
public record KartuStat(string Judul, string Nilai, string Keterangan, IBrush Aksen);

/// <summary>Satu batang grafik. Tinggi/lebar dihitung relatif terhadap nilai terbesar.</summary>
public class BatangGrafik
{
    public BatangGrafik(string label, decimal nilai, decimal maks, double ukuranPenuh, string nilaiText, IBrush warna)
    {
        Label = label;
        NilaiText = nilaiText;
        Warna = warna;
        Ukuran = maks <= 0 ? 0 : Math.Max(nilai > 0 ? 3 : 0, (double)(nilai / maks) * ukuranPenuh);
    }

    public string Label { get; }
    public string NilaiText { get; }
    public double Ukuran { get; }
    public IBrush Warna { get; }
}

public class AktivitasItem
{
    public AktivitasItem(Aktivitas a)
    {
        Keterangan = a.Keterangan;
        Info = $"{a.NamaPengguna} · {Fmt.Relatif(a.Waktu)}";
        Jenis = a.Jenis;
    }

    public string Keterangan { get; }
    public string Info { get; }
    public string Jenis { get; }
}

/// <summary>Dashboard & laporan ringkas. Semua angka dihitung dari database lewat DashboardService.</summary>
public partial class DashboardViewModel : ObservableObject
{
    private static readonly IBrush Teal = Brush.Parse("#0F6B6B");
    private static readonly IBrush Biru = Brush.Parse("#2563EB");
    private static readonly IBrush Kuning = Brush.Parse("#B45309");
    private static readonly IBrush Hijau = Brush.Parse("#0F7B5F");
    private static readonly IBrush Merah = Brush.Parse("#9F1239");
    private static readonly IBrush Ungu = Brush.Parse("#7C3AED");
    private static readonly IBrush Abu = Brush.Parse("#6B7280");

    private readonly DashboardService _service = new();
    private readonly Pengguna _user = Sesi.Wajib;

    public DashboardViewModel()
    {
        _ = MuatAsync();
    }

    public string Sapaan => $"Halo, {_user.Nama}";
    public bool IsPemesan => _user.Peran == Peran.Pemesan;
    public string Subjudul => _user.Peran switch
    {
        Peran.Admin => "Ringkasan seluruh event, pesanan, dan pembayaran.",
        Peran.Penyedia => "Ringkasan event dan pesanan milikmu.",
        _ => "Ringkasan pesanan dan tagihanmu."
    };

    public string[] PilihanPeriode { get; } = { "7 hari terakhir", "30 hari terakhir", "Semua waktu" };

    [ObservableProperty] private string _periode = "7 hari terakhir";
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _judulGrafik = "";

    public ObservableCollection<KartuStat> Kartu { get; } = new();
    public ObservableCollection<BatangGrafik> GrafikHarian { get; } = new();
    public ObservableCollection<BatangGrafik> GrafikStatus { get; } = new();
    public ObservableCollection<AktivitasItem> Aktivitas { get; } = new();

    [ObservableProperty] private bool _aktivitasKosong;
    [ObservableProperty] private bool _grafikKosong;

    partial void OnPeriodeChanged(string value) => _ = MuatAsync();

    private async Task MuatAsync()
    {
        IsLoading = true;
        try
        {
            int? hari = Periode switch { "7 hari terakhir" => 7, "30 hari terakhir" => 30, _ => null };
            var d = await _service.HitungAsync(hari);

            Kartu.Clear();
            if (IsPemesan)
            {
                Kartu.Add(new("Pesanan", d.TotalPesanan.ToString(), $"{d.PesananSelesai} selesai", Teal));
                Kartu.Add(new("Sedang berjalan", d.PesananBerjalan.ToString(), "diterima, dibeli, atau dikirim", Biru));
                Kartu.Add(new("Total belanja", Fmt.Rp(d.Omzet), "barang + jasa + ongkir", Ungu));
                Kartu.Add(new("Sudah dibayar", Fmt.Rp(d.Terbayar), "semua pembayaran tercatat", Hijau));
                Kartu.Add(new("Sisa tagihan", Fmt.Rp(d.Piutang), d.Piutang > 0 ? "segera lunasi ke penyedia" : "semua lunas", Merah));
                Kartu.Add(new("Event dibuka", d.EventDibuka.ToString(), "masih bisa dipesan", Kuning));
            }
            else
            {
                Kartu.Add(new("Total pesanan", d.TotalPesanan.ToString(), $"{d.PesananSelesai} selesai", Teal));
                Kartu.Add(new("Sedang berjalan", d.PesananBerjalan.ToString(), "perlu dibeli / dikirim", Biru));
                Kartu.Add(new("Omzet", Fmt.Rp(d.Omzet), "total tagihan pesanan", Ungu));
                Kartu.Add(new("Keuntungan", Fmt.Rp(d.Keuntungan), "dari biaya jasa", Hijau));
                Kartu.Add(new("Piutang", Fmt.Rp(d.Piutang), $"terbayar {Fmt.Rp(d.Terbayar)}", Merah));
                Kartu.Add(new("Event dibuka", d.EventDibuka.ToString(), "masih menerima pesanan", Kuning));
            }

            JudulGrafik = IsPemesan ? "Belanja per hari" : "Omzet per hari";
            GrafikHarian.Clear();
            var maksHari = d.PerHari.Count == 0 ? 0 : d.PerHari.Max(t => t.Nilai);
            foreach (var t in d.PerHari)
                GrafikHarian.Add(new BatangGrafik(t.Label, t.Nilai, maksHari, 150,
                    t.Nilai == 0 ? "" : Fmt.Ringkas(t.Nilai), Teal));
            GrafikKosong = maksHari == 0;

            GrafikStatus.Clear();
            var maksStatus = d.PerStatus.Count == 0 ? 0 : d.PerStatus.Max(t => t.Nilai);
            var warna = new[] { Biru, Kuning, Ungu, Hijau, Abu };
            for (int i = 0; i < d.PerStatus.Count; i++)
                GrafikStatus.Add(new BatangGrafik(d.PerStatus[i].Label, d.PerStatus[i].Nilai, maksStatus, 260,
                    d.PerStatus[i].Nilai.ToString("0"), warna[i % warna.Length]));

            Aktivitas.Clear();
            foreach (var a in d.AktivitasTerbaru) Aktivitas.Add(new AktivitasItem(a));
            AktivitasKosong = Aktivitas.Count == 0;
        }
        catch (Exception ex)
        {
            UiBus.Error(UiBus.PesanError(ex, "Memuat dashboard"));
        }
        finally
        {
            IsLoading = false;
        }
    }
}
