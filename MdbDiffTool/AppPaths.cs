using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MdbDiffTool
{
    /// <summary>
    /// Единые пути приложения.
    /// Важно: конфиг и логи должны храниться в пользовательском профиле,
    /// чтобы приложение работало без прав администратора.
    /// </summary>
    internal static class AppPaths
    {
        private const string ConfigFileName = "MdbDiffTool.config.xml";
        private const string ConfigDirectoryOverrideFileBaseName = "ConfigDirectory.override";
        private const string ConfigDirectoryOverrideFileExtension = ".txt";
        private const string LegacyConfigDirectoryOverrideFileName = "ConfigDirectory.override.txt";

        /// <summary>
        /// Базовая папка приложения в профиле пользователя.
        /// </summary>
        public static string AppDataDirectory
        {
            get
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrWhiteSpace(baseDir))
                {
                    // Фолбэк — рядом с exe (крайний случай)
                    baseDir = AppDomain.CurrentDomain.BaseDirectory;
                }

                var dir = Path.Combine(baseDir, "MdbDiffTool");
                TryCreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>
        /// Папка логов по умолчанию.
        /// </summary>
        public static string LogsDirectory
        {
            get
            {
                var dir = Path.Combine(AppDataDirectory, "Logs");
                TryCreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>
        /// Текущая папка конфигурации (может быть переопределена пользователем).
        /// Переопределение хранится в файле рядом с AppDataDirectory.
        /// </summary>
        public static string ConfigDirectory
        {
            get
            {
                // 1) Пытаемся читать переопределение, привязанное к конкретному экземпляру приложения
                // (т.е. к пути exe). Это позволяет нескольким копиям приложения на одной машине
                // иметь разные папки конфигурации.
                var overridePath = GetConfigDirectoryOverridePath();
                var dir = TryReadFirstLine(overridePath);

                // 2) Миграция со старого глобального файла (если был задан до обновления)
                if (string.IsNullOrWhiteSpace(dir))
                {
                    var legacyPath = GetLegacyConfigDirectoryOverridePath();
                    var legacyDir = TryReadFirstLine(legacyPath);
                    if (!string.IsNullOrWhiteSpace(legacyDir))
                    {
                        legacyDir = legacyDir.Trim().Trim('"');
                        if (TryCreateDirectorySafe(legacyDir))
                        {
                            TryWriteAllTextSafe(overridePath, legacyDir);
                            dir = legacyDir;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(dir))
                {
                    dir = dir.Trim().Trim('"');
                    if (TryCreateDirectorySafe(dir))
                        return dir;
                }

                // По умолчанию — профиль пользователя.
                return AppDataDirectory;
            }
        }

        /// <summary>
        /// Путь к файлу конфигурации.
        /// </summary>
        public static string ConfigPath
        {
            get
            {
                // Конфиг не рядом с exe, иначе в защищённых папках он не сохранится.
                return Path.Combine(ConfigDirectory, ConfigFileName);
            }
        }

        /// <summary>
        /// Устанавливает папку конфигурации.
        /// Переопределение сохраняется в файл, чтобы пережить перезапуск приложения.
        /// </summary>
        public static void SetConfigDirectory(string directory)
        {
            try
            {
                var overridePath = GetConfigDirectoryOverridePath();

                if (string.IsNullOrWhiteSpace(directory))
                {
                    if (File.Exists(overridePath))
                        File.Delete(overridePath);
                    return;
                }

                directory = directory.Trim().Trim('"');

                // Создаём папку заранее. Если нельзя — исключение уйдёт в catch.
                Directory.CreateDirectory(directory);

                File.WriteAllText(overridePath, directory);
            }
            catch
            {
                // Не валим приложение.
            }
        }

        private static string GetConfigDirectoryOverridePath()
        {
            try
            {
                var exePath = GetExePath();
                var key = ComputeStableKey(exePath);

                var fileName = ConfigDirectoryOverrideFileBaseName + "." + key + ConfigDirectoryOverrideFileExtension;
                return Path.Combine(AppDataDirectory, fileName);
            }
            catch
            {
                // Фолбэк (крайний случай)
                return Path.Combine(AppDataDirectory, LegacyConfigDirectoryOverrideFileName);
            }
        }

        private static string GetLegacyConfigDirectoryOverridePath()
        {
            return Path.Combine(AppDataDirectory, LegacyConfigDirectoryOverrideFileName);
        }

        private static string GetExePath()
        {
            try
            {
                var entry = Assembly.GetEntryAssembly();
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Location))
                    return entry.Location;
            }
            catch
            {
                // ignore
            }

            try
            {
                var p = Process.GetCurrentProcess();
                if (p != null && p.MainModule != null && !string.IsNullOrWhiteSpace(p.MainModule.FileName))
                    return p.MainModule.FileName;
            }
            catch
            {
                // ignore
            }

            // Фолбэк
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string ComputeStableKey(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
                exePath = string.Empty;

            // Нормализуем регистр/разделители, чтобы ключ был максимально стабильным.
            var normalized = exePath.Trim().Replace('/', '\\').ToLowerInvariant();

            // Короткий ключ, чтобы не раздувать имя файла: 8 hex-символов.
            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(normalized);
                var hash = sha1.ComputeHash(bytes);

                var sb = new StringBuilder(8);
                // 4 байта = 8 hex символов.
                for (int i = 0; i < 4; i++)
                    sb.Append(hash[i].ToString("x2"));

                return sb.ToString();
            }
        }

        private static void TryWriteAllTextSafe(string path, string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                File.WriteAllText(path, text ?? string.Empty);
            }
            catch
            {
                // ignore
            }
        }

        private static string TryReadFirstLine(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return null;

                using (var sr = new StreamReader(path))
                {
                    return sr.ReadLine();
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool TryCreateDirectorySafe(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return false;

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryCreateDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }
            catch
            {
                // Ничего: пути не должны ломать приложение.
            }
        }
    }
}
