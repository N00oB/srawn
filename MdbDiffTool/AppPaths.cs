using System;
using System.IO;

namespace MdbDiffTool
{
    /// <summary>
    /// Единые пути приложения.
    /// Важно: конфиг и логи должны храниться в пользовательском профиле,
    /// чтобы приложение работало без прав администратора.
    /// </summary>
    internal static class AppPaths
    {
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

        public static string LogsDirectory
        {
            get
            {
                var dir = Path.Combine(AppDataDirectory, "Logs");
                TryCreateDirectory(dir);
                return dir;
            }
        }

        public static string ConfigPath
        {
            get
            {
                // Конфиг не рядом с exe, иначе в защищённых папках он не сохранится.
                return Path.Combine(AppDataDirectory, "MdbDiffTool.config.xml");
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
