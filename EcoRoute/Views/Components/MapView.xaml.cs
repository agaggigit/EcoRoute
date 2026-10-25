
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using EcoRoute.ViewModels;
using Microsoft.Web.WebView2.Core;
using EcoRoute.Models;

namespace EcoRoute.Views.Components;

public partial class MapView : UserControl
{
    private bool _mapInitialized;
    private bool _mapReady;

    private MainViewModel? _viewModel;

    public MapView()
    {
        InitializeComponent();

        Loaded += MapView_Loaded;
        DataContextChanged += MapView_DataContextChanged;
        Unloaded += MapView_Unloaded;
    }

    private void MapView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();

        _viewModel = e.NewValue as MainViewModel;

        AttachViewModel();

        _ = SynchronizeMapAsync();
    }

    private void AttachViewModel()
    {
        if (_viewModel == null)
            return;

        _viewModel.PropertyChanged -=
            ViewModel_PropertyChanged;

        _viewModel.PropertyChanged +=
            ViewModel_PropertyChanged;
    }

    private void DetachViewModel()
    {
        if (_viewModel == null)
            return;

        _viewModel.PropertyChanged -=
            ViewModel_PropertyChanged;
    }

    private async void MapView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        AttachViewModel();

        if (_mapInitialized)
        {
            await SynchronizeMapAsync();
            return;
        }

        try
        {
            await MapBrowser.EnsureCoreWebView2Async();

            MapBrowser.CoreWebView2.WebMessageReceived +=
                MapBrowser_WebMessageReceived;

            string mapsFolder = Path.Combine(
                AppContext.BaseDirectory,
                "Maps"
            );

            if (!Directory.Exists(mapsFolder))
            {
                throw new DirectoryNotFoundException(
                    $"Folder peta tidak ditemukan: {mapsFolder}"
                );
            }

            MapBrowser.CoreWebView2
                .SetVirtualHostNameToFolderMapping(
                    "ecoroute.local",
                    mapsFolder,
                    CoreWebView2HostResourceAccessKind.DenyCors
                );

            _mapInitialized = true;

            MapBrowser.CoreWebView2.Navigate(
                "https://ecoroute.local/map.html"
            );
        }
        catch (Exception ex)
        {
            _mapInitialized = false;

            MessageBox.Show(
                $"Gagal memuat peta: {ex.Message}",
                "EcoRoute",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private async void MapBrowser_WebMessageReceived(
        object? sender,
        CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using JsonDocument message =
                JsonDocument.Parse(e.WebMessageAsJson);

            JsonElement root = message.RootElement;

            if (!root.TryGetProperty(
                "type", out JsonElement typeElement))
            {
                return;
            }

            string? messageType = typeElement.GetString();

            if (messageType == "mapReady")
            {
                _mapReady = true;
                await SynchronizeMapAsync();
                return;
            }

            if (messageType == "mapClicked")
            {
                if (_viewModel == null ||
                    _viewModel.CurrentState !=
                        AppState.StateC_InputRoute)
                {
                    return;
                }

                double lat = root.GetProperty(
                    "lat"
                ).GetDouble();

                double lng = root.GetProperty(
                    "lng"
                ).GetDouble();

                string latitude = lat.ToString(
                    "F6",
                    CultureInfo.InvariantCulture
                );

                string longitude = lng.ToString(
                    "F6",
                    CultureInfo.InvariantCulture
                );

                _viewModel.TitikAsal =
                    $"{latitude}, {longitude}";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Pesan peta gagal diproses: {ex.Message}"
            );
        }
    }

    private async void ViewModel_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(
                MainViewModel.SelectedPlace))
        {
            await UpdateMarkerAsync();
        }

        if (e.PropertyName == nameof(
                MainViewModel.CurrentState))
        {
            await UpdateMapStateAsync();
        }
    }

    private async Task SynchronizeMapAsync()
    {
        await UpdateMarkerAsync();
        await UpdateMapStateAsync();
    }

    private async Task UpdateMarkerAsync()
    {
        if (!_mapReady ||
            MapBrowser.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            var place = _viewModel?.SelectedPlace;

            if (place == null)
            {
                await MapBrowser.CoreWebView2
                    .ExecuteScriptAsync(
                        "clearLocation();"
                    );
                return;
            }

            string locationData = JsonSerializer.Serialize(
                new
                {
                    lat = place.Lat,
                    lng = place.Lng,
                    nama = place.Nama
                }
            );

            await MapBrowser.CoreWebView2
                .ExecuteScriptAsync(
                    $"showLocationFromWpf({locationData});"
                );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal memperbarui marker: {ex.Message}"
            );
        }
    }

    private async Task UpdateMapStateAsync()
    {
        if (!_mapReady ||
            MapBrowser.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            bool isInputRoute =
                _viewModel?.CurrentState ==
                    AppState.StateC_InputRoute;

            string enabled =
                isInputRoute ? "true" : "false";

            await MapBrowser.CoreWebView2
                .ExecuteScriptAsync(
                    $"setOriginSelectionEnabled({enabled});"
                );

            if (!isInputRoute)
            {
                // Jangan hapus marker saat sedang
                // menampilkan hasil rute atau loading.
                bool isBackToSearch =
                    _viewModel?.CurrentState ==
                        AppState.StateA_Idle ||
                    _viewModel?.CurrentState ==
                        AppState.StateB_SearchLocation;

                if (isBackToSearch)
                {
                    await MapBrowser.CoreWebView2
                        .ExecuteScriptAsync(
                            "clearOrigin();"
                        );
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal memperbarui state peta: {ex.Message}"
            );
        }
    }

    private void MapView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        DetachViewModel();
    }
}
