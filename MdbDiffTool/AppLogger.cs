using System;
using System.IO;
using System.Text;

namespace MdbDiffTool
{
    /// <summary>
    /// Простейший файловый логгер.
    /// Все сообщения на русском.
    /// </summary>
    internal static class AppLogger
    {
        private static readonly object _sync = new object();
        private static readonly string _logDirectory;
        private static readonly string _logFilePath;
        private static volatile bool _loggingDisabled;

        static AppLogger()
        {
            try
            {
                // Логи должны писаться в профиль пользователя, иначе в "Program Files"
                // и других защищённых местах логирование просто отключится.
                _logDirectory = AppPaths.LogsDirectory;
                Directory.CreateDirectory(_logDirectory);

                var fileName = "log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                _logFilePath = Path.Combine(_logDirectory, fileName);
            }
            catch
            {
                // Логирование не должно ломать приложение.
                _logDirectory = null;
                _logFilePath = null;
            }
        }

        public static void Info(string message)
        {
            Write("ИНФО", message);
        }

        public static void Error(string message, Exception ex = null)
        {
            if (ex != null)
                message = message + Environment.NewLine + ex;
            Write("ОШИБКА", message);
        }

        private static void Write(string level, string message)
        {
            if (_loggingDisabled)
                return;

            if (string.IsNullOrEmpty(_logFilePath))
                return;

            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                lock (_sync)
                {
                    File.AppendAllText(_logFilePath, line, Encoding.UTF8);
                }
            }
            catch
            {
                // Важно: нельзя логировать из логгера, иначе возможна рекурсия и падение приложения.
                _loggingDisabled = true;
            }
        }
    }
}
