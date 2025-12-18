using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using SSApp.Services.Notifications;

namespace SSApp.UI.Controls
{
    public partial class ToastOverlay : UserControl
    {
        private Window _parentWindow;

        public ToastOverlay()
        {
            InitializeComponent();
            Loaded += ToastOverlay_Loaded;
            Unloaded += ToastOverlay_Unloaded;
        }

        private void ToastOverlay_Loaded(object sender, RoutedEventArgs e)
        {
            NotificationService.OnNotificationRequest += NotificationService_OnNotificationRequest;

            // Find parent window to track movement/resizing
            _parentWindow = Window.GetWindow(this);
            if (_parentWindow != null)
            {
                _parentWindow.LocationChanged += ParentWindow_LocationChanged;
                _parentWindow.SizeChanged += ParentWindow_SizeChanged;
                _parentWindow.StateChanged += ParentWindow_StateChanged;
                _parentWindow.LayoutUpdated += ParentWindow_LayoutUpdated; // Important for initial accurate positioning

                // Initial positioning
                UpdatePopupPosition();
                OverlayPopup.IsOpen = true;
            }
        }

        private void ToastOverlay_Unloaded(object sender, RoutedEventArgs e)
        {
            NotificationService.OnNotificationRequest -= NotificationService_OnNotificationRequest;

            if (_parentWindow != null)
            {
                _parentWindow.LocationChanged -= ParentWindow_LocationChanged;
                _parentWindow.SizeChanged -= ParentWindow_SizeChanged;
                _parentWindow.StateChanged -= ParentWindow_StateChanged;
                _parentWindow.LayoutUpdated -= ParentWindow_LayoutUpdated;
                _parentWindow = null;
            }
            
            // Close popup when control is removed
            OverlayPopup.IsOpen = false;
        }

        private void ParentWindow_LocationChanged(object? sender, EventArgs e) => UpdatePopupPosition();
        private void ParentWindow_SizeChanged(object sender, SizeChangedEventArgs e) => UpdatePopupPosition();
        private void ParentWindow_StateChanged(object? sender, EventArgs e) => UpdatePopupPosition();
        private void ParentWindow_LayoutUpdated(object? sender, EventArgs e) => UpdatePopupPosition();

        private void UpdatePopupPosition()
        {
            if (_parentWindow == null || !OverlayPopup.IsOpen) return;

            // Get the screen coordinates of the parent window
            var windowTopLeft = _parentWindow.PointToScreen(new Point(0, 0));

            // Get the actual size of the window client area
            // For WindowStyle="None" and maximized, ActualWidth/Height might be full screen,
            // but we need to respect the work area if the app doesn't explicitly cover the taskbar.
            // However, since it's WindowStyle="None", it usually covers the taskbar in maximized state.
            // We'll use ActualWidth/Height as the reference.
            double windowWidth = _parentWindow.ActualWidth;
            double windowHeight = _parentWindow.ActualHeight;

            // Get the desired size of the StackPanel (ToastStack)
            // Use DesiredSize to get the size the StackPanel wants to be after layout.
            // We need to measure it if it hasn't been rendered yet.
            if (ToastStack.ActualWidth == 0 || ToastStack.ActualHeight == 0)
            {
                ToastStack.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                ToastStack.Arrange(new Rect(ToastStack.DesiredSize));
            }
            
            double stackWidth = ToastStack.ActualWidth;
            double stackHeight = ToastStack.ActualHeight;

            // Extract margin values (assuming uniform or specific values)
            // Current margin: Margin="20,20,20,80" -> Left, Top, Right, Bottom
            double rightMargin = 20;
            double bottomMargin = 80;

            // Calculate target position for the bottom-right corner of the stack
            double targetX = windowTopLeft.X + windowWidth - stackWidth - rightMargin;
            double targetY = windowTopLeft.Y + windowHeight - stackHeight - bottomMargin;

            // Set the absolute position of the Popup
            OverlayPopup.HorizontalOffset = targetX;
            OverlayPopup.VerticalOffset = targetY;
        }

        private void NotificationService_OnNotificationRequest(object? sender, NotificationEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                AddToast(e.Message, e.Type, e.DurationSeconds);
                UpdatePopupPosition(); // Update position after adding a new toast (stack height might change)
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
                    UpdatePopupPosition(); // Update position after removing a toast (stack height might change)
                }
            });

            ToastStack.Children.Add(toast);
        }
    }
}
