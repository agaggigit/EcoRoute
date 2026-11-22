// Menempelkan daftar saran lokasi (Popup berisi DaftarSaranView) ke sebuah TextBox, cukup dengan:
//   <TextBox components:SaranLokasiBehavior.Sumber="{Binding SaranAsal}" .../>
// Popup punya jendela sendiri, jadi tampil di atas peta WebView2 dan tidak mendorong isi card.
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using EcoRoute.ViewModels;

namespace EcoRoute.Views.Components;

public static class SaranLokasiBehavior
{
    // Ruang untuk bayangan card di dalam Popup (sama dengan Margin kiri/kanan di DaftarSaranView)
    private const double RuangBayangan = 8;

    public static readonly DependencyProperty SumberProperty = DependencyProperty.RegisterAttached(
        "Sumber", typeof(SaranLokasiViewModel), typeof(SaranLokasiBehavior),
        new PropertyMetadata(null, SumberBerubah));

    private static readonly DependencyProperty PopupProperty = DependencyProperty.RegisterAttached(
        "Popup", typeof(Popup), typeof(SaranLokasiBehavior));

    public static SaranLokasiViewModel? GetSumber(DependencyObject d) => (SaranLokasiViewModel?)d.GetValue(SumberProperty);
    public static void SetSumber(DependencyObject d, SaranLokasiViewModel? value) => d.SetValue(SumberProperty, value);

    private static void SumberBerubah(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb)
            return;

        Popup popup = (Popup?)tb.GetValue(PopupProperty) ?? BuatPopup(tb);
        popup.Child.SetValue(FrameworkElement.DataContextProperty, e.NewValue);

        if (e.NewValue is SaranLokasiViewModel saran)
        {
            popup.SetBinding(Popup.IsOpenProperty, new Binding(nameof(SaranLokasiViewModel.IsTerbuka))
            {
                Source = saran,
                Mode = BindingMode.OneWay
            });
        }
        else
        {
            BindingOperations.ClearBinding(popup, Popup.IsOpenProperty);
            popup.IsOpen = false;
        }
    }

    private static Popup BuatPopup(TextBox tb)
    {
        var daftar = new DaftarSaranView();
        var popup = new Popup
        {
            Child = daftar,
            PlacementTarget = tb,
            Placement = PlacementMode.Bottom,
            HorizontalOffset = -RuangBayangan,
            AllowsTransparency = true,
            StaysOpen = true,
            Focusable = false
        };
        tb.SetValue(PopupProperty, popup);

        // Lebar daftar = lebar kolom input
        tb.SizeChanged += (_, _) => popup.Width = tb.ActualWidth + 2 * RuangBayangan;

        // Hanya ketikan user yang memunculkan saran (bukan teks yang diisi program, mis. Tukar / klik peta)
        tb.TextChanged += (_, _) =>
        {
            if (tb.IsKeyboardFocusWithin)
                _ = GetSumber(tb)?.PerbaruiAsync(tb.Text);
        };

        tb.PreviewKeyDown += (_, e) => TextBox_PreviewKeyDown(tb, popup, daftar, e);
        daftar.Daftar.PreviewKeyDown += (_, e) => Daftar_PreviewKeyDown(tb, daftar, e);

        // Fokus pindah ke luar kolom dan daftarnya -> tutup
        tb.LostKeyboardFocus += (_, e) =>
        {
            if (e.NewFocus is Visual baru && daftar.IsAncestorOf(baru))
                return;
            GetSumber(tb)?.Tutup();
        };
        daftar.Daftar.LostKeyboardFocus += (_, e) =>
        {
            if (e.NewFocus is Visual baru && (baru == tb || daftar.IsAncestorOf(baru)))
                return;
            GetSumber(tb)?.Tutup();
        };
        tb.IsVisibleChanged += (_, _) =>
        {
            if (!tb.IsVisible)
                GetSumber(tb)?.Tutup();
        };

        // Sekali per jendela (Loaded bisa terpanggil berulang)
        Window? jendelaTerpasang = null;
        tb.Loaded += (_, _) =>
        {
            if (Window.GetWindow(tb) is not { } window || window == jendelaTerpasang)
                return;
            jendelaTerpasang = window;

            // Popup WPF tidak ikut bergeser saat jendela dipindah, dan tetap di atas aplikasi lain
            // saat jendela tidak aktif -> tutup saja
            window.LocationChanged += (_, _) => GetSumber(tb)?.Tutup();
            window.Deactivated += (_, _) => GetSumber(tb)?.Tutup();

            // Klik di bagian lain jendela (termasuk area yang tidak bisa difokus) -> tutup.
            // Klik di dalam Popup tidak sampai ke sini karena Popup punya jendela sendiri.
            window.PreviewMouseDown += (_, _) =>
            {
                if (!tb.IsMouseOver)
                    GetSumber(tb)?.Tutup();
            };
        };

        return popup;
    }

    private static void TextBox_PreviewKeyDown(TextBox tb, Popup popup, DaftarSaranView daftar, KeyEventArgs e)
    {
        SaranLokasiViewModel? saran = GetSumber(tb);
        if (saran is null || !popup.IsOpen)
            return;

        switch (e.Key)
        {
            case Key.Down:
                // Pindah ke item pertama daftar
                daftar.Daftar.SelectedIndex = 0;
                daftar.Daftar.UpdateLayout();
                (daftar.Daftar.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
                e.Handled = true;
                break;

            case Key.Escape:
                saran.Tutup();
                e.Handled = true;
                break;

            case Key.Enter:
                // Enter tetap menjalankan perintah kolom (Cari / pratinjau titik awal); daftarnya ditutup
                saran.Tutup();
                break;
        }
    }

    private static void Daftar_PreviewKeyDown(TextBox tb, DaftarSaranView daftar, KeyEventArgs e)
    {
        SaranLokasiViewModel? saran = GetSumber(tb);
        if (saran is null)
            return;

        switch (e.Key)
        {
            case Key.Enter when daftar.Daftar.SelectedItem is { } place:
                saran.PilihCommand.Execute(place);
                KembaliKeKolom(tb);
                e.Handled = true;
                break;

            case Key.Escape:
                saran.Tutup();
                KembaliKeKolom(tb);
                e.Handled = true;
                break;

            case Key.Up when daftar.Daftar.SelectedIndex <= 0:
                KembaliKeKolom(tb);
                e.Handled = true;
                break;
        }
    }

    private static void KembaliKeKolom(TextBox tb)
    {
        tb.Focus();
        tb.CaretIndex = tb.Text.Length;
    }
}
