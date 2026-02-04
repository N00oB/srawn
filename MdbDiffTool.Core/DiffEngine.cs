using System;
using System.Collections.Generic;
using System.Data;

namespace MdbDiffTool.Core
{
    /// <summary>
    /// Движок сравнения таблиц.
    /// </summary>
    public static class DiffEngine
    {
        /// <summary>
        /// Построить список различий между двумя таблицами на основе ключевых столбцов.
        /// </summary>
        public static List<RowPair> BuildDiff(
            DataTable source,
            DataTable target,
            string[] keyColumns)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (keyColumns == null || keyColumns.Length == 0)
                throw new ArgumentException("Не заданы ключевые столбцы.", nameof(keyColumns));

            var result = new List<RowPair>();

            var sourceDict = new Dictionary<string, DataRow>();
            var targetDict = new Dictionary<string, DataRow>();

            // Словарь для источника
            foreach (DataRow r in source.Rows)
            {
                var key = BuildKey(r, keyColumns);
                if (!sourceDict.ContainsKey(key))
                    sourceDict[key] = r;
            }

            // Словарь для приёмника
            foreach (DataRow r in target.Rows)
            {
                var key = BuildKey(r, keyColumns);
                if (!targetDict.ContainsKey(key))
                    targetDict[key] = r;
            }

            // Объединённый набор ключей
            var allKeys = new HashSet<string>(sourceDict.Keys);
            allKeys.UnionWith(targetDict.Keys);

            foreach (var key in allKeys)
            {
                sourceDict.TryGetValue(key, out var srcRow);
                targetDict.TryGetValue(key, out var tgtRow);

                bool inSource = srcRow != null;
                bool inTarget = tgtRow != null;

                if (inSource && !inTarget)
                {
                    // есть только в источнике
                    result.Add(new RowPair
                    {
                        Key = key,
                        SourceRow = srcRow,
                        TargetRow = null,
                        DiffType = RowDiffType.OnlyInSource
                    });
                }
                else if (!inSource && inTarget)
                {
                    // есть только в приёмнике
                    result.Add(new RowPair
                    {
                        Key = key,
                        SourceRow = null,
                        TargetRow = tgtRow,
                        DiffType = RowDiffType.OnlyInTarget
                    });
                }
                else if (inSource && inTarget)
                {
                    // есть в обеих — проверяем, отличаются ли данные
                    if (!RowsEqual(srcRow, tgtRow, source.Columns))
                    {
                        result.Add(new RowPair
                        {
                            Key = key,
                            SourceRow = srcRow,
                            TargetRow = tgtRow,
                            DiffType = RowDiffType.Different
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Построить строковый ключ по набору столбцов.
        /// </summary>
        public static string BuildKey(DataRow row, string[] keyColumns)
        {
            // Важно: этот ключ используется только как "идентификатор строки" внутри алгоритма diff.
            // Нам нужен быстрый и предсказуемый способ склейки значений без лишних аллокаций.
            var sb = new System.Text.StringBuilder(keyColumns.Length * 16);

            for (int i = 0; i < keyColumns.Length; i++)
            {
                var col = keyColumns[i];
                var v = row[col];

                if (i > 0)
                    sb.Append('|');

                if (v == null || v == DBNull.Value)
                {
                    sb.Append("<NULL>");
                }
                else
                {
                    sb.Append(v.ToString());
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Сравнение двух строк по всем столбцам с учётом типов.
        /// Для строковых полей NULL и "" считаются одинаковыми.
        /// </summary>
        private static bool RowsEqual(DataRow a, DataRow b, DataColumnCollection cols)
        {
            foreach (DataColumn col in cols)
            {
                // Колонки у источника и приёмника могут отличаться (Excel/разные версии схемы).
                // Если колонки нет в одной из таблиц — считаем значение NULL.
                object v1 = a.Table.Columns.Contains(col.ColumnName) ? a[col.ColumnName] : DBNull.Value;
                object v2 = b.Table.Columns.Contains(col.ColumnName) ? b[col.ColumnName] : DBNull.Value;

                if (!AreValuesEqual(v1, v2, col.DataType))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Сравнение двух значений одного типа.
        /// Для string: NULL == "".
        /// Для остальных типов: обычное Equals + учёт NULL.
        /// </summary>
        private static bool AreValuesEqual(object v1, object v2, Type type)
        {
            if (type == typeof(string))
            {
                string s1 = (v1 == null || v1 == DBNull.Value) ? "" : (string)v1;
                string s2 = (v2 == null || v2 == DBNull.Value) ? "" : (string)v2;
                return string.Equals(s1, s2, StringComparison.Ordinal);
            }

            bool isNull1 = (v1 == null || v1 == DBNull.Value);
            bool isNull2 = (v2 == null || v2 == DBNull.Value);

            if (isNull1 && isNull2) return true;
            if (isNull1 || isNull2) return false;

            return Equals(v1, v2);
        }
    }
}
