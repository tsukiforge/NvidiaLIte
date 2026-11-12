# NVIDIA Lite

NVIDIA Lite adalah overlay gaming Windows berbasis WPF yang ringan, dengan panel glass transparan bernuansa hitam dan putih, navigasi cepat, serta notifikasi glass.

## Fitur

- **Library game:** membaca Minecraft Launcher, instalasi Roblox, Steam (`libraryfolders.vdf` dan `appmanifest`), manifest Epic Games, registry GOG/EA, dan folder lokal `C:\Games`, `D:\Games`, serta `E:\Games`. Library dipindai ulang saat overlay pertama kali dibuka.
- **Ikon game:** diekstrak dari executable game dan disimpan sebagai cache lokal.
- **Screenshot:** `F10` mengambil seluruh desktop virtual (termasuk monitor tambahan) ke folder Pictures.
- **Galeri:** pratinjau screenshot, salin, simpan sebagai, buka, dan hapus. Gambar diproses dengan ukuran pratinjau agar galeri tetap ringan.
- **Filter:** preset warna dengan slider intensitas; klik di luar panel untuk menutup overlay.
- **Notifikasi:** toast glass untuk status capture dan error. Mode Jangan Ganggu menyembunyikan toast NVIDIA Lite dan bisa diubah dari pengaturan atau menu system tray.
- **Spotify:** kontrol putar/jeda/berikutnya opsional melalui Spotify Web API. Token OAuth tidak disimpan dan harus dimasukkan ulang setelah aplikasi ditutup. Spotify memerlukan scope `user-modify-playback-state`, perangkat aktif, dan akun Premium untuk kontrol playback.
- **Kontrol sistem:** volume, brightness yang didukung Windows WMI, dan tautan ke pengaturan Wi-Fi Windows. Jika perangkat tidak mendukung kontrol tertentu, aplikasi menonaktifkannya dan menjelaskan alasannya.

## Persyaratan dan menjalankan

- Windows 10/11 64-bit.
- .NET 9 SDK untuk build dari source.

```powershell
dotnet restore
dotnet build
dotnet run
```

## Shortcut

- **Alt + Z:** buka/tutup overlay.
- **F10:** ambil screenshot.

Shortcut dapat diubah dari Pengaturan dan disimpan lokal.

## Lokasi file

- **Data, pengaturan, dan cache ikon:** `%LOCALAPPDATA%\NvidiaCi`
- **Screenshot:** folder `NvidiaLite\Screenshots` di Known Folder Pictures Windows.

Token Spotify hanya berada di memori proses dan tidak ditulis ke file pengaturan.

## Gambar dokumentasi versi sebelumnya

Gambar di bawah berasal dari tampilan sebelum perombakan UI.

<img src="doc/image.png" width="350" alt="Tampilan NVIDIA Lite versi sebelumnya">
<img src="doc/image copy.png" width="350" alt="Galeri NVIDIA Lite versi sebelumnya">
<img src="doc/image copy 2.png" width="350" alt="Pengaturan NVIDIA Lite versi sebelumnya">
