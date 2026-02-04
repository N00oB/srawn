using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    /// <summary>
    /// Контроллер сравнения выбранной таблицы (UI-обвязка вокруг TableComparisonService).
    /// Вынесено из Form1 для уменьшения связности и упрощения сопровождения.
    /// </summary>
    internal sealed class TableCompareController
    {
        private readonly TableComparisonService _comparisonService;
        private readonly DiffGridBuilder _diffGridBuilder;
        private readonly DiffFilteringService _diffFilteringService;
        private readonly AppConfig _config;

        public TableCompareController(
            TableComparisonService comparisonService,
            DiffGridBuilder diffGridBuilder,
            DiffFilteringService diffFilteringService,
            AppConfig config)
        {
            _comparisonService = comparisonService ?? throw new ArgumentNullException(nameof(comparisonService));
            _diffGridBuilder = diffGridBuilder ?? throw new ArgumentNullException(nameof(diffGridBuilder));
            _diffFilteringService = diffFilteringService ?? throw new ArgumentNullException(nameof(diffFilteringService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task CompareTableAsync(
            IWin32Window owner,
            string tableName,
            string sourceConnectionString,
            string targetConnectionString,
            DataGridView dgvDiff,
            ProgressBar progressBar,
            Label lblStatus,
            Action<bool, string> setBusy)
        {
            var sw = Stopwatch.StartNew();
            bool ok = false;

            try
            {
                if (string.IsNullOrWhiteSpace(sourceConnectionString) ||
                    string.IsNullOrWhiteSpace(targetConnectionString))
                {
                    MessageBox.Show(owner,
                        "Сначала выбери обе базы (источник и приёмник).",
                        "Не выбраны базы",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(tableName))
                {
                    MessageBox.Show(owner,
                        "Не выбрана таблица для сравнения.",
                        "Таблица не выбрана",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                setBusy?.Invoke(true, "Сравнение таблицы [" + tableName + "]...");
                if (progressBar != null)
                {
                    progressBar.Visible = true;
                    progressBar.Style = ProgressBarStyle.Marquee;
                }

                AppLogger.Info($"Запуск сравнения таблицы '{tableName}'.");

                var swTotal = Stopwatch.StartNew();
                var result = await Task.Run(() =>
                    _comparisonService.CompareTable(sourceConnectionString, targetConnectionString, tableName, _config));
                swTotal.Stop();

                AppLogger.Info(
                    $"Сравнение таблицы '{tableName}' завершено. " +
                    $"Общее время сравнения: {swTotal.Elapsed.TotalMilliseconds:F0} мс. " +
                    $"Отличий: {result.DiffPairs.Count}.");


                // Если схемы отличаются (разные версии Excel/БД), логируем это и продолжаем сравнение.
                if ((result.ColumnsOnlyInSource != null && result.ColumnsOnlyInSource.Length > 0) ||
                    (result.ColumnsOnlyInTarget != null && result.ColumnsOnlyInTarget.Length > 0))
                {
                    AppLogger.Info(
                        $"Схемы таблицы '{tableName}' отличаются. " +
                        $"Только в источнике: {FormatColumnsForLog(result.ColumnsOnlyInSource)}. " +
                        $"Только в приёмнике: {FormatColumnsForLog(result.ColumnsOnlyInTarget)}.");
                }

                _diffGridBuilder.ShowDiff(
                    result.TableName,
                    result.DiffPairs,
                    result.SourceTable.Columns,
                    result.KeyColumns);

                // Применяем активные фильтры/режим отображения колонок
                _diffFilteringService.Apply(dgvDiff);

                if (lblStatus != null)
                {
                    var keyUi = FormatKeyColumnsForUi(result.KeyColumns);
                    lblStatus.Text = "Таблица [" + result.TableName + "]: " +
                                     "найдено различий = " + result.DiffPairs.Count +
                                     ". Ключ: " + keyUi;
                }

                // Логируем факт сравнения, но не спамим лог огромными списками колонок.
                var keyLog = FormatKeyColumnsForLog(result.KeyColumns);
                AppLogger.Info($"Сравнение таблицы '{result.TableName}' завершено. Отличий: {result.DiffPairs.Count}. Ключ: {keyLog}.");


                ok = true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Ошибка при сравнении таблицы '{tableName}'. {ex}");
                MessageBox.Show(owner,
                    "Ошибка при сравнении таблицы '" + tableName + "'.\r\n\r\n" + ex.Message,
                    "Ошибка сравнения",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (progressBar != null)
                {
                    progressBar.Visible = false;
                    progressBar.Style = ProgressBarStyle.Blocks;
                }

                sw.Stop();
                setBusy?.Invoke(false, null);

                if (ok && lblStatus != null && !string.IsNullOrWhiteSpace(lblStatus.Text))
                {
                    lblStatus.Text += $" .Время: {sw.Elapsed.TotalSeconds:F3} с.";
                }
            }
        }

        private static string FormatKeyColumnsForUi(string[] keyColumns)
        {
            if (keyColumns == null || keyColumns.Length == 0)
                return "<нет>";

            // Для UI стараемся не показывать огромные списки.
            if (keyColumns.Length <= 5)
                return string.Join(", ", keyColumns);

            return keyColumns.Length + " колонок";
        }


        private static string FormatColumnsForLog(string[] columns)
        {
            if (columns == null || columns.Length == 0)
                return "нет";

            const int max = 20;
            if (columns.Length <= max)
                return string.Join(", ", columns);

            var head = string.Join(", ", columns, 0, max);
            return head + $" ... (+{columns.Length - max})";
        }

        private static string FormatKeyColumnsForLog(string[] keyColumns)
        {
            if (keyColumns == null || keyColumns.Length == 0)
                return "<нет>";

            // Для лога тоже ограничиваем вывод.
            if (keyColumns.Length <= 8)
                return string.Join(", ", keyColumns);

            return keyColumns.Length + " колонок";
        }

    }
}
