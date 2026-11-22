
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
            // Simpan data WebView2 di %LOCALAPPDATA% agar selalu bisa ditulis,
            // termasuk saat app dijalankan lewat "dotnet EcoRoute.dll"
            // (default-nya di samping exe host, misal C:\Program Files\dotnet).
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                ),
                "EcoRoute",
                "WebView2"
            );

            CoreWebView2Environment environment =
                await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder
                );

            await MapBrowser.EnsureCoreWebView2Async(environment);

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
                await SendPopularPlacesAsync();
                await SynchronizeMapAsync();
                return;
            }

            // Klik pin tempat populer -> jadi lokasi terpilih (State B)
            if (messageType == "placeClicked")
            {
                if (_viewModel == null ||
                    (_viewModel.CurrentState != AppState.StateA_Idle &&
                     _viewModel.CurrentState != AppState.StateB_SearchLocation))
                {
                    return;
                }

                string? nama = root.GetProperty("nama").GetString();
                Place? place = _viewModel.TempatPopuler
                    .FirstOrDefault(p => p.Nama == nama);

                if (place != null &&
                    _viewModel.PilihTempatCommand.CanExecute(place))
                {
                    _viewModel.PilihTempatCommand.Execute(place);
                }
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

                // Penanda sudah digambar map.html di titik klik, jadi peta tidak perlu digeser lagi
                _asalDariKlikPeta = true;
                _viewModel.PilihAsalDariPeta(lat, lng);
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

        if (e.PropertyName == nameof(
                MainViewModel.KategoriAktif))
        {
            await UpdateCategoryFilterAsync();
        }

        if (e.PropertyName == nameof(
                MainViewModel.HasilRute))
        {
            await UpdateRouteAsync();
        }

        if (e.PropertyName == nameof(
                MainViewModel.PratinjauAsal))
        {
            await UpdateOriginPreviewAsync();
        }
    }

    private async Task SynchronizeMapAsync()
    {
        await UpdateMarkerAsync();
        await UpdateMapStateAsync();
        await UpdateCategoryFilterAsync();
        await UpdateRouteAsync();
        await UpdateOriginPreviewAsync();
    }

    // true kalau pratinjau berikutnya berasal dari klik peta (penanda sudah ada, peta tidak digeser)
    private bool _asalDariKlikPeta;

    // Penanda "Titik Asal" saat input rute (State C), sebelum Hitung Emisi
    private async Task UpdateOriginPreviewAsync()
    {
        bool dariKlikPeta = _asalDariKlikPeta;
        _asalDariKlikPeta = false;

        // Di luar State C penanda asal diatur oleh garis rute / UpdateMapStateAsync
        if (!_mapReady ||
            MapBrowser.CoreWebView2 == null ||
            _viewModel?.CurrentState != AppState.StateC_InputRoute)
        {
            return;
        }

        try
        {
            Place? asal = _viewModel.PratinjauAsal;

            if (asal == null)
            {
                await MapBrowser.CoreWebView2
                    .ExecuteScriptAsync("clearOrigin();");
                return;
            }

            string originData = JsonSerializer.Serialize(
                new
                {
                    lat = asal.Lat,
                    lng = asal.Lng,
                    nama = asal.Nama,
                    pan = !dariKlikPeta
                }
            );

            await MapBrowser.CoreWebView2
                .ExecuteScriptAsync($"showOriginPreview({originData});");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal menampilkan pratinjau titik awal: {ex.Message}"
            );
        }
    }

    // Gambar garis rute hasil OSRM (State D), atau hapus kalau HasilRute null
    private async Task UpdateRouteAsync()
    {
        if (!_mapReady ||
            MapBrowser.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            RouteResult? rute = _viewModel?.HasilRute;

            if (rute == null)
            {
                await MapBrowser.CoreWebView2
                    .ExecuteScriptAsync("clearRoute();");
                return;
            }

            // Segmen kemacetan hanya dikirim untuk rute jalan raya dan jalur trayek bus dengan data live
            // dari TomTom (jalur bus punya data kemacetannya sendiri, indeksnya mengacu ke titik trayek).
            // Rute sepeda / jalan kaki, atau kemacetan hasil tebakan jam -> garis satu warna.
            KondisiLaluLintas? laluLintas = rute.Profil == ProfilRute.TrayekBus
                ? _viewModel?.LaluLintasBus
                : _viewModel?.LaluLintas;
            var segments = laluLintas is { Live: true } && rute.Profil is ProfilRute.JalanRaya or ProfilRute.TrayekBus
                ? laluLintas.Segmen.Select(s => new
                {
                    from = s.IndeksAwal,
                    to = s.IndeksAkhir,
                    level = s.Tingkat.ToString(),
                    ratio = s.RasioKecepatan,
                    live = s.AdaDataLive
                }).ToList()
                : null;

            // Bus lewat trayek Trans Jogja: garis bus + jalan kaki ke / dari halte + penanda halte
            RuteBus? bus = rute.Bus;
            var busData = bus == null
                ? null
                : new
                {
                    label = $"Trans Jogja {bus.KodeTrayek}",
                    halteNaik = new { lat = bus.HalteNaik.Lat, lng = bus.HalteNaik.Lng, nama = bus.HalteNaik.Nama },
                    halteTurun = new { lat = bus.HalteTurun.Lat, lng = bus.HalteTurun.Lng, nama = bus.HalteTurun.Nama }
                };

            string routeData = JsonSerializer.Serialize(
                new
                {
                    points = rute.Titik,
                    segments,
                    bus = busData,
                    origin = new { lat = rute.Asal.Lat, lng = rute.Asal.Lng, nama = rute.Asal.Nama },
                    destination = new { lat = rute.Tujuan.Lat, lng = rute.Tujuan.Lng, nama = rute.Tujuan.Nama }
                }
            );

            await MapBrowser.CoreWebView2
                .ExecuteScriptAsync($"showRoute({routeData});");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal menggambar rute: {ex.Message}"
            );
        }
    }

    private async Task SendPopularPlacesAsync()
    {
        if (!_mapReady ||
            MapBrowser.CoreWebView2 == null ||
            _viewModel == null)
        {
            return;
        }

        try
        {
            string placesData = JsonSerializer.Serialize(
                _viewModel.TempatPopuler.Select(p => new
                {
                    nama = p.Nama,
                    kategori = p.Kategori,
                    lat = p.Lat,
                    lng = p.Lng
                })
            );

            await MapBrowser.CoreWebView2
                .ExecuteScriptAsync(
                    $"setPopularPlaces({placesData});"
                );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal mengirim tempat populer: {ex.Message}"
            );
        }
    }

    private async Task UpdateCategoryFilterAsync()
    {
        if (!_mapReady ||
            MapBrowser.CoreWebView2 == null)
        {
            return;
        }

        try
        {
            // Serialize supaya jadi string JS yang aman ("Wisata" atau null)
            string category = JsonSerializer.Serialize(
                _viewModel?.KategoriAktif
            );

            await MapBrowser.CoreWebView2
                .ExecuteScriptAsync(
                    $"setCategoryFilter({category});"
                );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal memperbarui filter kategori: {ex.Message}"
            );
        }
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

            // Pin tempat populer hanya di State A & B,
            // supaya peta bersih saat input & hasil rute.
            bool showPopular =
                _viewModel?.CurrentState is
                    AppState.StateA_Idle or
                    AppState.StateB_SearchLocation;

            await MapBrowser.CoreWebView2
                .ExecuteScriptAsync(
                    $"setPopularVisible({(showPopular ? "true" : "false")});"
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
