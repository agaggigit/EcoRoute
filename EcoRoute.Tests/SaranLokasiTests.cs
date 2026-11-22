using System.Text.Json;
using EcoRoute.Models;
using EcoRoute.Services;
using EcoRoute.ViewModels;

namespace EcoRoute.Tests;

// Saran lokasi saat mengetik. Sumber online diganti palsu (tanpa jaringan) dan tanpa jeda.
public class SaranLokasiTests
{
    private static readonly Place MalioboroMall =
        new("Malioboro Mall", "Mataram 31, Suryatmajan, Yogyakarta", "Mall", -7.7933434, 110.3666525);

    private sealed class OnlinePalsu
    {
        public List<string> Kueri { get; } = [];
        public IReadOnlyList<Place> Hasil { get; set; } = [MalioboroMall];
        public Exception? Gagal { get; set; }
        public TaskCompletionSource? Tahan { get; set; }

        public async Task<IReadOnlyList<Place>> CariAsync(string teks, CancellationToken ct)
        {
            Kueri.Add(teks);
            if (Tahan is not null)
                await Tahan.Task.WaitAsync(ct);
            if (Gagal is not null)
                throw Gagal;
            return Hasil;
        }
    }

    private static (MainViewModel Vm, OnlinePalsu Online) BuatViewModel(AppState state = AppState.StateC_InputRoute)
    {
        var online = new OnlinePalsu();
        var vm = new MainViewModel(userId: 1, cariSaranOnline: online.CariAsync, jedaSaran: TimeSpan.Zero)
        {
            CurrentState = state
        };
        return (vm, online);
    }

    [Fact]
    public async Task DuaHuruf_HanyaTempatPopulerTanpaOnline()
    {
        var (vm, online) = BuatViewModel();

        await vm.SaranAsal.PerbaruiAsync("ma");

        Assert.True(vm.SaranAsal.IsTerbuka);
        Assert.Equal("Malioboro", vm.SaranAsal.Daftar[0].Nama);
        Assert.Empty(online.Kueri);
    }

    [Fact]
    public async Task TigaHuruf_PopulerDuluLaluHasilOnlineTanpaDuplikat()
    {
        var (vm, online) = BuatViewModel();
        Place malioboro = vm.TempatPopuler.Single(p => p.Nama == "Malioboro");
        // Online juga mengembalikan Malioboro (lokasi sama) -> tidak boleh muncul dua kali
        online.Hasil = [malioboro with { Alamat = "Jalan Malioboro" }, MalioboroMall];

        await vm.SaranAsal.PerbaruiAsync("malio");

        Assert.Equal(["malio"], online.Kueri);
        Assert.Equal(["Malioboro", "Malioboro Mall"], vm.SaranAsal.Daftar.Select(p => p.Nama));
    }

    [Fact]
    public async Task JumlahSaranDibatasi()
    {
        var (vm, online) = BuatViewModel();
        online.Hasil = Enumerable.Range(1, 10)
            .Select(i => new Place($"Tempat {i}", "Yogyakarta", "Lokasi", -7.7 - i * 0.01, 110.3))
            .ToList();

        await vm.SaranAsal.PerbaruiAsync("tempat");

        Assert.Equal(SaranLokasiViewModel.MaksSaran, vm.SaranAsal.Daftar.Count);
    }

    [Fact]
    public async Task HasilOnlineLama_DibuangSaatTeksBerubah()
    {
        var (vm, online) = BuatViewModel();
        online.Tahan = new TaskCompletionSource();

        Task lama = vm.SaranAsal.PerbaruiAsync("malio");
        online.Tahan = null;
        await vm.SaranAsal.PerbaruiAsync("malioboro m");
        await lama;

        Assert.Equal(["malio", "malioboro m"], online.Kueri);
        Assert.Equal(1, vm.SaranAsal.Daftar.Count(p => p.Nama == "Malioboro Mall"));
    }

    [Fact]
    public async Task OnlineGagal_SaranLokalTetapTanpaNotifikasi()
    {
        var (vm, online) = BuatViewModel();
        online.Gagal = new RoutingException("Tidak bisa terhubung ke server peta.");

        await vm.SaranAsal.PerbaruiAsync("tugu");

        Assert.True(vm.SaranAsal.IsTerbuka);
        Assert.Equal("Tugu Yogyakarta", vm.SaranAsal.Daftar.Single().Nama);
        Assert.Empty(vm.Notifikasi);
    }

    [Fact]
    public async Task TeksTerlaluPendek_DaftarTertutup()
    {
        var (vm, _) = BuatViewModel();
        await vm.SaranAsal.PerbaruiAsync("tugu");

        await vm.SaranAsal.PerbaruiAsync("t");

        Assert.False(vm.SaranAsal.IsTerbuka);
        Assert.Empty(vm.SaranAsal.Daftar);
    }

