using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MediaColor = System.Windows.Media.Color;

namespace NvidiaCi
{
    public partial class ToastNotificationWindow : Window
    {
        private readonly DispatcherTimer _dismissTimer = new()
        {
            Interval = TimeSpan.FromSeconds(4)
        };

        public ToastNotificationWindow()
        {
            InitializeComponent();
            _dismissTimer.Tick += (_, _) => HideToast();
        }

        public void ShowToast(string title, string message, bool isError)
        {
            ToastTitle.Text = title;
            ToastMessage.Text = message;
            StatusGlyph.Text = isError ? "!" : "✓";
            StatusGlyph.Foreground = isError
                ? new SolidColorBrush(MediaColor.FromRgb(255, 139, 139))
                : new SolidColorBrush(MediaColor.FromRgb(212, 191, 255));
            StatusBadge.Background = isError
                ? new SolidColorBrush(MediaColor.FromArgb(38, 255, 88, 88))
                : new SolidColorBrush(MediaColor.FromArgb(58, 183, 161, 255));
            ToastBorder.BorderBrush = isError
                ? new SolidColorBrush(MediaColor.FromArgb(100, 255, 88, 88))
                : new SolidColorBrush(MediaColor.FromArgb(120, 199, 181, 255));

            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 24;
            Top = workArea.Top + 24;

            _dismissTimer.Stop();
            Opacity = 0;
            if (!IsVisible)
                Show();

            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            _dismissTimer.Start();
        }

        private void HideToast()
        {
            _dismissTimer.Stop();
            var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(180));
            animation.Completed += (_, _) => Hide();
            BeginAnimation(OpacityProperty, animation);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => HideToast();
    }
}
