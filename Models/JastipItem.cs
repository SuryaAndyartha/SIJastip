namespace JastipApp.Models;

/// <summary>Satu barang/makanan yang bisa dititip dalam sebuah event.</summary>
public class JastipItem
{
    public int Id { get; set; }
    public string Nama { get; set; } = "";
    public decimal Harga { get; set; }
    public string Catatan { get; set; } = "";
}
