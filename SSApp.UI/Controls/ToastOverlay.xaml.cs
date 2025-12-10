using System.Windows;
using System.Windows.Controls;
using SSApp.Services.Notifications;

namespace SSApp.UI.Controls
{
    public partial class ToastOverlay : UserControl
    {
        public ToastOverlay()
        {
            InitializeComponent();
            Loaded += ToastOverlay_Loaded;
            Unloaded += ToastOverlay_Unloaded;
        }

        private void ToastOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            NotificationService.OnNotificationRequest += NotificationService_OnNotificationRequest;
        }

        private void ToastOverlay_Unloaded(object sender, RoutedEventArgs e)
        {
            NotificationService.OnNotificationRequest -= NotificationService_OnNotificationRequest;
        }

        private void NotificationService_OnNotificationRequest(object? sender, NotificationEventArgs e)
        {
            // Must run on UI thread
            Dispatcher.Invoke(() =>
            {
                var toast = new ToastNotification(e.Message, e.Type, e.DurationSeconds, () =>
                {
                    // Remove self when closed
                    // We need to capture 'toast' variable safely
                    RemoveToast(null); 
                });

                // HACK: The callback above needs reference to the toast instance, 
                // but the toast instance is being created.
                // Let's fix the Remove logic.
                
                // Better approach:
                AddToast(e.Message, e.Type, e.DurationSeconds);
            });
        }

        private void AddToast(string message, NotificationType type, int duration)
        {
            ToastNotification toast = null;
            toast = new ToastNotification(message, type, duration, () =>
            {
                if (toast != null)
                {
                    ToastStack.Children.Remove(toast);
                }
            });

            // Add to top or bottom? 
            // Standard is new on top for bottom-aligned stacks, 
            // or new on bottom? 
            // If VerticalAlignment=Bottom, the stack grows upwards if we add to children? 
            // No, StackPanel grows down. 
            // If we want it to grow UP from bottom-right, we should probably set VerticalAlignment=Bottom on the stackpanel 
            // and add new items to the END (bottom), pushing old ones up? 
            // Actually, if it's bottom aligned, adding to Children (bottom of list) will push the panel DOWN (if there was space) or grow it UP?
            
            // If StackPanel is VerticalAlignment=Bottom:
            // [Toast 1]
            // [Toast 2] (Newest)
            // Bottom of Screen
            
            // This is fine.
            ToastStack.Children.Add(toast);
        }

        private void RemoveToast(ToastNotification? toast)
        {
            // Handled in AddToast closure
        }
    }
}
