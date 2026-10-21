using System.Globalization;

namespace JastipApp.Helpers;

/// <summary>Format tampilan berbahasa Indonesia (rupiah, tanggal, sisa waktu).</summary>
public static class Fmt
{
    public static readonly CultureInfo Id = new("id-ID");

    public static string Rp(decimal nilai) => "Rp " + nilai.ToString("N0", Id);

    public static string Tanggal(DateTime d) => d.ToString("ddd, d MMM yyyy, HH:mm", Id);

    public static string TanggalPendek(DateTime d) => d.ToString("d MMM yyyy, HH:mm", Id);

    /// <summary>Angka ringkas untuk grafik: 1,2 jt / 350 rb / 900.</summary>
    public static string Ringkas(decimal n) =>
        n >= 1_000_000_000 ? (n / 1_000_000_000m).ToString("0.#", Id) + " M" :
        n >= 1_000_000 ? (n / 1_000_000m).ToString("0.#", Id) + " jt" :
        n >= 1_000 ? (n / 1_000m).ToString("0.#", Id) + " rb" :
        n.ToString("0", Id);

    /// <summary>"5 menit lalu", "3 jam lalu", "2 hari lalu".</summary>
    public static string Relatif(DateTime waktu)
    {
        var s = DateTime.Now - waktu;
        if (s.TotalMinutes < 1) return "baru saja";
        if (s.TotalMinutes < 60) return $"{(int)s.TotalMinutes} menit lalu";
        if (s.TotalHours < 24) return $"{(int)s.TotalHours} jam lalu";
        if (s.TotalDays < 7) return $"{(int)s.TotalDays} hari lalu";
        return waktu.ToString("d MMM yyyy", Id);
    }

    /// <summary>
    /// Membaca harga dari teks: "38000", "38.000", "Rp 38.000", atau "38.000,50".
    /// Titik dianggap pemisah ribuan dan koma sebagai desimal (format Indonesia).
    /// </summary>
    public static bool TryParseRupiah(string? teks, out decimal nilai)
    {
        var bersih = new string((teks ?? "").Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
        return decimal.TryParse(bersih, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, Id, out nilai);
    }

    /// <summary>Angka tanpa pemisah ribuan, untuk mengisi ulang form ("38000" atau "38000,5").</summary>
    public static string AngkaPolos(decimal nilai) => nilai.ToString("0.##", Id);

    public static string SisaWaktu(DateTime deadline)
    {
        var s = deadline - DateTime.Now;
        if (s <= TimeSpan.Zero) return "sudah lewat";
        if (s.TotalMinutes < 60) return $"{(int)s.TotalMinutes} menit lagi";
        if (s.TotalHours < 24) return $"{(int)s.TotalHours} jam {s.Minutes} menit lagi";
        return $"{(int)s.TotalDays} hari {s.Hours} jam lagi";
    }
}
