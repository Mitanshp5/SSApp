using System;
using System.IO;

namespace SSApp.Services.Logging
{
    public static class Logger
    {
        private static readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        static Logger()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public static void LogInformation(string message) => Log("INFO", message);
        public static void LogWarning(string message) => Log("WARN", message);
        public static void LogError(string message, Exception? ex = null)
        {
            string msg = message;
            if (ex != null)
            {
                msg += $" | Exception: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
            Log("ERROR", msg);
        }

        private static void Log(string level, string message)
        {
            try
            {
                string filename = $"app_{DateTime.Now:yyyyMMdd}.log";
                string path = Path.Combine(_logDirectory, filename);
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";

                // Simple append, ideally we'd use a queue for high volume but this is a desktop app
                File.AppendAllText(path, logEntry);
            }
            catch
            {
                // Last resort: fail silently or debug output
                System.Diagnostics.Debug.WriteLine($"Failed to log: {message}");
            }
        }
    }
}
