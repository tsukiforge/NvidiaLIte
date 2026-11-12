using System;
using System.Windows;
using System.Windows.Forms;

namespace NvidiaCi
{
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _notifyIcon;
        private OverlayWindow? _overlayWindow;
        private ToolStripMenuItem? _overlayMenuItem;
        private ToolStripMenuItem? _screenshotMenuItem;
        private ToolStripMenuItem? _dndMenuItem;
        private UserSettingsStore? _settingsStore;
        private static System.Threading.Mutex? _mutex;
        private bool _ownsMutex;
        private bool _updatingDndMenu;

        public UserSettings Settings { get; private set; } = new();
        public ToastNotificationService Notifications { get; } = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            // Multiple instance prevention
            _mutex = new System.Threading.Mutex(true, "NvidiaCiMutex", out bool createdNew);
            _ownsMutex = createdNew;
            if (!createdNew)
            {
                System.Windows.MessageBox.Show("Aplikasi sudah berjalan.", "NVIDIA Lite", MessageBoxButton.OK, MessageBoxImage.Information);
                System.Windows.Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);
            _settingsStore = new UserSettingsStore();
            Settings = _settingsStore.Load();
            Notifications.IsDoNotDisturb = Settings.DoNotDisturb;

            try
            {
                _overlayWindow = new OverlayWindow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Overlay initialization failed: {ex}");
                System.Windows.MessageBox.Show(
                    $"NVIDIA Lite gagal dimulai:\n{ex.Message}",
                    "NVIDIA Lite",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                ShutdownApp();
                return;
            }

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = "NVIDIA Lite";

            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
                if (System.IO.File.Exists(iconPath))
                    _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
                else
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load icon: {ex.Message}");
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            _notifyIcon.Visible = true;

            ShowWelcomeNotification();
            _notifyIcon.MouseClick += (s, args) =>
            {
                if (args.Button == MouseButtons.Left)
                    ShowOverlay();
            };

            var contextMenu = new ContextMenuStrip();
            _overlayMenuItem = new ToolStripMenuItem();
            _overlayMenuItem.Click += (s, args) => ToggleOverlay();
            _screenshotMenuItem = new ToolStripMenuItem();
            _screenshotMenuItem.Click += (s, args) => _overlayWindow?.TakeScreenshot();
            UpdateHotkeyMenuLabels();
            contextMenu.Items.Add(_overlayMenuItem);
            contextMenu.Items.Add(_screenshotMenuItem);
            _dndMenuItem = new ToolStripMenuItem("Jangan ganggu")
            {
                Checked = Settings.DoNotDisturb,
                CheckOnClick = true
            };
            _dndMenuItem.CheckedChanged += (s, args) =>
            {
                if (!_updatingDndMenu)
                    SetDoNotDisturb(_dndMenuItem.Checked);
            };
            contextMenu.Items.Add(_dndMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, (s, args) => ShutdownApp());
            _notifyIcon.ContextMenuStrip = contextMenu;

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        public void SetDoNotDisturb(bool enabled)
        {
            Settings.DoNotDisturb = enabled;
            Notifications.IsDoNotDisturb = enabled;
            _overlayWindow?.UpdateDoNotDisturbToggle(enabled);
            if (_dndMenuItem != null && _dndMenuItem.Checked != enabled)
            {
                _updatingDndMenu = true;
                try
                {
                    _dndMenuItem.Checked = enabled;
                }
                finally
                {
                    _updatingDndMenu = false;
                }
            }

            try
            {
                _settingsStore?.Save(Settings);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Pengaturan Jangan Ganggu gagal disimpan:\n{ex.Message}",
                    "NVIDIA Lite",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            if (!enabled)
                Notifications.Show("Notifikasi aktif", "Toast NVIDIA Lite akan ditampilkan kembali.");
        }

        public void SaveSettings()
        {
            if (_settingsStore == null)
                throw new InvalidOperationException("Pengaturan aplikasi belum siap.");

            _settingsStore.Save(Settings);
        }

        public void UpdateHotkeyMenuLabels()
        {
            if (_overlayMenuItem != null)
                _overlayMenuItem.Text = $"Tampilkan/sembunyikan overlay (Alt+{Settings.OverlayHotkey})";
            if (_screenshotMenuItem != null)
                _screenshotMenuItem.Text = $"Ambil screenshot ({Settings.ScreenshotHotkey})";
        }

        private void ShowWelcomeNotification()
        {
            string appDataPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NvidiaCi");
            string flagFile = System.IO.Path.Combine(appDataPath, ".first_run");
            bool isFirstRun = !System.IO.File.Exists(flagFile);
            if (isFirstRun)
            {
                System.IO.Directory.CreateDirectory(appDataPath);
                System.IO.File.WriteAllText(flagFile, DateTime.Now.ToString("O"));
            }

            Notifications.Show(
                isFirstRun ? "Selamat datang di NVIDIA Lite" : "NVIDIA Lite aktif",
                isFirstRun
                    ? "Alt+Z membuka overlay • F10 mengambil screenshot."
                    : "Aplikasi berjalan di background. Tekan Alt+Z untuk membuka overlay.");
        }

        public async void ToggleOverlay()
        {
            if (_overlayWindow == null) return;

            if (_overlayWindow.IsVisible)
            {
                await _overlayWindow.HideAnimated();
            }
            else
            {
                await _overlayWindow.ShowAnimated();
            }
        }

        private void ShowOverlay()
        {
            if (_overlayWindow != null)
                _ = _overlayWindow.ShowAnimated();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _overlayWindow?.Dispose();
            Notifications.Dispose();
            _notifyIcon?.Dispose();
            if (_ownsMutex)
                _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }

        private void ShutdownApp()
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
