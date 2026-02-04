using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using MdbDiffTool.Core;
using Npgsql;
using System.Threading;

namespace MdbDiffTool
{
    /// <summary>
    /// Провайдер для PostgreSQL через Npgsql.
    /// Реализует тот же контракт IDatabaseProvider, что и Access/SQLite.
    /// </summary>
    internal sealed class PostgresDatabaseProvider : IDatabaseProvider, IFastRowHashProvider
    {
        public List<string> GetTableNames(string connectionString)
        {
            var result = new List<string>();

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                // Берём только таблицы из текущей схемы (current_schema()).
                const string sql = @"
                    select table_name
                    from information_schema.tables
                    where table_type = 'BASE TABLE'
                      and table_schema = current_schema()
                    order by table_name;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var name = reader.GetString(0);
                        if (!string.IsNullOrEmpty(name))
                            result.Add(name);
                    }
                }
            }

            return result;
        }

        public DataTable LoadTable(string connectionString, string tableName)
        {
            var sw = Stopwatch.StartNew();
            var table = new DataTable();

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                using (var cmd = new NpgsqlCommand(
                           "SELECT * FROM " + QuoteIdentifier(tableName), conn))
                using (var adapter = new NpgsqlDataAdapter(cmd))
                {
                    conn.Open();
                    adapter.Fill(table);
                }

                return table;
            }
            finally
            {
                sw.Stop();
                try
                {
                    AppLogger.Info(
                        $"Загрузка таблицы '{tableName}' (PostgreSQL): " +
                        $"{sw.Elapsed.TotalMilliseconds:F0} мс, строк: {table.Rows.Count}.");
                }
                catch
                {
                    // Ошибка логирования не должна ломать приложение
                }
            }
        }

        public string[] GetPrimaryKeyColumns(string connectionString, string tableName)
        {
            var result = new List<string>();

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                const string sql = @"
                    select kcu.column_name
                    from information_schema.table_constraints tc
                    join information_schema.key_column_usage kcu
                      on  kcu.constraint_name = tc.constraint_name
                      and kcu.table_schema   = tc.table_schema
                      and kcu.table_name     = tc.table_name
                    where tc.constraint_type = 'PRIMARY KEY'
                      and kcu.table_name     = @tableName
                      and kcu.table_schema   = current_schema()
                    order by kcu.ordinal_position;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("tableName", tableName);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var name = reader.GetString(0);
                            if (!string.IsNullOrEmpty(name))
                                result.Add(name);
                        }
                    }
                }
            }

            return result.ToArray();
        }


        public ProviderCapabilities GetCapabilities(string connectionString)
        {
            // PostgreSQL: чтение + запись (apply/replace/drop)
            return ProviderCapabilities.Read |
                   ProviderCapabilities.ApplyRowChanges |
                   ProviderCapabilities.ReplaceTable |
                   ProviderCapabilities.DropTable;
        }

        public void ApplyRowChanges(
            string targetConnectionString,
            string tableName,
            string[] primaryKeyColumns,
            IEnumerable<RowPair> pairsToApply)
        {
            if (pairsToApply == null)
                return;

            using (var conn = new NpgsqlConnection(targetConnectionString))
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var pair in pairsToApply)
                        {
                            if (pair == null)
                                continue;

                            if (pair.DiffType == RowDiffType.OnlyInSource)
                            {
                                InsertRow(conn, tran, tableName, pair.SourceRow);
                            }
                            else if (pair.DiffType == RowDiffType.Different)
                            {
                                UpdateRow(conn, tran, tableName, primaryKeyColumns, pair.SourceRow);
                            }
                            else if (pair.DiffType == RowDiffType.OnlyInTarget)
                            {
                                DeleteRow(conn, tran, tableName, primaryKeyColumns, pair.TargetRow);
                            }
                        }

                        tran.Commit();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            tran.Rollback();
                        }
                        catch
                        {
                        }

                        AppLogger.Error("Ошибка при применении изменений в PostgreSQL базе.", ex);
                        throw;
                    }
                }
            }
        }

        public void ReplaceTable(
            string targetConnectionString,
            string tableName,
            DataTable sourceTable)
        {
            if (sourceTable == null)
                throw new ArgumentNullException(nameof(sourceTable));

            using (var conn = new NpgsqlConnection(targetConnectionString))
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        // Полностью очищаем таблицу
                        using (var cmdDel = new NpgsqlCommand(
                                   "DELETE FROM " + QuoteIdentifier(tableName), conn, tran))
                        {
                            cmdDel.ExecuteNonQuery();
                        }

                        // Перезаливаем данные
                        foreach (DataRow r in sourceTable.Rows)
                        {
                            InsertRow(conn, tran, tableName, r);
                        }

                        tran.Commit();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            tran.Rollback();
                        }
                        catch
                        {
                        }

                        AppLogger.Error("Ошибка при полной перезаливке таблицы в PostgreSQL базе.", ex);
                        throw;
                    }
                }
            }
        }
        public void DropTable(string connectionString, string tableName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Пустая строка подключения.", nameof(connectionString));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Не задано имя таблицы.", nameof(tableName));

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                using (var cmd = conn.CreateCommand())
                {
                    conn.Open();
                    cmd.CommandText = "DROP TABLE " + QuotePostgresIdentifier(tableName);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Ошибка при удалении таблицы '{tableName}' в PostgreSQL базе.", ex);
                throw;
            }
        }

        private static string QuotePostgresIdentifier(string name)
        {
            // В PostgreSQL идентификаторы экранируются двойными кавычками.
            // Внутри имени двойные кавычки удваиваются.
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Не задано имя таблицы.", nameof(name));

            // Поддержим qualified name: schema.table
            var parts = name.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                throw new ArgumentException("Не задано имя таблицы.", nameof(name));

            return string.Join(".", parts.Select(p => "\""+p.Replace("\"", "\"\"")+"\""));
        }


        private static void DeleteRow(
            NpgsqlConnection conn,
            NpgsqlTransaction tran,
            string tableName,
            string[] pkColumns,
            DataRow targetRow)
        {
            if (pkColumns == null || pkColumns.Length == 0 || targetRow == null)
                return;

            // Используем безопасное квотирование имён
            string quotedTable = "\"" + tableName.Replace("\"", "\"\"") + "\"";

            var whereParts = new List<string>();
            for (int i = 0; i < pkColumns.Length; i++)
            {
                string col = pkColumns[i];
                string paramName = "@p" + i;
                string quotedCol = "\"" + col.Replace("\"", "\"\"") + "\"";
                whereParts.Add(quotedCol + " = " + paramName);
            }

            var where = string.Join(" AND ", whereParts);
            var sql = "DELETE FROM " + quotedTable + " WHERE " + where;

            using (var cmd = new NpgsqlCommand(sql, conn, tran))
            {
                for (int i = 0; i < pkColumns.Length; i++)
                {
                    string col = pkColumns[i];
                    string paramName = "@p" + i;

                    var val = targetRow[col];
                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue(paramName, val);
                }

                cmd.ExecuteNonQuery();
            }
        }

        // ===== Вспомогательные методы =====

        private static string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return "\"\"";

            return "\"" + identifier.Replace("\"", "\"\"") + "\"";
        }

        private static void InsertRow(
            NpgsqlConnection conn,
            NpgsqlTransaction tran,
            string tableName,
            DataRow srcRow)
        {
            var cols = srcRow.Table.Columns.Cast<DataColumn>().ToList();

            var colNames = string.Join(", ",
                cols.Select(c => QuoteIdentifier(c.ColumnName)));

            var paramNames = new List<string>();
            for (int i = 0; i < cols.Count; i++)
            {
                paramNames.Add("@p" + i);
            }

            var paramPart = string.Join(", ", paramNames);

            var sql = "INSERT INTO " + QuoteIdentifier(tableName) +
                      " (" + colNames + ") VALUES (" + paramPart + ")";

            using (var cmd = new NpgsqlCommand(sql, conn, tran))
            {
                for (int i = 0; i < cols.Count; i++)
                {
                    var col = cols[i];
                    var val = srcRow[col];

                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue(paramNames[i], val ?? DBNull.Value);
                }

                cmd.ExecuteNonQuery();
            }
        }

        private static void UpdateRow(
            NpgsqlConnection conn,
            NpgsqlTransaction tran,
            string tableName,
            string[] primaryKeyColumns,
            DataRow srcRow)
        {
            if (primaryKeyColumns == null || primaryKeyColumns.Length == 0)
                return;

            var allCols = srcRow.Table.Columns.Cast<DataColumn>()
                .Select(c => c.ColumnName)
                .ToList();

            var nonPkCols = new List<string>();
            foreach (var c in allCols)
            {
                bool isPk = false;
                foreach (var pk in primaryKeyColumns)
                {
                    if (string.Equals(pk, c, StringComparison.OrdinalIgnoreCase))
                    {
                        isPk = true;
                        break;
                    }
                }

                if (!isPk)
                    nonPkCols.Add(c);
            }

            if (nonPkCols.Count == 0)
                return;

            var setParts = new List<string>();
            int paramIndex = 0;

            foreach (var c in nonPkCols)
            {
                setParts.Add(QuoteIdentifier(c) + " = @p" + paramIndex);
                paramIndex++;
            }

            var whereParts = new List<string>();
            foreach (var c in primaryKeyColumns)
            {
                whereParts.Add(QuoteIdentifier(c) + " = @p" + paramIndex);
                paramIndex++;
            }

            var setPart = string.Join(", ", setParts);
            var wherePart = string.Join(" AND ", whereParts);

            var sql = "UPDATE " + QuoteIdentifier(tableName) +
                      " SET " + setPart +
                      " WHERE " + wherePart;

            using (var cmd = new NpgsqlCommand(sql, conn, tran))
            {
                paramIndex = 0;

                // SET
                foreach (var colName in nonPkCols)
                {
                    var val = srcRow[colName];
                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue("@p" + paramIndex, val ?? DBNull.Value);
                    paramIndex++;
                }

                // WHERE (PK)
                foreach (var pk in primaryKeyColumns)
                {
                    var val = srcRow[pk];
                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue("@p" + paramIndex, val ?? DBNull.Value);
                    paramIndex++;
                }

                cmd.ExecuteNonQuery();
            }
        }

        public ColumnInfo[] GetTableColumns(string connectionString, string tableName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("connectionString пустой.", nameof(connectionString));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("tableName пустой.", nameof(tableName));

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                using (var cmd = new NpgsqlCommand($"SELECT * FROM {QuoteIdentifier(tableName)} LIMIT 0", conn))
                using (var adapter = new NpgsqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.FillSchema(dt, SchemaType.Source);

                    var cols = new List<ColumnInfo>(dt.Columns.Count);
                    foreach (DataColumn c in dt.Columns)
                        cols.Add(new ColumnInfo(c.ColumnName, c.DataType));

                    return cols.ToArray();
                }
            }
        }

        public Dictionary<string, ulong> LoadKeyHashMap(
            string connectionString,
            string tableName,
            string[] keyColumns,
            ColumnInfo[] tableColumns,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("connectionString пустой.", nameof(connectionString));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("tableName пустой.", nameof(tableName));
            if (keyColumns == null || keyColumns.Length == 0)
                throw new ArgumentException("keyColumns пустой.", nameof(keyColumns));
            if (tableColumns == null || tableColumns.Length == 0)
                throw new ArgumentException("tableColumns пустой.", nameof(tableColumns));

            var selectColumns = new List<string>(tableColumns.Length);
            var typeList = new List<Type>(tableColumns.Length);

            foreach (var c in tableColumns)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.Name)) continue;
                selectColumns.Add(c.Name);
                typeList.Add(c.DataType ?? typeof(object));
            }

            // гарантируем, что столбцы ключа точно присутствуют в SELECT (на всякий случай)
            for (int i = 0; i < keyColumns.Length; i++)
            {
                var k = keyColumns[i];
                if (string.IsNullOrWhiteSpace(k)) continue;

                bool exists = selectColumns.Exists(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    selectColumns.Add(k);
                    typeList.Add(typeof(object));
                }
            }

            using (var conn = new NpgsqlConnection(connectionString))
            {
                conn.Open();

                string sql = $"SELECT {string.Join(", ", selectColumns.ConvertAll(QuoteIdentifier))} FROM {QuoteIdentifier(tableName)}";

                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    var keyOrdinals = new int[keyColumns.Length];
                    for (int i = 0; i < keyColumns.Length; i++)
                        keyOrdinals[i] = reader.GetOrdinal(keyColumns[i]);

                    var hashOrdinals = new int[selectColumns.Count];
                    for (int i = 0; i < selectColumns.Count; i++)
                        hashOrdinals[i] = reader.GetOrdinal(selectColumns[i]);

                    var expectedTypes = typeList.ToArray();

                    var map = new Dictionary<string, ulong>(StringComparer.Ordinal);

                    while (reader.Read())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string key = RowHashing.BuildKey(reader, keyOrdinals);
                        ulong h = RowHashing.ComputeRowHash(reader, hashOrdinals, expectedTypes);

                        map[key] = h;
                    }

                    return map;
                }
            }
        }
}
}