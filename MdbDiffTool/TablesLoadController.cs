using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    /// <summary>
    /// Контроллер загрузки таблиц и сводного сравнения.
    /// Вынесено из Form1 для уменьшения размера UI-класса и повышения читаемости кода.
    /// </summary>
    internal sealed class TablesLoadController
    {
        private readonly IDatabaseProvider _dbProvider;
        private readonly TableComparisonService _comparisonService;

        public TablesLoadController(IDatabaseProvider dbProvider, TableComparisonService comparisonService)
        {
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
            _comparisonService = comparisonService ?? throw new ArgumentNullException(nameof(comparisonService));
        }

        public async Task<TablesLoadResult> LoadAndCompareAsync(
            string sourceConnectionString,
            string targetConnectionString,
            HashSet<string> excludedTables,
            AppConfig config,
            CancellationToken token,
            IProgress<TablesDiffProgress> progress)
        {
            if (sourceConnectionString == null) throw new ArgumentNullException(nameof(sourceConnectionString));
            if (targetConnectionString == null) throw new ArgumentNullException(nameof(targetConnectionString));
            if (config == null) throw new ArgumentNullException(nameof(config));

            excludedTables ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Пакетный режим (ускорение серии вызовов к одной и той же БД).
            // Особенно полезно для Access (OLEDB), чтобы не открывать соединение заново для каждой таблицы.
            var batchProvider = _dbProvider as IBatchDatabaseProvider;
            batchProvider?.BeginBatch(sourceConnectionString);
            batchProvider?.BeginBatch(targetConnectionString);

            try
            {

            // 1) читаем имена таблиц (параллельно)
            var srcTablesTask = Task.Run(() => _dbProvider.GetTableNames(sourceConnectionString), token);
            var tgtTablesTask = Task.Run(() => _dbProvider.GetTableNames(targetConnectionString), token);

            await Task.WhenAll(srcTablesTask, tgtTablesTask).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            var srcTables = srcTablesTask.Result ?? new List<string>();
            var tgtTables = tgtTablesTask.Result ?? new List<string>();

            // 2) общие таблицы с учётом исключений
            var common = srcTables
                .Where(t => !excludedTables.Contains(t))
                .Intersect(
                    tgtTables.Where(t => !excludedTables.Contains(t)),
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();

            if (common.Count == 0)
                return TablesLoadResult.Empty();

            // Параллелизм по таблицам оставляем как в конфиге (MaxParallelTables).
            // Для Access все обращения к OleDb уже выполняются через STA-очередь,
            // поэтому мы не получаем падения от MTA, а CPU-часть сравнения выигрывает от параллелизма.
            var effectiveConfig = config;

            // 3) сводное сравнение (в фоне)
            var summaries = await Task.Run(() =>
                _comparisonService.CompareTablesSummary(
                    sourceConnectionString,
                    targetConnectionString,
                    common,
                    effectiveConfig,
                    token,
                    progress),
                token).ConfigureAwait(false);

            return new TablesLoadResult(common, summaries ?? new List<TableDiffSummary>());
            }
            finally
            {
                batchProvider?.EndBatch(sourceConnectionString);
                batchProvider?.EndBatch(targetConnectionString);
            }
        }
    }

    internal sealed class TablesLoadResult
    {
        public List<string> CommonTables { get; }
        public List<TableDiffSummary> Summaries { get; }

        public int CommonTablesCount => CommonTables?.Count ?? 0;

        public int TablesWithDiffCount => Summaries?.Count(s => s.TotalDiffCount > 0) ?? 0;

        public TablesLoadResult(List<string> commonTables, List<TableDiffSummary> summaries)
        {
            CommonTables = commonTables ?? new List<string>();
            Summaries = summaries ?? new List<TableDiffSummary>();
        }

        public static TablesLoadResult Empty()
        {
            return new TablesLoadResult(new List<string>(), new List<TableDiffSummary>());
        }
    }
}
