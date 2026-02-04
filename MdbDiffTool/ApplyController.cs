using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    /// <summary>
    /// Контроллер операций применения (применить выбранные строки diff / перезалить выбранные таблицы).
    /// Вынесено из Form1 для уменьшения размера UI-класса и упрощения сопровождения.
    /// </summary>
    internal sealed class ApplyController
    {
        private readonly DiffApplyService _diffApplyService;
        private readonly IDatabaseProvider _dbProvider;

        public ApplyController(DiffApplyService diffApplyService, IDatabaseProvider dbProvider)
        {
            _diffApplyService = diffApplyService ?? throw new ArgumentNullException(nameof(diffApplyService));
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        }

        public async Task ApplySelectedDiffRowsAsync(
            IWin32Window owner,
            DataGridView dgvDiff,
            Func<string> getTargetConnectionString,
            Func<string> getSelectedTableName,
            Func<string, Task> recompareTableAsync,
            Action<bool, string> setBusy)
        {
            if (dgvDiff == null)
                throw new ArgumentNullException(nameof(dgvDiff));
            if (getTargetConnectionString == null)
                throw new ArgumentNullException(nameof(getTargetConnectionString));
            if (setBusy == null)
                throw new ArgumentNullException(nameof(setBusy));

            // Быстрые проверки без перехода в busy
            var ctx = dgvDiff.Tag as DiffContext;
            if (ctx == null)
            {
                MessageBox.Show(owner,
                    "Нет данных для применения. Сначала сравни таблицу.",
                    "Нет diff",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var toApply = _diffApplyService.CollectPairsToApply(dgvDiff);
            if (toApply.Count == 0)
            {
                MessageBox.Show(owner,
                    "Нет отмеченных строк для применения.",
                    "Нечего применять",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            try
            {
                setBusy(true, "Применение выбранных строк...");

                string tgtConnStr = getTargetConnectionString();

                if (!EnsureCapabilities(owner, tgtConnStr, ProviderCapabilities.ApplyRowChanges, "Применение изменений (по строкам)", "базы-приёмника"))
                    return;

                await _diffApplyService.ApplyAsync(tgtConnStr, ctx, toApply);

                MessageBox.Show(owner,
                    "Изменения успешно применены в дорабатываемую базу.",
                    "Готово",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Пересравниваем текущую таблицу (если можем определить её имя)
                if (recompareTableAsync != null)
                {
                    string tableName = getSelectedTableName?.Invoke();
                    if (!string.IsNullOrWhiteSpace(tableName))
                    {
                        await recompareTableAsync(tableName);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                // Обычно: не выбран путь к базе/строка подключения
                MessageBox.Show(owner,
                    ex.Message,
                    "Не выбрана база",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка при применении изменений.", ex);
                MessageBox.Show(owner,
                    "Ошибка при применении изменений:\r\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                setBusy(false, null);
            }
        }

        public async Task ReplaceCheckedTablesAsync(
            IWin32Window owner,
            Func<string> getSourceConnectionString,
            Func<string> getTargetConnectionString,
            Func<List<string>> getCheckedTableNames,
            ProgressBar progressBar,
            Label lblStatus,
            Action<bool, string> setBusy,
            Action<IReadOnlyList<string>> onTablesReplacedSuccessfully = null)
        {
            if (getSourceConnectionString == null)
                throw new ArgumentNullException(nameof(getSourceConnectionString));
            if (getTargetConnectionString == null)
                throw new ArgumentNullException(nameof(getTargetConnectionString));
            if (getCheckedTableNames == null)
                throw new ArgumentNullException(nameof(getCheckedTableNames));
            if (setBusy == null)
                throw new ArgumentNullException(nameof(setBusy));

            var tableNames = getCheckedTableNames() ?? new List<string>();

            if (tableNames.Count == 0)
            {
                MessageBox.Show(owner,
                    "Не выбраны таблицы для перезаливки.\n\nОтметь галочкой таблицы в списке слева.",
                    "Нет выбранных таблиц",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string msg;
            if (tableNames.Count == 1)
            {
                msg = "Полностью заменить данные таблицы [" + tableNames[0] + "] в дорабатываемой базе\n" +
                      "данными из базы объекта?\n\n" +
                      "Все существующие строки в этой таблице будут удалены.";
            }
            else
            {
                var previewList = string.Join(
                    Environment.NewLine + "  - ",
                    tableNames.Take(10));

                if (tableNames.Count > 10)
                {
                    previewList += Environment.NewLine + $"  ... и ещё {tableNames.Count - 10} таблиц";
                }

                msg =
                    "Полностью заменить данные в выбранных таблицах дорабатываемой базы\n" +
                    "данными из базы объекта?\n\n" +
                    "Все существующие строки в этих таблицах будут удалены.\n\n" +
                    "Таблицы:\n  - " + previewList;
            }

            var confirm = MessageBox.Show(owner,
                msg,
                "Подтверждение перезаливки",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes)
                return;

            try
            {
                setBusy(true, "Перезаливка выбранных таблиц...");

                string srcConnStr = getSourceConnectionString();
                string tgtConnStr = getTargetConnectionString();

                if (!EnsureCapabilities(owner, tgtConnStr, ProviderCapabilities.ReplaceTable, "Полная замена таблицы", "базы-приёмника"))
                    return;

                if (!EnsureCapabilities(owner, tgtConnStr, ProviderCapabilities.ApplyRowChanges, "Применение изменений (по строкам)", "базы-приёмника"))
                    return;

                if (progressBar != null)
                {
                    progressBar.Visible = true;
                    progressBar.Style = ProgressBarStyle.Blocks;
                    progressBar.Minimum = 0;
                    progressBar.Maximum = tableNames.Count;
                    progressBar.Value = 0;
                }

                var progress = new Progress<int>(done =>
                {
                    if (progressBar != null && done <= progressBar.Maximum)
                        progressBar.Value = done;

                    if (lblStatus != null)
                        lblStatus.Text = "Перезаливка выбранных таблиц: " + done + " / " + tableNames.Count;
                });

                await BulkReplaceTablesAsync(srcConnStr, tgtConnStr, tableNames, progress);

                // Если перезаливка прошла без ошибок — обновляем UI-слой (список таблиц/сводку)
                onTablesReplacedSuccessfully?.Invoke(tableNames);


                MessageBox.Show(owner,
                    "Выбранные таблицы успешно перезалиты.",
                    "Готово",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(owner,
                    ex.Message,
                    "Не выбрана база",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка при перезаливке выбранных таблиц.", ex);
                MessageBox.Show(owner,
                    "Ошибка при перезаливке выбранных таблиц:\r\n" + ex.Message,
                    "Ошибка",
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

                setBusy(false, null);
            }
        }



            private bool EnsureCapabilities(
                IWin32Window owner,
                string connectionString,
                ProviderCapabilities required,
                string operationName,
                string sideLabel)
            {
                try
                {
                    var caps = _dbProvider.GetCapabilities(connectionString);

                    if ((caps & required) == required)
                        return true;

                    string msg =
                        $"{sideLabel} не поддерживает операцию: {operationName}.

" +
                        "Источник данных работает в режиме "только чтение" или не реализует требуемые операции.";

                    AppLogger.Info(msg.Replace("
", " "));
                    MessageBox.Show(owner, msg, "Операция недоступна", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Не удалось определить возможности провайдера для {sideLabel}.", ex);
                    MessageBox.Show(
                        owner,
                        $"Не удалось определить возможности провайдера для {sideLabel}:
{ex.Message}",
                        "Ошибка",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return false;
                }
            }

        private Task BulkReplaceTablesAsync(
            string srcConnStr,
            string tgtConnStr,
            List<string> tableNames,
            IProgress<int> progress)
        {
            return Task.Run(() =>
            {
                int processed = 0;

                foreach (var tableName in tableNames)
                {
                    // 1. Загружаем таблицу из источника
                    var srcTable = _dbProvider.LoadTable(srcConnStr, tableName);

                    // 2. Полностью заменяем таблицу в приёмнике
                    _dbProvider.ReplaceTable(tgtConnStr, tableName, srcTable);

                    processed++;
                    progress?.Report(processed);
                }
            });
        }
    }
}
