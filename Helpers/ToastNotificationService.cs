using System;

namespace NvidiaCi
{
    public sealed class ToastNotificationService : IDisposable
    {
        private ToastNotificationWindow? _window;

        public bool IsDoNotDisturb { get; set; }

        public void Show(string title, string message, bool isError = false)
        {
            if (IsDoNotDisturb)
                return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _window ??= new ToastNotificationWindow();
                _window.ShowToast(title, message, isError);
            });
        }

        public void Dispose()
        {
            if (_window == null)
                return;

            if (_window.Dispatcher.CheckAccess())
                _window.Close();
            else
                _window.Dispatcher.Invoke(_window.Close);
            _window = null;
        }
    }
}
