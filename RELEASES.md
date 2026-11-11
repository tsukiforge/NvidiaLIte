# 📋 Changelog - NVIDIA LITE

Semua pembaruan penting untuk project NVIDIA LITE akan dicatat di sini.

## [v3.0.0] - 2026-11-11

### ✨ Glass UI dan pengalaman baru

- **Glass visual baru:** panel transparan bergradasi indigo-violet dengan aksen lavender dan cyan, termasuk kartu game, navigasi, tombol, dan toast.
- **Notifikasi dan Jangan Ganggu:** toast glass untuk status dan error; preferensi DND tersimpan dan tersedia dari overlay serta system tray.
- **Library game lebih akurat:** scan library Steam dan manifest Epic, serta registry GOG/EA; tampilkan nama/platform dan ikon executable yang ditemukan.
- **Screenshot dan galeri:** tangkap desktop virtual multi-monitor, optimalkan pratinjau galeri, dan tampilkan lokasi simpan yang jelas.
- **Perekaman layar:** opsi FFmpeg eksternal untuk merekam 30 FPS ke folder Videos NVIDIA Lite; FFmpeg tidak dibundel.
- **Spotify Web API:** kontrol putar/jeda/berikutnya opsional dengan access token OAuth sementara; tidak mengklaim playback tanpa otorisasi.
- **Pengaturan lebih andal:** simpan shortcut dan pengaturan lokal, serta laporkan kontrol perangkat yang tidak didukung.
- **Project cleanup:** hapus aset Bootstrap `wwwroot` yang tidak digunakan aplikasi WPF dan perbarui README.

## [v2.0.0] - 2025-01-XX

### 🎨 Major UI Redesign - NVIDIA Industrial Red Theme

