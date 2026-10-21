using Avalonia.Controls;
using Avalonia.Interactivity;
using JastipApp.Helpers;
using JastipApp.ViewModels;

namespace JastipApp.Views;

public partial class EventView : UserControl
{
    public EventView()
    {
        InitializeComponent();
    }

    // Code-behind hanya mengurus clipboard (milik jendela). Logika pesanan ada di ViewModel/Service.
    private async void OnKirimPesanan(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: EventViewModel ev } || DataContext is not EventPageViewModel vm) return;

        var ringkasan = await vm.KirimPesananAsync(ev);
        if (ringkasan is not null && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(ringkasan);
    }

    private async void OnSalinRingkasan(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: PesananViewModel p }) return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(p.Ringkasan);
            UiBus.Info("Ringkasan pesanan tersalin.");
        }
    }
}
