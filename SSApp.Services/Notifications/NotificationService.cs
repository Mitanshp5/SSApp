using System;

namespace SSApp.Services.Notifications
{
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    public class NotificationEventArgs : EventArgs
    {
        public string Message { get; }
        public NotificationType Type { get; }
        public int DurationSeconds { get; }

        public NotificationEventArgs(string message, NotificationType type, int durationSeconds)
        {
            Message = message;
            Type = type;
            DurationSeconds = durationSeconds;
        }
    }

    public static class NotificationService
    {
        public static event EventHandler<NotificationEventArgs>? OnNotificationRequest;

        public static void Show(string message, NotificationType type = NotificationType.Info, int durationSeconds = 3)
        {
            OnNotificationRequest?.Invoke(null, new NotificationEventArgs(message, type, durationSeconds));
        }

        public static void ShowSuccess(string message) => Show(message, NotificationType.Success);
        public static void ShowError(string message) => Show(message, NotificationType.Error, 5);
        public static void ShowWarning(string message) => Show(message, NotificationType.Warning, 4);
        public static void ShowInfo(string message) => Show(message, NotificationType.Info);
    }
}
