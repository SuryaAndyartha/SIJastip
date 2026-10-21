using System.Text.Json;
using Avalonia;
using Avalonia.Styling;
using JastipApp.Data;
using JastipApp.Helpers;

namespace JastipApp.Services;

/// <summary>Preferensi perangkat (saat ini: tema terang/gelap), disimpan di pengaturan.json.</summary>
public static class PengaturanService
{
    private class Data
    {
        public bool TemaGelap { get; set; }
    }

    private static string FilePath => Path.Combine(JastipDbContext.Folder, "pengaturan.json");

    public static bool TemaGelap { get; private set; }

    /// <summary>Dipanggil setiap tema berubah, agar semua toggle tema ikut sinkron.</summary>
    public static event Action? TemaBerubah;

    public static void Muat()
    {
        try
        {
            if (File.Exists(FilePath))
                TemaGelap = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath))?.TemaGelap ?? false;
        }
        catch (Exception ex)
        {
            Log.Error("Membaca pengaturan.json", ex);
        }
        Terapkan();
    }

    public static void SetTemaGelap(bool gelap)
    {
        if (TemaGelap == gelap) return;
        TemaGelap = gelap;
        Terapkan();
        TemaBerubah?.Invoke();
        try
        {
            Directory.CreateDirectory(JastipDbContext.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new Data { TemaGelap = gelap }));
        }
        catch (Exception ex)
        {
            Log.Error("Menyimpan pengaturan.json", ex);
        }
    }

    private static void Terapkan()
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = TemaGelap ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}
