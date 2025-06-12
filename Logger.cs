using System;
using System.IO;
using System.Threading.Tasks;

namespace PiperTray
{
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "system.log");
        private static readonly object LogLock = new object();
        public static bool IsEnabled { get; set; } = false;

        public static void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public static void Error(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
            WriteLog("ERROR", fullMessage);
        }

        public static void Debug(string message)
        {
            WriteLog("DEBUG", message);
        }

        public static void Warn(string message)
        {
            WriteLog("WARN", message);
        }

        private static void WriteLog(string level, string message)
        {
            if (!IsEnabled) return;
            
            lock (LogLock)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var logEntry = $"[{timestamp}] [{level}] {message}{Environment.NewLine}";
                    File.AppendAllText(LogPath, logEntry);
                }
                catch
                {
                    // If logging fails, don't crash the application
                }
            }
        }

        public static void ClearLog()
        {
            lock (LogLock)
            {
                try
                {
                    if (File.Exists(LogPath))
                        File.Delete(LogPath);
                }
                catch
                {
                    // Ignore if we can't clear the log
                }
            }
        }
    }
}