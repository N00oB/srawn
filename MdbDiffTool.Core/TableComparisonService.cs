using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace MdbDiffTool.Core
{
    /// <summary>
    /// Сервис сценариев сравнения таблиц.
    /// Не знает про UI, только про IDatabaseProvider, AppConfig и DiffEngine.
    /// </summary>
    public sealed class TableComparisonService
    {
        private readonly IDatabaseProvider _dbProvider;

        public TableComparisonService(IDatabaseProvider dbProvider)
        {
            _dbProvider = dbProvider ?? throw new ArgumentNullException(nameof(dbProvider));
        }

        /// <summary>
        /// Получить количество различий для одной таблицы.
        /// </summary>
        public int GetDiffCountForTable(
            string srcConnStr,
            string tgtConnStr,
            string tableName,
            AppConfig config)
        {
            var batch = _dbProvider as IBatchDatabaseProvider;
            batch?.BeginBatch(srcConnStr);
            batch?.BeginBatch(tgtConnStr);

            try
            {
                var sourceTable = _dbProvider.LoadTable(srcConnStr, tableName);
                var targetTable = _dbProvider.LoadTable(tgtConnStr, tableName);

                var pkSrc = _dbProvider.GetPrimaryKeyColumns(srcConnStr, tableName);
                var pkTgt = _dbProvider.GetPrimaryKeyColumns(tgtConnStr, tableName);
                var keyColumns = ResolveKeyColumnsForPair(tableName, sourceTable, targetTable, pkSrc, pkTgt, config);

                var diffPairs = DiffEngine.BuildDiff(sourceTable, targetTable, keyColumns);
                return diffPairs.Count;
            }
            finally
            {
                try { batch?.EndBatch(srcConnStr); } catch { }
                try { batch?.EndBatch(tgtConnStr); } catch { }
            }
        }


        /// <summary>
        /// Полное сравнение одной таблицы: схема, ключи, список diff-строк.
        /// </summary>
        public TableDiffResult CompareTable(
            string srcConnStr,
            string tgtConnStr,
            string tableName,
            AppConfig config)
        {
            var batch = _dbProvider as IBatchDatabaseProvider;
            batch?.BeginBatch(srcConnStr);
            batch?.BeginBatch(tgtConnStr);

            try
            {
                DataTable sourceTable = null;
                DataTable targetTable = null;

                // Параллельно загружаем таблицу из источника и приёмника.
                // Каждая загрузка использует своё подключение, безопасно.
                System.Threading.Tasks.Parallel.Invoke(
                    () => { sourceTable = _dbProvider.LoadTable(srcConnStr, tableName); },
                    () => { targetTable = _dbProvider.LoadTable(tgtConnStr, tableName); }
                );

                var pkSrc = _dbProvider.GetPrimaryKeyColumns(srcConnStr, tableName);
                var pkTgt = _dbProvider.GetPrimaryKeyColumns(tgtConnStr, tableName);
                var keyColumns = ResolveKeyColumnsForPair(tableName, sourceTable, targetTable, pkSrc, pkTgt, config);

                var diffPairs = DiffEngine.BuildDiff(sourceTable, targetTable, keyColumns);

                return new TableDiffResult
                {
                    TableName = tableName,
                    SourceTable = sourceTable,
                    KeyColumns = keyColumns,
                    DiffPairs = diffPairs
                };
            }
            finally
            {
                try { batch?.EndBatch(srcConnStr); } catch { }
                try { batch?.EndBatch(tgtConnStr); } catch { }
            }
        }


        /// <summary>
        /// Пакетное сравнение набора таблиц.
        /// Возвращает только краткие сводки (количества отличающихся строк),
        /// без тащения всех DataRow в память.
        /// </summary>
        public List<TableDiffSummary> CompareTablesSummary(
            string srcConnStr,
            string tgtConnStr,
            IEnumerable<string> tableNames,
            AppConfig config,
            CancellationToken cancellationToken,
            IProgress<TablesDiffProgress> progress = null)
        {
            if (tableNames == null)
                throw new ArgumentNullException(nameof(tableNames));

            // Нормализуем список имён таблиц
            var tableList = new List<string>();
            foreach (var t in tableNames)
            {
                if (!string.IsNullOrWhiteSpace(t))
                    tableList.Add(t);
            }

            int total = tableList.Count;
            int processed = 0;

            // Пустой набор — сразу выходим
            if (total == 0)
                return new List<TableDiffSummary>();

            var result = new ConcurrentBag<TableDiffSummary>();

            int maxParallel = ResolveMaxParallelism(config);

            var options = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = maxParallel
            };

            var fast = _dbProvider as IFastRowHashProvider;

            var batch = _dbProvider as IBatchDatabaseProvider;
            try
            {
                batch?.BeginBatch(srcConnStr);
                batch?.BeginBatch(tgtConnStr);

                Parallel.ForEach(tableList, options, tableName =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Если сравниваем одну и ту же БД — отличий быть не может.
                if (string.Equals(srcConnStr, tgtConnStr, StringComparison.Ordinal))
                {
                    var summarySame = new TableDiffSummary
                    {
                        TableName = tableName,
                        OnlyInSourceCount = 0,
                        OnlyInTargetCount = 0,
                        DifferentCount = 0,
                        TotalDiffCount = 0
                    };

                    result.Add(summarySame);

                    var doneSame = Interlocked.Increment(ref processed);
                    progress?.Report(new TablesDiffProgress
                    {
                        Processed = doneSame,
                        Total = total,
                        CurrentTable = tableName
                    });

                    return;
                }

                // --- FAST PATH: без загрузки DataTable (Key->Hash) ---
                if (fast != null)
                {
                    ColumnInfo[] srcColumns = null;
                    ColumnInfo[] tgtColumns = null;

                    try
                    {
                        srcColumns = fast.GetTableColumns(srcConnStr, tableName);
                    }
                    catch
                    {
                        // Если не смогли быстро получить схему — уйдём на обычный режим ниже.
                        srcColumns = null;
                    }

                    try
                    {
                        tgtColumns = fast.GetTableColumns(tgtConnStr, tableName);
                    }
                    catch
                    {
                        tgtColumns = null;
                    }

                    if (srcColumns != null && srcColumns.Length > 0)
                    {
                        // Собираем "псевдо-схему" только ради ResolveKeyColumns (чтобы не менять DiffEngine).
                        var schema = new DataTable();
                        foreach (var c in srcColumns)
                            schema.Columns.Add(c.Name, c.DataType);

                        var pkColumns = _dbProvider.GetPrimaryKeyColumns(srcConnStr, tableName);
                        var keyColumns = ResolveKeyColumns(tableName, schema.Columns, pkColumns, config);

                        // Если ключевые колонки не совпадают со схемой приёмника — fast-path опасен.
                        // Уходим на обычный режим (DataTable), чтобы подобрать ключ безопасно.
                        if (tgtColumns == null || tgtColumns.Length == 0 || !AreColumnsPresent(tgtColumns, keyColumns))
                        {
                            srcColumns = null;
                        }

                        if (srcColumns != null)
                        {

                        Dictionary<string, ulong> srcMap = null;
                        Dictionary<string, ulong> tgtMap = null;

                        // Грузим source/target параллельно, чтобы два разных источника не простаивали.
                        var srcTask = Task.Run(() =>
                            fast.LoadKeyHashMap(srcConnStr, tableName, keyColumns, srcColumns, cancellationToken),
                            cancellationToken);

                        tgtMap = fast.LoadKeyHashMap(tgtConnStr, tableName, keyColumns, srcColumns, cancellationToken);
                        srcMap = srcTask.GetAwaiter().GetResult();

                        int onlySrc = srcMap?.Count ?? 0;
                        int onlyTgt = 0;
                        int different = 0;

                        if (tgtMap != null && srcMap != null)
                        {
                            foreach (var kv in tgtMap)
                            {
                                if (srcMap.TryGetValue(kv.Key, out var srcHash))
                                {
                                    // есть в обеих — значит "только в source" уменьшаем
                                    onlySrc--;
                                    if (srcHash != kv.Value)
                                        different++;
                                }
                                else
                                {
                                    onlyTgt++;
                                }
                            }
                        }
                        else if (tgtMap != null)
                        {
                            onlySrc = 0;
                            onlyTgt = tgtMap.Count;
                        }

                        var summaryFast = new TableDiffSummary
                        {
                            TableName = tableName,
                            OnlyInSourceCount = onlySrc,
                            OnlyInTargetCount = onlyTgt,
                            DifferentCount = different,
                            TotalDiffCount = onlySrc + onlyTgt + different
                        };

                        result.Add(summaryFast);

                        var doneFast = Interlocked.Increment(ref processed);
                        progress?.Report(new TablesDiffProgress
                        {
                            Processed = doneFast,
                            Total = total,
                            CurrentTable = tableName
                        });

                        return;
                        }
                    }
                }

                // --- FALLBACK: старый путь через DataTable (максимальная совместимость) ---
                DataTable sourceTable = null;
                DataTable targetTable = null;

                var srcTask2 = Task.Run(() => _dbProvider.LoadTable(srcConnStr, tableName), cancellationToken);
                targetTable = _dbProvider.LoadTable(tgtConnStr, tableName);
                sourceTable = srcTask2.GetAwaiter().GetResult();

                var pkColumns2 = _dbProvider.GetPrimaryKeyColumns(srcConnStr, tableName);
                var pkColumnsTgt2 = _dbProvider.GetPrimaryKeyColumns(tgtConnStr, tableName);
                var keyColumns2 = ResolveKeyColumnsForPair(tableName, sourceTable, targetTable, pkColumns2, pkColumnsTgt2, config);

                var diffPairs = DiffEngine.BuildDiff(sourceTable, targetTable, keyColumns2);

                int onlySrc2 = 0;
                int onlyTgt2 = 0;
                int different2 = 0;

                foreach (var pair in diffPairs)
                {
                    switch (pair.DiffType)
                    {
                        case RowDiffType.OnlyInSource:
                            onlySrc2++;
                            break;
                        case RowDiffType.OnlyInTarget:
                            onlyTgt2++;
                            break;
                        case RowDiffType.Different:
                            different2++;
                            break;
                    }
                }

                var summary = new TableDiffSummary
                {
                    TableName = tableName,
                    OnlyInSourceCount = onlySrc2,
                    OnlyInTargetCount = onlyTgt2,
                    DifferentCount = different2,
                    TotalDiffCount = onlySrc2 + onlyTgt2 + different2
                };

                result.Add(summary);

                var done = Interlocked.Increment(ref processed);
                progress?.Report(new TablesDiffProgress
                {
                    Processed = done,
                    Total = total,
                    CurrentTable = tableName
                });
                });
            }
            finally
            {
                batch?.EndBatch(tgtConnStr);
                batch?.EndBatch(srcConnStr);
            }

            return result
                .OrderBy(s => s.TableName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        private static int ResolveMaxParallelism(AppConfig config)
        {
            int procCount = Environment.ProcessorCount;
            int value = 0;

            if (config != null && config.MaxParallelTables > 0)
            {
                value = config.MaxParallelTables;
            }
            else
            {
                // Значение по умолчанию – не больше 4 потоков.
                value = Math.Min(procCount, 4);
            }

            if (value <= 0)
                value = 1;

            // На всякий случай режем сверху, чтобы не было десятков потоков.
            int hardMax = Math.Max(1, procCount * 2);
            if (value > hardMax)
                value = hardMax;

            return value;
        }

        /// <summary>
        /// Выбор ключевых столбцов: PK &gt; пользовательский ключ &gt; все столбцы.
        /// </summary>
        private static string[] ResolveKeyColumns(
            string tableName,
            DataColumnCollection columns,
            string[] pkColumns,
            AppConfig config)
        {
            // 1) Если есть реальный PK — используем его.
            //
            // Особый случай для Excel:
            // ExcelDatabaseProvider возвращает псевдо-PK "Row" (номер строки), чтобы diff работал даже без явных ID-колонок.
            // Но пользователь может захотеть переопределить ключ на комбинацию колонок — разрешаем это через CustomKeys.
            // Поэтому: если PK = ["Row"], сначала пробуем CustomKeys, иначе используем "Row".
            if (pkColumns != null && pkColumns.Length > 0)
            {
                if (pkColumns.Length == 1 &&
                    string.Equals(pkColumns[0], "Row", StringComparison.OrdinalIgnoreCase))
                {
                    // Пропускаем на шаг CustomKeys ниже
                }
                else
                {
                    return pkColumns;
                }
            }

            // 2) Пользовательский ключ из конфига
            if (config?.CustomKeys != null)
            {
                CustomKeyConfig custom = null;

                foreach (var k in config.CustomKeys)
                {
                    if (string.Equals(k.TableName, tableName, StringComparison.OrdinalIgnoreCase))
                    {
                        custom = k;
                        break;
                    }
                }

                if (custom != null && custom.Columns != null && custom.Columns.Count > 0)
                {
                    var list = new List<string>();
                    foreach (var name in custom.Columns)
                    {
                        if (columns.Contains(name))
                            list.Add(name);
                    }

                    if (list.Count > 0)
                        return list.ToArray();
                }
            }

            
            // 2.5) Если дошли сюда и у нас есть псевдо-PK (например Excel: "Row") — используем его как ключ по умолчанию.
            if (pkColumns != null && pkColumns.Length > 0)
                return pkColumns;

// 3) Fallback — все столбцы
            var all = new List<string>();
            foreach (DataColumn c in columns)
            {
                all.Add(c.ColumnName);
            }

            return all.ToArray();
        }

        /// <summary>
        /// Выбор ключа с учётом ДВУХ таблиц (источник/приёмник).
        /// Нужен для форматов, где PK может отличаться между файлами/версиями (например XML .config).
        /// </summary>
        private static string[] ResolveKeyColumnsForPair(
            string tableName,
            DataTable sourceTable,
            DataTable targetTable,
            string[] pkSrc,
            string[] pkTgt,
            AppConfig config)
        {
            if (sourceTable == null) throw new ArgumentNullException(nameof(sourceTable));
            if (targetTable == null) throw new ArgumentNullException(nameof(targetTable));

            // 1) Пробуем PK источника (если он реально применим к обеим таблицам)
            if (IsColumnsPresentInBoth(sourceTable, targetTable, pkSrc))
                return pkSrc;

            // 2) Пробуем пользовательский ключ, но только если колонки есть в обеих таблицах
            if (config?.CustomKeys != null)
            {
                CustomKeyConfig custom = null;
                foreach (var k in config.CustomKeys)
                {
                    if (string.Equals(k.TableName, tableName, StringComparison.OrdinalIgnoreCase))
                    {
                        custom = k;
                        break;
                    }
                }

                if (custom != null && custom.Columns != null && custom.Columns.Count > 0)
                {
                    var list = new List<string>();
                    foreach (var name in custom.Columns)
                    {
                        if (ContainsColumnIgnoreCase(sourceTable.Columns, name) && ContainsColumnIgnoreCase(targetTable.Columns, name))
                            list.Add(GetActualColumnName(sourceTable.Columns, name));
                    }

                    if (list.Count > 0)
                        return list.ToArray();
                }
            }

            // 3) Пробуем PK приёмника (иногда PK вычисляется по-другому в другой версии файлов)
            if (IsColumnsPresentInBoth(sourceTable, targetTable, pkTgt))
                return pkTgt;

            // 4) Спец. мостик для XML .config: a_name <-> name, a_id <-> id и т.п.
            // Если в одной таблице ключ представлен атрибутом (a_*), а в другой — дочерним leaf-элементом,
            // создаём синтетический общий столбец __k_<base> и используем его как ключ.
            var bridged = TryBuildAliasedKey(sourceTable, targetTable);
            if (bridged != null && bridged.Length > 0)
                return bridged;

            // 5) Универсальный авто-подбор: ищем общий уникальный столбец (с приоритетами)
            var auto = TryPickUniqueCommonKey(sourceTable, targetTable);
            if (auto != null && auto.Length > 0)
                return auto;

            // 6) Фолбэк: если есть псевдо-PK (Excel: Row и т.п.) — используем его, но только если есть в обеих таблицах
            var resolvedFromSrc = ResolveKeyColumns(tableName, sourceTable.Columns, pkSrc, config);
            if (IsColumnsPresentInBoth(sourceTable, targetTable, resolvedFromSrc))
                return resolvedFromSrc;

            // 7) Крайний случай: ключ по номеру строки (без падения приложения)
            EnsureRowKey(sourceTable);
            EnsureRowKey(targetTable);
            return new[] { "__row" };
        }

        private static void EnsureRowKey(DataTable dt)
        {
            if (dt == null) return;
            if (!ContainsColumnIgnoreCase(dt.Columns, "__row"))
            {
                var c = dt.Columns.Add("__row", typeof(int));
                if (c != null)
                    c.ExtendedProperties["HideInGrid"] = true;
            }

            var colName = GetActualColumnName(dt.Columns, "__row");
            for (int i = 0; i < dt.Rows.Count; i++)
                dt.Rows[i][colName] = i + 1;
        }

        private static bool IsColumnsPresentInBoth(DataTable a, DataTable b, string[] cols)
        {
            if (cols == null || cols.Length == 0)
                return false;

            foreach (var c in cols)
            {
                if (string.IsNullOrWhiteSpace(c))
                    return false;
                if (!ContainsColumnIgnoreCase(a.Columns, c) || !ContainsColumnIgnoreCase(b.Columns, c))
                    return false;
            }
            return true;
        }

        private static bool ContainsColumnIgnoreCase(DataColumnCollection cols, string name)
        {
            if (cols == null || string.IsNullOrWhiteSpace(name)) return false;
            foreach (DataColumn c in cols)
            {
                if (string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool AreColumnsPresent(ColumnInfo[] cols, string[] names)
        {
            if (cols == null || cols.Length == 0) return false;
            if (names == null || names.Length == 0) return false;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in cols)
            {
                if (c != null && !string.IsNullOrWhiteSpace(c.Name))
                    set.Add(c.Name);
            }

            foreach (var n in names)
            {
                if (string.IsNullOrWhiteSpace(n))
                    return false;
                if (!set.Contains(n))
                    return false;
            }

            return true;
        }

        private static string GetActualColumnName(DataColumnCollection cols, string name)
        {
            if (cols == null || string.IsNullOrWhiteSpace(name)) return name;
            foreach (DataColumn c in cols)
            {
                if (string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                    return c.ColumnName;
            }
            return name;
        }

        private static string[] TryBuildAliasedKey(DataTable source, DataTable target)
        {
            var bases = new[] { "pk", "id", "key", "name", "offset", "n", "index", "uid", "guid" };

            foreach (var baseName in bases)
            {
                var srcHas = HasEither(source.Columns, baseName);
                var tgtHas = HasEither(target.Columns, baseName);
                if (!srcHas || !tgtHas)
                    continue;

                var synth = "__k_" + baseName;
                if (!ContainsColumnIgnoreCase(source.Columns, synth))
                {
                    var c = source.Columns.Add(synth, typeof(string));
                    if (c != null)
                        c.ExtendedProperties["HideInGrid"] = true;
                }
                if (!ContainsColumnIgnoreCase(target.Columns, synth))
                {
                    var c = target.Columns.Add(synth, typeof(string));
                    if (c != null)
                        c.ExtendedProperties["HideInGrid"] = true;
                }

                var srcSynth = GetActualColumnName(source.Columns, synth);
                var tgtSynth = GetActualColumnName(target.Columns, synth);

                for (int i = 0; i < source.Rows.Count; i++)
                    source.Rows[i][srcSynth] = GetEitherValue(source.Rows[i], baseName);
                for (int i = 0; i < target.Rows.Count; i++)
                    target.Rows[i][tgtSynth] = GetEitherValue(target.Rows[i], baseName);

                // Используем как ключ только если уникально в обеих таблицах.
                if (IsUniqueBySingleColumn(source, srcSynth) && IsUniqueBySingleColumn(target, tgtSynth))
                    return new[] { synth };
            }

            return null;
        }

        private static bool HasEither(DataColumnCollection cols, string baseName)
        {
            return ContainsColumnIgnoreCase(cols, baseName) || ContainsColumnIgnoreCase(cols, "a_" + baseName);
        }

        private static string GetEitherValue(DataRow r, string baseName)
        {
            if (r == null) return "";

            var t = r.Table;
            // сначала non-prefixed
            if (t != null && ContainsColumnIgnoreCase(t.Columns, baseName))
            {
                var n = GetActualColumnName(t.Columns, baseName);
                var v = r[n];
                return v == null || v == DBNull.Value ? "" : v.ToString();
            }

            var pref = "a_" + baseName;
            if (t != null && ContainsColumnIgnoreCase(t.Columns, pref))
            {
                var n = GetActualColumnName(t.Columns, pref);
                var v = r[n];
                return v == null || v == DBNull.Value ? "" : v.ToString();
            }

            return "";
        }

        private static bool IsUniqueBySingleColumn(DataTable dt, string colName)
        {
            if (dt == null || string.IsNullOrWhiteSpace(colName) || !ContainsColumnIgnoreCase(dt.Columns, colName))
                return false;

            var actual = GetActualColumnName(dt.Columns, colName);
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DataRow r in dt.Rows)
            {
                var v = r[actual];
                var s = (v == null || v == DBNull.Value) ? "" : v.ToString();
                if (string.IsNullOrEmpty(s))
                    return false;
                if (!set.Add(s))
                    return false;
            }

            return true;
        }

        private static string[] TryPickUniqueCommonKey(DataTable source, DataTable target)
        {
            if (source == null || target == null) return null;

            // общие колонки (по имени, без учёта регистра)
            var srcMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn c in source.Columns)
                if (!srcMap.ContainsKey(c.ColumnName)) srcMap[c.ColumnName] = c.ColumnName;

            var tgtSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn c in target.Columns)
                tgtSet.Add(c.ColumnName);

            // приоритетные имена
            var priority = new[] { "pk", "id", "key", "name", "offset", "n", "index", "uid", "guid" };

            foreach (var p in priority)
            {
                if (srcMap.TryGetValue(p, out var srcCol) && tgtSet.Contains(p))
                {
                    if (IsUniqueBySingleColumn(source, srcCol) && IsUniqueBySingleColumn(target, p))
                        return new[] { srcCol };
                }
            }

            // если не нашли по приоритетам — пробуем любые общие столбцы, но только уникальные
            foreach (var srcCol in source.Columns.Cast<DataColumn>().Select(c => c.ColumnName))
            {
                if (!tgtSet.Contains(srcCol))
                    continue;

                if (IsUniqueBySingleColumn(source, srcCol) && IsUniqueBySingleColumn(target, srcCol))
                    return new[] { srcCol };
            }

            return null;
        }
    }
}
