
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace EcoRoute.Views.Components;

public partial class MapView : UserControl
{
    private bool _mapInitialized;

    public MapView()
    {
        InitializeComponent();
        Loaded += MapView_Loaded;
    }

    private async void MapView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_mapInitialized)
            return;

        try
        {
            await MapBrowser.EnsureCoreWebView2Async();

            string mapsFolder = Path.Combine(
                AppContext.BaseDirectory,
                "Maps"
            );

            MapBrowser.CoreWebView2
                .SetVirtualHostNameToFolderMapping(
                    "ecoroute.local",
                    mapsFolder,
                    CoreWebView2HostResourceAccessKind.DenyCors
                );

            MapBrowser.CoreWebView2.Navigate(
                "https://ecoroute.local/map.html"
            );

            _mapInitialized = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Gagal memuat peta: {ex.Message}",
                "EcoRoute",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }
}
