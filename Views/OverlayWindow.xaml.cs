using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;

namespace NvidiaCi
{
    public partial class OverlayWindow : Window
    {
        private const int HOTKEY_OVERLAY_ID = 9000;
        private const int HOTKEY_SCREENSHOT_ID = 9001;
        private readonly GameDataManager _dataManager = new GameDataManager();
        private readonly ScreenRecordingService _recordingService = new();
        private readonly SpotifyControlService _spotifyService = new();
        private List<GameItem> _games = new();
        private bool _disposed;

        private bool _isChangingOverlayHotkey = false;
        private bool _isChangingScreenshotHotkey = false;
        private bool _isInitializingSettings;
        private bool _isSyncingControls;
        private bool _isCustomTint;
        private bool _isCapturing;
        private int _visibilityTransitionVersion;
        private System.Windows.Media.Animation.Storyboard? _activeVisibilityStoryboard;
        private System.Windows.Input.Key _tempOverlayKey = System.Windows.Input.Key.Z;
        private System.Windows.Input.Key _tempScreenshotKey = System.Windows.Input.Key.F10;

        private ScreenFilterWindow? _filterWindow;
        private System.Windows.Media.Color _selectedTintColor = MediaColor.FromRgb(0xB7, 0xA1, 0xFF);
        private FilterPreset _activePreset = FilterPreset.None;

        public OverlayWindow()
        {
            InitializeComponent();

            if (System.Windows.Application.Current is App app)
            {
                if (Enum.TryParse(app.Settings.OverlayHotkey, out System.Windows.Input.Key overlayKey))
                    _tempOverlayKey = overlayKey;
                if (Enum.TryParse(app.Settings.ScreenshotHotkey, out System.Windows.Input.Key screenshotKey))
                    _tempScreenshotKey = screenshotKey;

                OverlayHotkeyText.Text = $"Alt + {_tempOverlayKey}";
                ScreenshotHotkeyText.Text = _tempScreenshotKey.ToString();
            }

            var handle = new WindowInteropHelper(this).EnsureHandle();
            RegisterGlobalHotkey(handle);

            this.Closed += OnClosed;
            PositionWindow();

            _filterWindow = new ScreenFilterWindow();
            _filterWindow.Show();
            _recordingService.RecordingStopped += OnRecordingStopped;

            this.Opacity = 0;
            this.Visibility = Visibility.Collapsed;
        }

        public Task ShowAnimated()
        {
            _visibilityTransitionVersion++;
            StopVisibilityTransition();
            PositionWindow();
            this.Visibility = Visibility.Visible;
            this.Opacity = 1;

            _activeVisibilityStoryboard = (Storyboard)this.Resources["ShowSidebar"];
            _activeVisibilityStoryboard.Begin(this, HandoffBehavior.SnapshotAndReplace, true);

            this.Activate();
            this.Topmost = false;
            this.Topmost = true;
            this.Focus();

            return Task.CompletedTask;
        }

        public async Task HideAnimated()
        {
            int transitionVersion = ++_visibilityTransitionVersion;
            StopVisibilityTransition();
            _activeVisibilityStoryboard = (Storyboard)this.Resources["HideSidebar"];
            _activeVisibilityStoryboard.Begin(this, HandoffBehavior.SnapshotAndReplace, true);

            await Task.Delay(350);
            if (transitionVersion == _visibilityTransitionVersion)
                this.Visibility = Visibility.Collapsed;
        }

        private void StopVisibilityTransition()
        {
            _activeVisibilityStoryboard?.Stop(this);
            _activeVisibilityStoryboard = null;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Application.Current is App app)
            {
                _isInitializingSettings = true;
                DndToggle.IsChecked = app.Settings.DoNotDisturb;
                FfmpegPathText.Text = string.IsNullOrWhiteSpace(app.Settings.FfmpegPath)
                    ? "FFmpeg: cari otomatis melalui PATH"
                    : $"FFmpeg: {app.Settings.FfmpegPath}";
                _isInitializingSettings = false;
            }