    [Fact]
    public async Task PilihSaranAsal_JadiPratinjauDanDaftarTidakTerbukaLagi()
    {
        var (vm, _) = BuatViewModel();
        await vm.SaranAsal.PerbaruiAsync("malio");

        vm.SaranAsal.PilihCommand.Execute(MalioboroMall);
        // TextBox mengirim teks hasil pilihan kembali sebagai ketikan
        await vm.SaranAsal.PerbaruiAsync(vm.TitikAsal);

        Assert.Equal("Malioboro Mall", vm.TitikAsal);
        Assert.Same(MalioboroMall, vm.PratinjauAsal);
        Assert.False(vm.SaranAsal.IsTerbuka);
    }

    [Fact]
    public async Task PilihSaranTujuan_DipakaiSampaiTeksnyaDiubah()
    {
        var (vm, _) = BuatViewModel();
        await vm.SaranTujuan.PerbaruiAsync("malio");

        vm.SaranTujuan.PilihCommand.Execute(MalioboroMall);

        Assert.Equal("Malioboro Mall", vm.TitikTujuan);
        Assert.Same(MalioboroMall, vm.TujuanTerpilih);

        vm.TitikTujuan = "Malioboro Mall lantai 2";
        Assert.Null(vm.TujuanTerpilih);
    }

    [Fact]
    public async Task PilihSaranCari_MembukaKartuTempat()
    {
        var (vm, _) = BuatViewModel(AppState.StateA_Idle);
        await vm.SaranCari.PerbaruiAsync("malio");

        vm.SaranCari.PilihCommand.Execute(MalioboroMall);

        Assert.Equal(AppState.StateB_SearchLocation, vm.CurrentState);
        Assert.Same(MalioboroMall, vm.SelectedPlace);
        Assert.Equal("Malioboro Mall", vm.SearchQuery);
    }

    [Fact]
    public async Task GantiState_MenutupDaftar()
    {
        var (vm, _) = BuatViewModel();
        await vm.SaranTujuan.PerbaruiAsync("tugu");

        vm.CurrentState = AppState.StateA_Idle;

        Assert.False(vm.SaranTujuan.IsTerbuka);
    }

    [Fact]
    public void Tukar_LokasiTerpilihIkutBertukar()
    {
        var (vm, _) = BuatViewModel();
        Place tugu = vm.TempatPopuler.Single(p => p.Nama == "Tugu Yogyakarta");
        vm.SaranAsal.PilihCommand.Execute(MalioboroMall);
        vm.SaranTujuan.PilihCommand.Execute(tugu);

        vm.TukarCommand.Execute(null);

        Assert.Equal("Tugu Yogyakarta", vm.TitikAsal);
        Assert.Same(tugu, vm.PratinjauAsal);
        Assert.Equal("Malioboro Mall", vm.TitikTujuan);
        Assert.Equal(MalioboroMall, vm.TujuanTerpilih);
    }

    [Fact]
    public void ParsePhoton_KoordinatLonLatDanAlamat()
    {
        const string json = """
            {"type":"FeatureCollection","features":[
              {"type":"Feature",
               "properties":{"osm_value":"mall","name":"Malioboro Mall","street":"Mataram","housenumber":"31",
                             "district":"Suryatmajan","city":"Yogyakarta","state":"Daerah Istimewa Yogyakarta"},
               "geometry":{"type":"Point","coordinates":[110.3666525,-7.7933434]}},
              {"type":"Feature",
               "properties":{"osm_value":"residential","street":"Jalan Kaliurang","city":"Sleman"},
               "geometry":{"type":"Point","coordinates":[110.38,-7.75]}},
              {"type":"Feature","properties":{"osm_value":"yes"},
               "geometry":{"type":"Point","coordinates":[110.0,-7.0]}}
            ]}
            """;
        using JsonDocument doc = JsonDocument.Parse(json);

        IReadOnlyList<Place> hasil = RoutingService.ParsePhoton(doc);

        Assert.Equal(2, hasil.Count); // tanpa nama & jalan dilewati
        Assert.Equal("Malioboro Mall", hasil[0].Nama);
        Assert.Equal("Mataram 31, Suryatmajan, Yogyakarta, Daerah Istimewa Yogyakarta", hasil[0].Alamat);
        Assert.Equal("Mall", hasil[0].Kategori);
        Assert.Equal(-7.7933434, hasil[0].Lat, precision: 6);
        Assert.Equal(110.3666525, hasil[0].Lng, precision: 6);
        Assert.Equal("Jalan Kaliurang", hasil[1].Nama);
        Assert.Equal("Sleman", hasil[1].Alamat);
    }
}
