using Avalonia.Controls;
using Avalonia.Interactivity;
using JastipApp.Helpers;
using JastipApp.ViewModels;

namespace JastipApp.Views;

public partial class PesananView : UserControl
{
    public PesananView()
    {
        InitializeComponent();
    }

    // Clipboard milik jendela, jadi ditangani di code-behind. Logika lain ada di PesananPageViewModel.
    private async void OnSalinRingkasan(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PesananViewModel p }) return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(p.Ringkasan);
            UiBus.Info($"Ringkasan {p.Kode} tersalin.");
        }
    }
}