            LoadData();
            LoadGallery();
            if (_games.Count == 0)
                await RefreshGameListAsync();
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string viewName)
            {
                SwitchView(viewName);
            }
        }

        private void SwitchView(string viewName)
        {
            DashboardView.Visibility = Visibility.Collapsed;
            GalleryView.Visibility = Visibility.Collapsed;
            FiltersView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;

            switch (viewName)
            {
                case "Dashboard":
                    DashboardView.Visibility = Visibility.Visible;
                    HeaderText.Text = " HOME";
                    break;
                case "Gallery":
                    GalleryView.Visibility = Visibility.Visible;
                    HeaderText.Text = " GALERI";
                    LoadGallery();
                    break;
                case "Filters":
                    FiltersView.Visibility = Visibility.Visible;
                    HeaderText.Text = " FILTER";
                    break;
                case "Settings":
                    SettingsView.Visibility = Visibility.Visible;
                    HeaderText.Text = " PENGATURAN";
                    SyncSystemSliders();
                    break;
            }

            var sb = (Storyboard)this.Resources["FadeInContent"];
            sb.Begin(ContentView);
        }

        private void SyncSystemSliders()
        {
            _isSyncingControls = true;
            try
            {
                VolumeSlider.Value = SystemControlHelper.GetSystemVolume();
                VolumeSlider.IsEnabled = true;
            }
            catch (Exception ex)
            {
                VolumeSlider.IsEnabled = false;
                ShowError("Kontrol volume tidak tersedia", ex.Message);
            }

            try
            {
                BrightnessSlider.Value = SystemControlHelper.GetBrightness();
                BrightnessSlider.IsEnabled = true;
            }
            catch (Exception ex)
            {
                BrightnessSlider.IsEnabled = false;
                ShowError("Kontrol brightness tidak tersedia", ex.Message);
            }
            finally
            {
                _isSyncingControls = false;
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded && !_isSyncingControls)
            {
                try
                {
                    SystemControlHelper.SetSystemVolume((float)e.NewValue);
                }
                catch (Exception ex)
                {
                    VolumeSlider.IsEnabled = false;
                    ShowError("Volume tidak dapat diubah", ex.Message);
                }
            }
        }

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded && !_isSyncingControls)
            {
                try
                {
                    SystemControlHelper.SetBrightness((int)e.NewValue);
                }
                catch (Exception ex)
                {
                    BrightnessSlider.IsEnabled = false;
                    ShowError("Brightness tidak dapat diubah", ex.Message);
                }
            }
        }

        private void ManageWifi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SystemControlHelper.OpenWifiSettings();
            }
            catch (Exception ex)
            {
                ShowError("Pengaturan Wi-Fi gagal dibuka", ex.Message);
            }
        }



        private async void LoadGallery()
        {
            try
            {
                string screenshotPath = UserSettingsStore.ScreenshotDirectory;
                List<GalleryImageItem> images = await Task.Run(() =>
                {
                    if (!Directory.Exists(screenshotPath))
                        return new List<GalleryImageItem>();

                    return Directory.GetFiles(screenshotPath, "*.png")
                        .OrderByDescending(File.GetCreationTimeUtc)
                        .Take(24)
                        .Select(TryLoadGalleryImage)
                        .Where(image => image != null)
                        .Cast<GalleryImageItem>()
                        .ToList();
                });

                GalleryItemsControl.ItemsSource = images;
                NoGalleryText.Visibility = images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ShowError("Galeri gagal dimuat", ex.Message);
            }
        }

        private static GalleryImageItem? TryLoadGalleryImage(string path)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 480;
                bitmap.EndInit();
                bitmap.Freeze();
                return new GalleryImageItem(path, bitmap);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Gallery image could not be loaded ({path}): {ex.Message}");
                return null;
            }
        }

        private void LoadData()
        {
            try
            {
                _games = _dataManager.LoadGames();
                ApplyGameFilter();
            }
            catch (Exception ex)
            {
                ShowError("Library game gagal dimuat", ex.Message);
                _games = new List<GameItem>();
                ApplyGameFilter();
            }
        }

        private async Task RefreshGameListAsync()
        {
            ScanButton.IsEnabled = false;
            ScanButton.Content = "MEMINDAI...";
            NoGamesFoundText.Visibility = Visibility.Collapsed;
            try
            {
                List<GameItem> games = await Task.Run(() => new GameScanner().ScanForGames(200));
                _dataManager.SaveGames(games);
                _games = games;
                ApplyGameFilter();
                if (games.Count > 0)
                    ShowNotification("Scan selesai", $"{games.Count} game valid ditemukan.");
            }
            catch (Exception ex)
            {
                ShowError("Scan game gagal", ex.Message);
            }
            finally
            {
                ScanButton.IsEnabled = true;
                ScanButton.Content = "↻  PINDAI GAME";
            }
        }

        private void ApplyGameFilter()
        {
            string query = SearchBox?.Text?.Trim() ?? string.Empty;
            List<GameItem> filtered = string.IsNullOrEmpty(query)
                ? _games
                : _games.Where(game => game.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();

            GameListBox.ItemsSource = filtered;
            NoGamesFoundText.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (filtered.Count == 0 && _games.Count > 0)
            {
                NoGamesTitleText.Text = "Tidak ada hasil";
                NoGamesHintText.Text = "Coba kata kunci lain untuk mencari game.";
            }
            else
            {
                NoGamesTitleText.Text = "Belum ada game terdeteksi";
                NoGamesHintText.Text = "Pindai library Steam, Epic, GOG, EA, atau folder Games.";
            }
        }

        private void ShowNotification(string title, string message)
        {
            if (System.Windows.Application.Current is App app)
                app.Notifications.Show(title, message);
        }

        private void ShowError(string title, string message)
        {
            if (System.Windows.Application.Current is App app)
                app.Notifications.Show(title, message, true);
            else
                System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private sealed record GalleryImageItem(string Path, BitmapSource Source);
        
        private void OpenImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is string imagePath)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(imagePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    ShowError("File tidak dapat dibuka", ex.Message);
                }
            }
        }

        private void CopyImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is string imagePath)
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                    bitmap.Freeze();
                    System.Windows.Clipboard.SetImage(bitmap);
                    ShowNotification("Gambar disalin", "Screenshot siap ditempel.");
                }
                catch (Exception ex)
                {
                    ShowError("Gambar gagal disalin", ex.Message);
                }
            }
        }

        private void GalleryItem_PreviewClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: GalleryImageItem image })
            {
                try
                {
                    var fullImage = new BitmapImage();
                    fullImage.BeginInit();
                    fullImage.UriSource = new Uri(image.Path, UriKind.Absolute);
                    fullImage.CacheOption = BitmapCacheOption.OnLoad;
                    fullImage.EndInit();
                    fullImage.Freeze();
                    FullImageDisplay.Source = fullImage;
                    ImagePreviewModal.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    ShowError("Pratinjau screenshot gagal dibuka", ex.Message);
                }
            }
        }

        private void ClosePreview_Click(object sender, RoutedEventArgs e)
        {
            FullImageDisplay.Source = null;
            ImagePreviewModal.Visibility = Visibility.Collapsed;
        }

        private void ClosePreview_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            FullImageDisplay.Source = null;
            ImagePreviewModal.Visibility = Visibility.Collapsed;
        }

        private void CopyCurrentPreview_Click(object sender, RoutedEventArgs e)
        {
            if (FullImageDisplay.Source is System.Windows.Media.Imaging.BitmapSource bitmap)
            {
                try
                {
                    System.Windows.Clipboard.SetImage(bitmap);
                    ShowNotification("Gambar disalin", "Screenshot siap ditempel.");
                }
                catch (Exception ex)
                {
                    ShowError("Gambar gagal disalin", ex.Message);
                }
            }
        }

        private void SaveImageAs_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is string imagePath)
            {
                var sfd = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = System.IO.Path.GetFileName(imagePath),
                    DefaultExt = ".png",
                    Filter = "PNG Image (.png)|*.png|All files (*.*)|*.*"
                };

                if (sfd.ShowDialog() == true)
                {
                    try
                    {
                        System.IO.File.Copy(imagePath, sfd.FileName, true);
                        ShowNotification("Gambar disimpan", sfd.FileName);
                    }
                    catch (Exception ex)
                    {
                        ShowError("Gambar gagal disimpan", ex.Message);
                    }
                }
            }
        }

        private void DeleteImage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is string imagePath)
            {
                if (System.Windows.MessageBox.Show(
                    $"Hapus screenshot ini?\n{imagePath}",
                    "Hapus screenshot",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                try
                {
                    System.IO.File.Delete(imagePath);
                    LoadGallery();
                    ShowNotification("Screenshot dihapus", "File telah dihapus dari perangkat.");
                }
                catch (Exception ex)
                {
                    ShowError("Screenshot gagal dihapus", ex.Message);
                }
            }
        }

        // --- WINAPI HOTKEY LOGIC ---

        private void RegisterGlobalHotkey(IntPtr handle)
        {
            var source = HwndSource.FromHwnd(handle);
            source?.AddHook(WndProc);

            uint vkOverlay = (uint)HotkeyHelper.GetVirtualKeyCode(_tempOverlayKey);
            if (!HotkeyHelper.RegisterHotKey(handle, HOTKEY_OVERLAY_ID, HotkeyHelper.MOD_ALT, vkOverlay))
                ShowError("Shortcut overlay gagal dipasang",
                    $"Alt + {_tempOverlayKey} mungkin sedang digunakan aplikasi lain (Win32 {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");

            uint vkScreenshot = (uint)HotkeyHelper.GetVirtualKeyCode(_tempScreenshotKey);
            if (!HotkeyHelper.RegisterHotKey(handle, HOTKEY_SCREENSHOT_ID, 0, vkScreenshot))
                ShowError("Shortcut screenshot gagal dipasang",
                    $"{_tempScreenshotKey} mungkin sedang digunakan aplikasi lain (Win32 {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}).");
        }

        private void UnregisterGlobalHotkey()
        {
            var handle = new WindowInteropHelper(this).Handle;
            HotkeyHelper.UnregisterHotKey(handle, HOTKEY_OVERLAY_ID);
            HotkeyHelper.UnregisterHotKey(handle, HOTKEY_SCREENSHOT_ID);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == HotkeyHelper.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_OVERLAY_ID)
                {
                    this.Dispatcher.Invoke(() => {
                        if (System.Windows.Application.Current is App app) 
                            app.ToggleOverlay();
                    });
                    handled = true;
                }
                else if (id == HOTKEY_SCREENSHOT_ID)
                {
                    this.Dispatcher.Invoke(() => {
                        TakeScreenshot();
                    });
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void TakeScreenshot()
        {
            _ = CaptureScreenshotAsync();
        }

        private async Task CaptureScreenshotAsync()
        {
            if (_isCapturing)
                return;

            _isCapturing = true;
            bool restoreOverlay = IsVisible;
            try
            {
                if (restoreOverlay)
                    await HideAnimated();

                await Task.Delay(100);
                string path = await Task.Run(ScreenshotHelper.CaptureScreen);
                if (restoreOverlay)
                    await ShowAnimated();

                if (HeaderText.Text.Contains("GALERI", StringComparison.OrdinalIgnoreCase))
                    LoadGallery();

                FlashBorder.Visibility = Visibility.Visible;
                await Task.Delay(70);
                FlashBorder.Visibility = Visibility.Collapsed;
                CaptureStatusText.Text = $"Screenshot tersimpan: {path}";
                ShowNotification("Screenshot tersimpan", path);
            }
            catch (Exception ex)
            {
                if (restoreOverlay && !IsVisible)
                    await ShowAnimated();
                FlashBorder.Visibility = Visibility.Collapsed;
                ShowError("Screenshot gagal", ex.Message);
            }
            finally
            {
                _isCapturing = false;
            }
        }

        private async void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            await CaptureScreenshotAsync();
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_recordingService.IsRecording)
                {
                    RecordButton.IsEnabled = false;
                    await _recordingService.StopAsync();
                    RecordButton.IsEnabled = true;
                }
                else
                {
                    string? ffmpegPath = (System.Windows.Application.Current as App)?.Settings.FfmpegPath;
                    _recordingService.Start(ffmpegPath);
                    RecordButton.Content = "■  BERHENTI REKAM";
                    RecordButton.Foreground = new SolidColorBrush(MediaColor.FromRgb(255, 139, 139));
                    CaptureStatusText.Text = $"Merekam ke: {_recordingService.RecordingPath}";
                    ShowNotification("Perekaman dimulai", "Video disimpan di Videos\\NvidiaLite\\Recordings.");
                }
            }
            catch (Exception ex)
            {
                RecordButton.IsEnabled = true;
                ShowError("Perekaman gagal dimulai", ex.Message);
            }
        }

        private void OnRecordingStopped(object? sender, RecordingStoppedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                RecordButton.IsEnabled = true;
                RecordButton.Content = "●  MULAI REKAM";
                RecordButton.Foreground = (System.Windows.Media.Brush)FindResource("TextColor");

                if (e.ExitCode == 0 && !string.IsNullOrWhiteSpace(e.Path) && File.Exists(e.Path))
                {
                    CaptureStatusText.Text = $"Rekaman tersimpan: {e.Path}";
                    ShowNotification("Rekaman selesai", e.Path);
                }
                else
                {
                    string detail = string.IsNullOrWhiteSpace(e.Error) ? $"Kode keluar FFmpeg: {e.ExitCode}." : e.Error;
                    CaptureStatusText.Text = "Perekaman berhenti karena terjadi kesalahan.";
                    ShowError("Perekaman gagal", detail);
                }
            });
        }

        private void OpenMediaFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(UserSettingsStore.RecordingDirectory);
                Process.Start(new ProcessStartInfo(UserSettingsStore.RecordingDirectory)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowError("Folder media gagal dibuka", ex.Message);
            }
        }

        private void ChooseFfmpeg_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih FFmpeg executable",
                Filter = "ffmpeg.exe|ffmpeg.exe|Executable (*.exe)|*.exe",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() != true)
                return;

            if (System.Windows.Application.Current is not App app)
                return;

            try
            {
                app.Settings.FfmpegPath = dialog.FileName;
                app.SaveSettings();
                FfmpegPathText.Text = $"FFmpeg: {dialog.FileName}";
                ShowNotification("FFmpeg tersimpan", "Perekam siap digunakan.");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Pengaturan FFmpeg gagal disimpan:\n{ex.Message}",
                    "NVIDIA Lite",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DoNotDisturb_Checked(object sender, RoutedEventArgs e) =>
            UpdateDoNotDisturb(true);

        private void DoNotDisturb_Unchecked(object sender, RoutedEventArgs e) =>
            UpdateDoNotDisturb(false);

        private void UpdateDoNotDisturb(bool enabled)
        {
            if (_isInitializingSettings || System.Windows.Application.Current is not App app)
                return;

            app.SetDoNotDisturb(enabled);
        }

        public void UpdateDoNotDisturbToggle(bool enabled)
        {
            if (DndToggle == null || DndToggle.IsChecked == enabled)
                return;

            _isInitializingSettings = true;
            DndToggle.IsChecked = enabled;
            _isInitializingSettings = false;
        }

        private void ApplySpotifyToken_Click(object sender, RoutedEventArgs e)
        {
            _spotifyService.SetAccessToken(SpotifyTokenBox.Password);
            if (_spotifyService.HasAccessToken)
                SpotifyStatusText.Text = "Token disimpan sementara. Uji dengan kontrol Spotify.";
            else
                SpotifyStatusText.Text = "Token belum dimasukkan.";
        }

        private async void SpotifyPlay_Click(object sender, RoutedEventArgs e) =>
            await RunSpotifyCommandAsync(_spotifyService.PlayAsync, "Perintah putar dikirim.");

        private async void SpotifyPause_Click(object sender, RoutedEventArgs e) =>
            await RunSpotifyCommandAsync(_spotifyService.PauseAsync, "Perintah jeda dikirim.");

        private async void SpotifyNext_Click(object sender, RoutedEventArgs e) =>
            await RunSpotifyCommandAsync(_spotifyService.NextAsync, "Lagu berikutnya diminta.");

        private async Task RunSpotifyCommandAsync(Func<Task> command, string successMessage)
        {
            try
            {
                await command();
                SpotifyStatusText.Text = successMessage;
                ShowNotification("Spotify", successMessage);
            }
            catch (Exception ex)
            {
                SpotifyStatusText.Text = ex.Message;
                ShowError("Kontrol Spotify gagal", ex.Message);
            }
        }

        private void ChangeOverlayHotkey_Click(object sender, RoutedEventArgs e)
        {
            _isChangingOverlayHotkey = true;
            _isChangingScreenshotHotkey = false;
            OverlayHotkeyText.Text = "TEKAN TOMBOL...";
            ChangeOverlayBtn.Visibility = Visibility.Collapsed;
        }

        private void ChangeScreenshotHotkey_Click(object sender, RoutedEventArgs e)
        {
            _isChangingScreenshotHotkey = true;
            _isChangingOverlayHotkey = false;
            ScreenshotHotkeyText.Text = "TEKAN TOMBOL...";
            ChangeScreenshotBtn.Visibility = Visibility.Collapsed;
        }

        private void OnWindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (ImagePreviewModal.Visibility == Visibility.Visible)
                {
                    ImagePreviewModal.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                    return;
                }
            }

            // Ignore modifiers themselves as primary keys if needed, or handle them
            if (e.Key == System.Windows.Input.Key.LeftAlt || e.Key == System.Windows.Input.Key.RightAlt ||
                e.Key == System.Windows.Input.Key.LeftCtrl || e.Key == System.Windows.Input.Key.RightCtrl ||
                e.Key == System.Windows.Input.Key.LeftShift || e.Key == System.Windows.Input.Key.RightShift)
                return;

            if (_isChangingOverlayHotkey)
            {
                _isChangingOverlayHotkey = false;
                ChangeOverlayBtn.Visibility = Visibility.Visible;

                var handle = new WindowInteropHelper(this).Handle;
                HotkeyHelper.UnregisterHotKey(handle, HOTKEY_OVERLAY_ID);
                uint vk = (uint)HotkeyHelper.GetVirtualKeyCode(e.Key);
                if (HotkeyHelper.RegisterHotKey(handle, HOTKEY_OVERLAY_ID, HotkeyHelper.MOD_ALT, vk))
                {
                    _tempOverlayKey = e.Key;
                    OverlayHotkeyText.Text = $"Alt + {_tempOverlayKey}";
                    SaveHotkeys();
                }
                else
                {
                    uint previousVk = (uint)HotkeyHelper.GetVirtualKeyCode(_tempOverlayKey);
                    HotkeyHelper.RegisterHotKey(handle, HOTKEY_OVERLAY_ID, HotkeyHelper.MOD_ALT, previousVk);
                    ShowError("Shortcut tidak tersedia", $"Alt + {e.Key} sedang digunakan aplikasi lain.");
                }
                e.Handled = true;
            }
            else if (_isChangingScreenshotHotkey)
            {
                _isChangingScreenshotHotkey = false;
                ChangeScreenshotBtn.Visibility = Visibility.Visible;

                var handle = new WindowInteropHelper(this).Handle;
                HotkeyHelper.UnregisterHotKey(handle, HOTKEY_SCREENSHOT_ID);
                uint vk = (uint)HotkeyHelper.GetVirtualKeyCode(e.Key);
                if (HotkeyHelper.RegisterHotKey(handle, HOTKEY_SCREENSHOT_ID, 0, vk))
                {
                    _tempScreenshotKey = e.Key;
                    ScreenshotHotkeyText.Text = _tempScreenshotKey.ToString();
                    SaveHotkeys();
                }
                else
                {
                    uint previousVk = (uint)HotkeyHelper.GetVirtualKeyCode(_tempScreenshotKey);
                    HotkeyHelper.RegisterHotKey(handle, HOTKEY_SCREENSHOT_ID, 0, previousVk);
                    ShowError("Shortcut tidak tersedia", $"{e.Key} sedang digunakan aplikasi lain.");
                }
                e.Handled = true;
            }
        }

        private void SaveHotkeys()
        {
            if (System.Windows.Application.Current is not App app)
                return;

            try
            {
                app.Settings.OverlayHotkey = _tempOverlayKey.ToString();
                app.Settings.ScreenshotHotkey = _tempScreenshotKey.ToString();
                app.UpdateHotkeyMenuLabels();
                app.SaveSettings();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Shortcut berhasil digunakan untuk sesi ini, tetapi gagal disimpan:\n{ex.Message}",
                    "NVIDIA Lite",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // --- SISANYA TETAP SAMA ---

        private void GameListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GameListBox.SelectedItem is GameItem selectedGame)
            {
                LaunchGame(selectedGame.Path);
                GameListBox.SelectedItem = null;
            }
        }

        private async void LaunchGame(string exePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                    throw new FileNotFoundException("Executable game tidak ditemukan.", exePath);

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = System.IO.Path.GetDirectoryName(exePath),
                    UseShellExecute = true
                });
                await HideAnimated();
            }
            catch (Exception ex)
            {
                ShowError("Game gagal dijalankan", ex.Message);
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e) =>
            await RefreshGameListAsync();

        private void OnClosed(object? sender, EventArgs e)
        {
            Dispose();
        }

        // --- FILTER HANDLERS ---

        private void FilterPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button btn || _filterWindow == null)
            {
                System.Diagnostics.Debug.WriteLine("Filter: button or window is null");
                return;
            }

            if (Enum.TryParse<FilterPreset>(btn.Tag?.ToString(), out var preset))
            {
                _activePreset = preset;
                _isCustomTint = false;
                _filterWindow.ApplyFilter(preset, FilterOpacitySlider.Value);
                _filterWindow.Topmost = true;
                _filterWindow.Show();
                UpdateFilterStatus();
            }
            else
            {
                ShowError("Filter tidak dikenali", $"Preset '{btn.Tag}' tidak tersedia.");
            }
        }

        private void Swatch_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not Border border || _filterWindow == null) return;

            var colorStr = border.Tag?.ToString();
            if (colorStr == null) return;

            try
            {
                _selectedTintColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                _activePreset = FilterPreset.None;
                _isCustomTint = true;
                _filterWindow.ApplyCustomTint(_selectedTintColor, FilterOpacitySlider.Value);
                UpdateFilterStatus();
            }
            catch (Exception ex)
            {
                ShowError("Warna filter tidak valid", ex.Message);
            }
        }

        private void FilterOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!this.IsLoaded || _filterWindow == null) return;

            FilterOpacityLabel.Text = $"{(int)e.NewValue}%";

            if (_isCustomTint)
            {
                _filterWindow.ApplyCustomTint(_selectedTintColor, e.NewValue);
            }
            else
            {
                _filterWindow.ApplyFilter(_activePreset, e.NewValue);
            }
        }

        private void UpdateFilterStatus()
        {
            if (_filterWindow == null)
                return;

            if (_isCustomTint && FilterOpacitySlider.Value > 0)
            {
                FilterStatusDot.Fill = new SolidColorBrush(_selectedTintColor);
                FilterStatusText.Text = "Warna kustom aktif";
                FilterStatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextColor");
            }
            else if (_activePreset == FilterPreset.None)
            {
                FilterStatusDot.Fill = new SolidColorBrush(Colors.Gray);
                FilterStatusText.Text = "Filter tidak aktif";
                FilterStatusText.Foreground = new SolidColorBrush(MediaColor.FromRgb(0x9A, 0xA7, 0xB5));
            }
            else
            {
                FilterStatusDot.Fill = (System.Windows.Media.Brush)FindResource("AccentColor");
                FilterStatusText.Text = $"Preset aktif: {_activePreset}";
                FilterStatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextColor");
            }
        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyGameFilter();
        }

        private async void Background_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Grid)
            {
                await HideAnimated();
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e) => await HideAnimated();

        public void PositionWindow()
        {
            // Full Screen coverage
            this.Left = 0;
            this.Top = 0;
            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;
            
            // Adjust Sidebar Width for Grid layout
            SidebarBorder.Width = 760;

            // Ensure transparency and topmost
            this.Topmost = false;
            this.Topmost = true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _recordingService.RecordingStopped -= OnRecordingStopped;
            _recordingService.Dispose();
            UnregisterGlobalHotkey();
            _filterWindow?.Close();
        }
    }
}
