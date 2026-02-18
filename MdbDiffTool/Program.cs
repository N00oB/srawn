using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MdbDiffTool
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Настройка логов должна быть ДО первого сообщения.
            // Читаем конфиг максимально "тихо" — без использования AppLogger.
            try
            {
                var bootCfg = ConfigBootstrap.TryLoad(AppPaths.ConfigPath);
                AppLogger.Configure(bootCfg?.LogsDirectory);
            }
            catch
            {
                // игнорируем
            }

            // Глобальные обработчики исключений.
            // Нужны, чтобы приложение не "молча закрывалось" на чужих ПК.
            Application.ThreadException += (s, e) =>
            {
                try { AppLogger.Error("Необработанное исключение в UI-потоке.", e.Exception); } catch { }
                MessageBox.Show(
                    e.Exception?.Message ?? "Неизвестная ошибка.",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                try { AppLogger.Error("Необработанное исключение (AppDomain).", ex); } catch { }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try { AppLogger.Error("Необработанное исключение задачи (TaskScheduler).", e.Exception); } catch { }
                e.SetObserved();
            };

            try
            {
                AppLogger.Info("Запуск приложения.");
                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                AppLogger.Error("Критическая ошибка при запуске приложения.", ex);
                MessageBox.Show(
                    ex.ToString(),
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
