using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using WpfColor = System.Windows.Media.Color;

namespace NvidiaCi
{
    public partial class ScreenFilterWindow : Window
    {
        // Win32 API to hide from Alt+Tab and make click-through
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public ScreenFilterWindow()
        {
            InitializeComponent();
            PositionWindow();
            
            // Hide from Alt+Tab and make click-through
            this.SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                // Combine TOOLWINDOW (hide from Alt+Tab) and TRANSPARENT (click-through)
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);
            };
            
            System.Diagnostics.Debug.WriteLine($"ScreenFilterWindow initialized: {this.Width}x{this.Height}");
        }

        public void PositionWindow()
        {
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
            
            System.Diagnostics.Debug.WriteLine($"ScreenFilterWindow positioned: {this.Width}x{this.Height} at ({this.Left},{this.Top})");
        }

        public void ApplyFilter(FilterPreset preset, double intensity = 20)
        {
            double scale = Math.Clamp(intensity / 20.0, 0, 3);
            switch (preset)
            {
                case FilterPreset.None:
                    SetTint(0, 0, 0, 0, scale);
                    break;

                case FilterPreset.Vivid:
                    SetTint(18, 255, 140, 0, scale);
                    break;

                case FilterPreset.CoolBlue:
                    SetTint(20, 30, 80, 200, scale);
                    break;

                case FilterPreset.NightMode:
                    SetTint(35, 180, 20, 0, scale);
                    break;

                case FilterPreset.Cinematic:
                    SetTint(25, 0, 0, 0, scale);
                    break;

                case FilterPreset.Reading:
                    SetTint(30, 255, 240, 200, scale);
                    break;

                case FilterPreset.Sharpen:
                    SetTint(0, 0, 0, 0, scale);
                    break;
            }
        }

        /// <summary>
        /// Apply a custom RGBA tint with given opacity (0-100).
        /// </summary>
        public void ApplyCustomTint(WpfColor color, double opacity)
        {
            byte alpha = (byte)(Math.Clamp(opacity, 0, 100) / 100.0 * 255);
            FilterRect.Fill = new SolidColorBrush(WpfColor.FromArgb(alpha, color.R, color.G, color.B));
            FilterRect.Effect = null;
        }

        private void SetTint(byte alpha, byte red, byte green, byte blue, double scale)
        {
            byte scaledAlpha = (byte)Math.Clamp(Math.Round(alpha * scale), 0, 255);
            FilterRect.Fill = new SolidColorBrush(WpfColor.FromArgb(scaledAlpha, red, green, blue));
            FilterRect.Effect = null;
        }
    }

    public enum FilterPreset
    {
        None,
        Vivid,
        CoolBlue,
        NightMode,
        Cinematic,
        Reading,
        Sharpen
    }
}