- **Complete Visual Overhaul**: Redesign total dengan NVIDIA RTX Red (#E60012) sebagai accent color
- **Industrial Design Language**: Card-based layout dengan rounded corners, sharp edges, dan professional gaming aesthetic
- **Enhanced Game Cards**: Hover effects dengan red glow, "LAUNCH" overlay, dan improved spacing
- **Modern Navigation**: Redesigned sidebar dengan 80px icons dan smooth transitions
- **Premium Sliders**: Red glow effects pada thumb, improved track styling
- **Filter Preset Buttons**: Industrial button style dengan hover states dan border animations

### 🎮 Smart Game Scanner

- **Registry-Based Detection**: Otomatis baca library Steam, Epic Games, GOG, dan EA dari Windows Registry
- **Steam Multi-Library Support**: Parse `libraryfolders.vdf` untuk detect semua Steam library locations
- **Epic Games Manifest Parser**: Baca install location dari `.item` manifest files
- **GOG Registry Integration**: Detect installed GOG games dari registry
- **Alphabetical Sorting**: Game list otomatis tersortir A-Z
- **Improved Filtering**: Enhanced skip list untuk system files dan utilities

### 🎨 Screen Filters

- **Full-Screen Color Overlay**: Real-time filter yang bekerja di atas semua aplikasi (game, browser, desktop)
- **6 Filter Presets**:
  - 🌟 **VIVID** - Warm orange tint untuk boost saturation
  - ❄️ **COOL** - Cool blue tint untuk focus
  - 🌙 **NIGHT** - Red tint untuk reduce blue light (eye comfort)
  - 🎬 **CINEMA** - Dark overlay untuk cinematic look
  - 📖 **READING** - Sepia warm tone untuk comfortable reading
  - OFF - Disable filter
- **Custom Tint System**: 7 color swatches dengan intensity slider (0-60%)
- **Click-Through Technology**: Filter tidak block mouse/keyboard input (WS_EX_TRANSPARENT)
- **Hidden from Alt+Tab**: Filter window tidak muncul di task switcher
- **Live Status Indicator**: Real-time display active filter dengan color preview

### 🔔 Welcome Notification

- **First-Run Detection**: Toast notification khusus untuk first-time users
- **Persistent Flag**: Flag file di `%LocalAppData%\NvidiaCi\.first_run`
- **Informative Messages**: Shortcut hints (Alt+Z, F10) langsung di notification
- **Subsequent Runs**: Simplified notification untuk app startup berikutnya

### 🛠️ Technical Improvements

- **Color Ambiguity Fix**: Resolved `System.Drawing.Color` vs `System.Windows.Media.Color` conflicts
- **WPF Color Alias**: Introduced `WpfColor` alias untuk cleaner code
- **Win32 API Integration**: Extended window styles untuk click-through dan Alt+Tab hiding
- **Registry Access**: Added `Microsoft.Win32` integration untuk game detection
- **Regex Parsing**: VDF dan JSON manifest parsing untuk game libraries

### 📁 Project Restructuring

- **Organized Folder Structure**:
  - `Core/` - GameItem, GameScanner, GameDataManager
  - `Helpers/` - HotkeyHelper, ScreenshotHelper, SystemControlHelper
  - `Views/` - OverlayWindow, ScreenFilterWindow
- **Improved Maintainability**: Logical separation of concerns

### 🚀 CI/CD & Release Automation

- **GitHub Actions Workflows**:
  - `dotnet-desktop.yml` - Build & Test pada push/PR
  - `release.yml` - Automated release dengan tag trigger
  - `pr-check.yml` - PR validation dengan format check
- **PowerShell Release Script**: `release-version.ps1` dengan dry-run, force-build, dan RELEASES.md validation
- **Automated Changelog**: Extract release notes dari RELEASES.md untuk GitHub Releases
- **Self-Contained Builds**: Single `.exe` dengan bundled .NET runtime
- **Version Management**: Auto-update `<Version>` dan `<AssemblyVersion>` di `.csproj`

---

## [v1.4.] - 2025-12-19

### Added

- **Sound & WiFi "Complex"**: Slider volume audio real-time (NAudio), input password WiFi, dan tombol Manage.
- **Brightness Control**: Slider pengaturan cahaya layar menggunakan WMI.
- **Real-time Hotkeys**: Sistem ganti shortcut yang langsung aktif tanpa perlu restart.
- **Gallery Fix**: Perbaikan bug penghapusan gambar menggunakan `CacheOption.OnLoad`.
- **Strict Game Scanner**: Filter file sistem (<1MB) dan prioritas folder Steam/Epic.

## [v1.2.0] - 2025-12-19

### Added

- **macOS-style Animations**: Transisi slide-in dan fade-in yang halus untuk overlay.
- **Context Menu Gallery**: Fitur klik kanan pada galeri (Open, Save As, Delete).
- **Grid Layout Dashboard**: Tampilan daftar game dalam format grid 5 kolom.
- **Improved Filter**: Pembersihan total file exe sistem (mDNS, dotnet, dll) dari daftar game.
- **Empty State**: Pesan "NO GAMES FOUND" jika scanner tidak menemukan game.

## [v1.1.0] - 2025-12-19

### Added

- **Direct Screenshot Engine**: Fitur ambil gambar menggunakan hotkey **F10**.
- **Flash Feedback Effect**: Efek kedipan layar saat mengambil screenshot.
- **Interactive Settings**: Antarmuka untuk mengubah hotkey secara visual.
- **Glass Transparency**: Efek background transparan ala Windows Game Bar.

## [v1.0.0] - 2025-12-18

### Added

- **Core Overlay System**: Integrasi WinAPI untuk hotkey Alt+Z.
- **Game Scanner**: Auto-scan folder Program Files untuk mencari game.
- **System Tray Integration**: Ikon tray dengan menu Exit dan Toggle.
- **Tabbed Navigation**: Sistem navigasi Dashboard, Gallery, dan Settings.

---

_Dibuat dengan ❤️ oleh Zi._
