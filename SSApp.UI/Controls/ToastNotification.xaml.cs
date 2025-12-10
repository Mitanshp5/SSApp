using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SSApp.Services.Notifications;

namespace SSApp.UI.Controls
{
    public partial class ToastNotification : UserControl
    {
        private DispatcherTimer _timer;
        private Action _onClosed;

        public ToastNotification(string message, NotificationType type, int durationSeconds, Action onClosed)
        {
            InitializeComponent();
            _onClosed = onClosed;

            MessageText.Text = message;
            ApplyStyle(type);

            Loaded += ToastNotification_Loaded;

            if (durationSeconds > 0)
            {
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(durationSeconds);
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }
        }

        private void ApplyStyle(NotificationType type)
        {
            // Simple color coding
            switch (type)
            {
                case NotificationType.Success:
                    StatusIndicator.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
                    break;
                case NotificationType.Error:
                    StatusIndicator.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                    break;
                case NotificationType.Warning:
                    StatusIndicator.Background = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                    break;
                case NotificationType.Info:
                default:
                    StatusIndicator.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // Blue
                    break;
            }
        }

        private void ToastNotification_Loaded(object sender, RoutedEventArgs e)
        {
            var sb = this.Resources["SlideIn"] as Storyboard;
            sb?.Begin(this);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timer.Stop();
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _timer?.Stop();
            Close();
        }

        private void Close()
        {
            var sb = this.Resources["FadeOut"] as Storyboard;
            if (sb != null)
            {
                sb.Completed += (s, e) => _onClosed?.Invoke();
                sb.Begin(this);
            }
            else
            {
                _onClosed?.Invoke();
            }
        }
    }
}
