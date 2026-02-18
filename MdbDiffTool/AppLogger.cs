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

        private static volatile bool _loggingDisabled;
        private static bool _initialized;

        private static string _configuredDirectory; // то, что пришло из конфига (может быть null)
        private static string _activeDirectory;     // реально используемая папка
        private static string _logFilePath;         // текущий файл лога (по дате)

        /// <summary>
        /// Настраивает папку логов.
        /// Важно вызывать как можно раньше (до первого Info/Error), иначе часть старта окажется в дефолтной папке.
        /// </summary>
        public static void Configure(string logsDirectory)
        {
            lock (_sync)
            {
                _configuredDirectory = string.IsNullOrWhiteSpace(logsDirectory) ? null : logsDirectory.Trim();

                // Сброс инициализации: следующий Write пересоздаст путь.
                _initialized = false;
                _loggingDisabled = false;
                _activeDirectory = null;
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

        /// <summary>
        /// Текущая папка, куда пишет логгер (с учётом конфига).
        /// </summary>
        public static string GetLogDirectory()
        {
            EnsureInitialized();
            return _activeDirectory;
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            lock (_sync)
            {
                if (_initialized)
                    return;

                try
                {
                    var dir = _configuredDirectory;
                    if (string.IsNullOrWhiteSpace(dir))
                        dir = AppPaths.LogsDirectory;

                    // Пытаемся создать папку. Если не получается — фолбэк на дефолт.
                    try
                    {
                        Directory.CreateDirectory(dir);
                    }
                    catch
                    {
                        dir = AppPaths.LogsDirectory;
                        Directory.CreateDirectory(dir);
                    }

                    _activeDirectory = dir;

                    var fileName = "log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                    _logFilePath = Path.Combine(_activeDirectory, fileName);

                    _initialized = true;
                }
                catch
                {
                    // Логирование не должно ломать приложение.
                    _activeDirectory = null;
                    _logFilePath = null;
                    _initialized = true; // чтобы не пытаться снова и снова
                }
            }
        }

        private static void Write(string level, string message)
        {
            if (_loggingDisabled)
                return;

            EnsureInitialized();

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
