using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JastipApp.Helpers;
using JastipApp.Models;

namespace JastipApp.ViewModels;

/// <summary>Satu baris barang di panel detail, lengkap dengan jumlah yang dipesan.</summary>
public partial class ItemViewModel : ObservableObject
{
    private readonly Action _onChanged;
    private readonly Func<bool> _bisaPesan;

    public ItemViewModel(JastipItem item, Action onChanged, Func<bool> bisaPesan)
    {
        Item = item;
        _onChanged = onChanged;
        _bisaPesan = bisaPesan;
    }

    public JastipItem Item { get; }
    public string Nama => Item.Nama;
    public string Catatan => Item.Catatan;
    public string HargaText => Fmt.Rp(Item.Harga);

    public decimal Subtotal => Item.Harga * Qty;
    public bool CanOrder => _bisaPesan();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private int _qty;

    partial void OnQtyChanged(int value) => _onChanged();

    public void Refresh() => OnPropertyChanged(nameof(CanOrder));

    [RelayCommand]
    private void Tambah()
    {
        if (CanOrder && Qty < 99) Qty++;
    }

    [RelayCommand]
    private void Kurang()
    {
        if (Qty > 0) Qty--;
    }
}
