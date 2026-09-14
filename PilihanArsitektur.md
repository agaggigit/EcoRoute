MVVM + Services

EcoRoute/
├── EcoRoute.sln
└── EcoRoute/
    ├── EcoRoute.csproj
    │
    ├── Models/                       <-- [MODEL LAYER / OOP DOMAIN]
    │   ├── Vehicle.cs                <-- Abstract Base Class (Wajib OOP)
    │   ├── Motorcycle.cs             <-- Class Turunan (Inheritance/Polymorphism)
    │   ├── Car.cs                    <-- Class Turunan
    │   ├── Bus.cs                    <-- Class Turunan
    │   ├── Train.cs                  <-- Class Turunan
    │   ├── RouteResult.cs            <-- Data model hasil rute & emisi
    │   └── AppState.cs               <-- Enum State A, B, C, D, L, E
    │
    ├── Services/                     <-- [SERVICE LAYER / DATA ACCESS]
    │   ├── RoutingService.cs         <-- REST API (HttpClient) panggil API Peta
    │   └── DatabaseService.cs        <-- PostgreSQL (Npgsql) simpan & ambil riwayat
    │
    ├── ViewModels/                   <-- [VIEWMODEL LAYER / UI LOGIC]
    │   ├── BaseViewModel.cs          <-- Implementasi INotifyPropertyChanged
    │   ├── MainViewModel.cs          <-- ViewModel utama mengatur State A, B, C, D, L, E
    │   └── RelayCommand.cs           <-- ICommand helper untuk aksi tombol XAML
    │
    ├── Views/                        <-- [VIEW LAYER / PRESENTATION XAML]
    │   ├── MainWindow.xaml           <-- Window utama (Split Panel Kiri & Peta Kanan)
    │   ├── Components/               <-- Sub-tampilan/UserControl per State
    │   │   ├── IdleView.xaml         <-- State A & B
    │   │   ├── RouteInputView.xaml   <-- State C & L
    │   │   └── EmissionResultView.xaml <-- State D & E
    │   └── MapView.xaml              <-- Wadah WebView2 Peta Interaktif
    │
    └── App.xaml                      <-- Entry Point Aplikasi

Kenapa:
1. Memenuhi Syarat Aplikasi Mandiri (.exe).
2. Pemenuhan Nilai PBO dengan prinsip High Cohesion, Low Coupling, Inheritance, dan Polymorphism.
3. Kemudahan Pengelolaan State Tampilan karena MVVM memungkinkan pengikatan data dinamis.
4. Pembuatan lebih cepat dibanding arsitektur lainnya.
5. Scope aplikasi tidak terlalu luas (hanya desktop app windows saja).