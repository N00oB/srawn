using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using MdbDiffTool.Core;
using System.Data.SQLite;
using System.Threading;

namespace MdbDiffTool
{
    /// <summary>
    /// Провайдер для SQLite через System.Data.SQLite.
    /// Реализует тот же контракт IDatabaseProvider, что и AccessDatabaseProvider.
    /// </summary>
    internal sealed class SqliteDatabaseProvider : IDatabaseProvider, IFastRowHashProvider
    {
        public List<string> GetTableNames(string connectionString)
        {
            var result = new List<string>();

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                const string sql =
                    "SELECT name FROM sqlite_master " +
                    "WHERE type = 'table' AND name NOT LIKE 'sqlite_%' " +
                    "ORDER BY name";

                using (var cmd = new SQLiteCommand(sql, conn))
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
                using (var connection = new SQLiteConnection(connectionString))
                using (var command = new SQLiteCommand($"SELECT * FROM [{tableName}]", connection))
                using (var adapter = new SQLiteDataAdapter(command))
                {
                    connection.Open();
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
                        $"Загрузка таблицы '{tableName}' (SQLite): " +
                        $"{sw.Elapsed.TotalMilliseconds:F0} мс, строк: {table.Rows.Count}.");
                }
                catch
                {
                    // Логирование никогда не должно ломать основной код.
                }
            }
        }

        public string[] GetPrimaryKeyColumns(string connectionString, string tableName)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand("PRAGMA table_info([" + tableName + "]);", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    var pkCols = new List<string>();

                    while (reader.Read())
                    {
                        // PRAGMA table_info:
                        // cid | name | type | notnull | dflt_value | pk
                        object pkFlag = reader["pk"];
                        long pkVal = 0;

                        if (pkFlag is long l)
                            pkVal = l;
                        else if (pkFlag is int i)
                            pkVal = i;
                        else if (pkFlag != null && pkFlag != DBNull.Value)
                        {
                            long.TryParse(pkFlag.ToString(), out pkVal);
                        }

                        if (pkVal > 0)
                        {
                            var name = reader["name"]?.ToString();
                            if (!string.IsNullOrEmpty(name))
                                pkCols.Add(name);
                        }
                    }

                    if (pkCols.Count > 0)
                        return pkCols.ToArray();
                }
            }

            return Array.Empty<string>();
        }


        public ProviderCapabilities GetCapabilities(string connectionString)
        {
            // SQLite: чтение + запись (apply/replace/drop)
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

            using (var conn = new SQLiteConnection(targetConnectionString))
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
                            AppLogger.Info("Игнорируем ошибки rollback.");
                        }

                        AppLogger.Error("Ошибка при применении изменений в SQLite базе.", ex);
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

            using (var conn = new SQLiteConnection(targetConnectionString))
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        // чистим таблицу
                        using (var cmdDel = new SQLiteCommand("DELETE FROM [" + tableName + "]", conn, tran))
                        {
                            cmdDel.ExecuteNonQuery();
                        }

                        // перезаливаем данные
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

                        AppLogger.Error("Ошибка при полной перезаливке таблицы в SQLite базе.", ex);
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
                using (var conn = new SQLiteConnection(connectionString))
                using (var cmd = conn.CreateCommand())
                {
                    conn.Open();
                    cmd.CommandText = "DROP TABLE " + QuoteSqliteIdentifier(tableName);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Ошибка при удалении таблицы '{tableName}' в SQLite базе.", ex);
                throw;
            }
        }

        private static string QuoteSqliteIdentifier(string name)
        {
            // В SQLite идентификаторы можно экранировать двойными кавычками.
            // Внутри имени двойные кавычки удваиваются.
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Не задано имя таблицы.", nameof(name));

            // Поддержим qualified name: schema.table (например, для ATTACHed БД).
            var parts = name.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                throw new ArgumentException("Не задано имя таблицы.", nameof(name));

            return string.Join(".", parts.Select(p => "\""+p.Replace("\"", "\"\"")+"\""));
        }



        private static void 
            InsertRow(
            SQLiteConnection conn,
            SQLiteTransaction tran,
            string tableName,
            DataRow srcRow)
        {
            var cols = srcRow.Table.Columns.Cast<DataColumn>().ToList();

            var colNames = string.Join(", ", cols.Select(c => "[" + c.ColumnName + "]"));

            // Параметры @p0, @p1, ...
            var paramNames = new List<string>();
            for (int i = 0; i < cols.Count; i++)
            {
                paramNames.Add("@p" + i);
            }

            var paramPlaceholders = string.Join(", ", paramNames);

            var sql = "INSERT INTO [" + tableName + "] (" + colNames + ") VALUES (" + paramPlaceholders + ")";

            using (var cmd = new SQLiteCommand(sql, conn, tran))
            {
                for (int i = 0; i < cols.Count; i++)
                {
                    var col = cols[i];
                    var val = srcRow[col];

                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue(paramNames[i], val);
                }

                cmd.ExecuteNonQuery();
            }
        }
        private static void DeleteRow(
            SQLiteConnection conn,
            SQLiteTransaction tran,
            string tableName,
            string[] pkColumns,
            DataRow targetRow)
        {
            if (pkColumns == null || pkColumns.Length == 0 || targetRow == null)
                return;

            var whereParts = new List<string>();
            foreach (var c in pkColumns)
                whereParts.Add("\"" + c + "\" = @p_" + c);

            var where = string.Join(" AND ", whereParts);
            var sql = "DELETE FROM \"" + tableName + "\" WHERE " + where;

            using (var cmd = new SQLiteCommand(sql, conn, tran))
            {
                foreach (var pk in pkColumns)
                {
                    var val = targetRow[pk];
                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue("@p_" + pk, val);
                }

                cmd.ExecuteNonQuery();
            }
        }

        private static void UpdateRow(
            SQLiteConnection conn,
            SQLiteTransaction tran,
            string tableName,
            string[] pkColumns,
            DataRow srcRow)
        {
            if (pkColumns == null || pkColumns.Length == 0)
                return;

            var allCols = srcRow.Table.Columns.Cast<DataColumn>()
                               .Select(c => c.ColumnName)
                               .ToList();

            var nonPkCols = new List<string>();
            foreach (var c in allCols)
            {
                bool isPk = false;
                foreach (var pk in pkColumns)
                {
                    if (string.Equals(pk, c, StringComparison.OrdinalIgnoreCase))
                    {
                        isPk = true;
                        break;
                    }
                }

                if (!isPk)
                {
                    nonPkCols.Add(c);
                }
            }

            if (nonPkCols.Count == 0)
                return;

            var setParts = new List<string>();
            int paramIndex = 0;

            foreach (var c in nonPkCols)
            {
                setParts.Add("[" + c + "] = @p" + paramIndex);
                paramIndex++;
            }

            var whereParts = new List<string>();
            foreach (var c in pkColumns)
            {
                whereParts.Add("[" + c + "] = @p" + paramIndex);
                paramIndex++;
            }

            var setPart = string.Join(", ", setParts);
            var wherePart = string.Join(" AND ", whereParts);

            var sql = "UPDATE [" + tableName + "] SET " + setPart + " WHERE " + wherePart;

            using (var cmd = new SQLiteCommand(sql, conn, tran))
            {
                paramIndex = 0;

                // SET параметры
                foreach (var colName in nonPkCols)
                {
                    var val = srcRow[colName];
                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue("@p" + paramIndex, val);
                    paramIndex++;
                }

                // WHERE параметры (PK)
                foreach (var pk in pkColumns)
                {
                    var val = srcRow[pk];
                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue("@p" + paramIndex, val);
                    paramIndex++;
                }

                cmd.ExecuteNonQuery();
            }
        }
    

        // -------------------- FAST SUMMARY (Level C) --------------------

        public ColumnInfo[] GetTableColumns(string connectionString, string tableName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("connectionString пустой.", nameof(connectionString));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("tableName пустой.", nameof(tableName));

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                using (var cmd = new SQLiteCommand($"SELECT * FROM {Q(tableName)} WHERE 1=0", conn))
                using (var adapter = new SQLiteDataAdapter(cmd))
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

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                string sql = $"SELECT {string.Join(", ", selectColumns.ConvertAll(Q))} FROM {Q(tableName)}";

                using (var cmd = new SQLiteCommand(sql, conn))
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

        private static string Q(string identifier)
        {
            if (identifier == null) return "[]";
            return "[" + identifier.Replace("]", "]]") + "]";
        }
}
}