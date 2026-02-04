using MdbDiffTool.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Threading;

namespace MdbDiffTool
{
    /// <summary>
    /// Провайдер для Access/MDB через OleDb.
    /// </summary>
    internal sealed class AccessDatabaseProvider : IDatabaseProvider, IBatchDatabaseProvider, IFastRowHashProvider
    {
        // Для ускорения пакетных операций (btnLoadTables, сравнение, применение)
        // открываем OleDbConnection один раз и переиспользуем в рамках STA-воркера.
        // ВАЖНО: OleDbConnection не потокобезопасен и привязан к STA-потоку.
        [ThreadStatic]
        private static Dictionary<string, OleDbConnection> _threadConnections;

        public void BeginBatch(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return;

            StaExecutor.Run(connectionString, () =>
            {
                var _ = GetOrCreateCachedConnection(connectionString);
            });
        }

        public void EndBatch(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return;

            StaExecutor.Run(connectionString, () =>
            {
                try
                {
                    if (_threadConnections == null)
                        return;

                    if (_threadConnections.TryGetValue(connectionString, out var conn) && conn != null)
                    {
                        try { conn.Close(); } catch { }
                        try { conn.Dispose(); } catch { }
                    }

                    _threadConnections.Remove(connectionString);
                }
                catch { }
            });
        }

        private static OleDbConnection GetOrCreateCachedConnection(string connectionString)
        {
            if (_threadConnections == null)
                _threadConnections = new Dictionary<string, OleDbConnection>(StringComparer.Ordinal);

            if (!_threadConnections.TryGetValue(connectionString, out var conn) || conn == null)
            {
                conn = new OleDbConnection(connectionString);
                _threadConnections[connectionString] = conn;
            }

            if (conn.State != ConnectionState.Open)
                conn.Open();

            return conn;
        }

        private static OleDbConnection AcquireConnection(string connectionString, out bool dispose)
        {
            // Если BeginBatch уже был вызван в этом STA-потоке, переиспользуем коннект.
            if (_threadConnections != null &&
                _threadConnections.TryGetValue(connectionString, out var cached) &&
                cached != null)
            {
                dispose = false;
                if (cached.State != ConnectionState.Open)
                    cached.Open();
                return cached;
            }

            // Иначе работаем в старом режиме: открыть/закрыть для одного вызова.
            dispose = true;
            var conn = new OleDbConnection(connectionString);
            conn.Open();
            return conn;
        }
        public List<string> GetTableNames(string connectionString)
        {
            // Важно: Access/ACE внутри использует COM и на некоторых ПК падает при вызовах из ThreadPool (MTA)
            // или при параллельных обращениях. Прогоняем всё через STA-исполнитель.
            return StaExecutor.Run(connectionString, () => GetTableNamesCore(connectionString));
        }

        private static List<string> GetTableNamesCore(string connectionString)
        {
            var result = new List<string>();

            try
            {
                bool disposeConn;
                var conn = AcquireConnection(connectionString, out disposeConn);
                try
                {
                    var schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
                    foreach (DataRow row in schema.Rows)
                    {
                        var type = row["TABLE_TYPE"] == null ? null : row["TABLE_TYPE"].ToString();
                        var name = row["TABLE_NAME"] == null ? null : row["TABLE_NAME"].ToString();

                        if (!string.Equals(type, "TABLE", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (string.IsNullOrEmpty(name))
                            continue;
                        if (name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
                            continue;

                        result.Add(name);
                    }
                }
                finally
                {
                    if (disposeConn)
                    {
                        try { conn.Close(); } catch { }
                        try { conn.Dispose(); } catch { }
                    }
                }
            }
            catch (OleDbException ex) when (IsAceNotRegistered(ex))
            {
                ThrowAceNotRegistered(ex);
            }

            return result;
        }

        public DataTable LoadTable(string connectionString, string tableName)
        {
            return StaExecutor.Run(connectionString, () => LoadTableCore(connectionString, tableName));
        }

        private static DataTable LoadTableCore(string connectionString, string tableName)
        {
            var sw = Stopwatch.StartNew();
            DataTable table = new DataTable();

            try
            {
                bool disposeConn;
                var connection = AcquireConnection(connectionString, out disposeConn);
                try
                {
                    using (var command = new OleDbCommand($"SELECT * FROM [{tableName}]", connection))
                    using (var adapter = new OleDbDataAdapter(command))
                    {
                    adapter.Fill(table);
                    }
                }
                finally
                {
                    if (disposeConn)
                    {
                        try { connection.Close(); } catch { }
                        try { connection.Dispose(); } catch { }
                    }
                }

                return table;
            }
            catch (OleDbException ex) when (IsAceNotRegistered(ex))
            {
                ThrowAceNotRegistered(ex);
                throw; // нужно компилятору
            }
            finally
            {
                sw.Stop();
                try
                {
                    AppLogger.Info(
                        $"Загрузка таблицы '{tableName}': " +
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
            return StaExecutor.Run(connectionString, () => GetPrimaryKeyColumnsCore(connectionString, tableName));
        }

private static string[] GetPrimaryKeyColumnsCore(string connectionString, string tableName)
{
    try
    {
        bool disposeConn;
        var conn = AcquireConnection(connectionString, out disposeConn);
        try
        {

            // 1) Быстрый путь: берём PK через schema table.
            // Важно: для OleDbSchemaGuid.Primary_Keys разные провайдеры могут ожидать разную длину restrictions.
            // На части ПК передача "лишних" ограничений приводит к OleDbException: 0x80070057 (E_INVALIDARG).
            DataTable schema = null;

            try
            {
                // Чаще всего ожидается 3 ограничения: Catalog, Schema, Table.
                var restrictions3 = new object[] { null, null, tableName };
                schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys, restrictions3);
            }
            catch (OleDbException ex) when (IsInvalidArg(ex))
            {
                // Некоторые реализации терпят только 4 ограничения (или наоборот).
                try
                {
                    var restrictions4 = new object[] { null, null, tableName, null };
                    schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys, restrictions4);
                }
                catch (OleDbException ex2) when (IsInvalidArg(ex2))
                {
                    // Последняя попытка: без ограничений, фильтруем вручную.
                    schema = conn.GetOleDbSchemaTable(OleDbSchemaGuid.Primary_Keys, null);
                }
            }

            var pkList = ExtractPkColumnsFromSchema(schema, tableName);
            if (pkList != null && pkList.Count > 0)
                return pkList.ToArray();

            // 2) Фолбэк: FillSchema на пустом SELECT (медленнее, но обычно совместимее).
            // Этот путь используем только если schema-метод не дал результатов.
            try
            {
                using (var command = new OleDbCommand($"SELECT * FROM [{tableName}] WHERE 1=0", conn))
                using (var adapter = new OleDbDataAdapter(command))
                {
                    var dt = new DataTable();
                    adapter.FillSchema(dt, SchemaType.Source);

                    if (dt.PrimaryKey != null && dt.PrimaryKey.Length > 0)
                    {
                        return dt.PrimaryKey
                            .Select(c => c.ColumnName)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                // Не критично: просто считаем, что PK нет, и сравнение пойдёт по пользовательскому ключу или всем столбцам.
                AppLogger.Error(
                    $"Не удалось определить первичный ключ для таблицы '{tableName}'. " +
                    $"Будет использован пользовательский ключ или все столбцы.",
                    ex);
            }

            return Array.Empty<string>();
        }
        finally
        {
            if (disposeConn)
            {
                try { conn.Close(); } catch { }
                try { conn.Dispose(); } catch { }
            }
        }
    }
    catch (OleDbException ex) when (IsAceNotRegistered(ex))
    {
        ThrowAceNotRegistered(ex);
        throw;
    }
}

private static bool IsInvalidArg(OleDbException ex)
{
    // E_INVALIDARG = 0x80070057
    const int E_INVALIDARG = unchecked((int)0x80070057);
    return ex != null && ex.ErrorCode == E_INVALIDARG;
}

private static List<string> ExtractPkColumnsFromSchema(DataTable schema, string tableName)
{
    if (schema == null || schema.Rows == null || schema.Rows.Count == 0)
        return null;

    IEnumerable<DataRow> rows = schema.Rows.Cast<DataRow>();

    // Если schema получен без restrictions — отфильтруем по имени таблицы.
    if (!string.IsNullOrWhiteSpace(tableName) && schema.Columns.Contains("TABLE_NAME"))
    {
        rows = rows.Where(r =>
            string.Equals(r["TABLE_NAME"]?.ToString(), tableName, StringComparison.OrdinalIgnoreCase));
    }

    // Порядок ключевых колонок
    if (schema.Columns.Contains("ORDINAL"))
        rows = rows.OrderBy(r => Convert.ToInt32(r["ORDINAL"]));
    else if (schema.Columns.Contains("ORDINAL_POSITION"))
        rows = rows.OrderBy(r => Convert.ToInt32(r["ORDINAL_POSITION"]));
    else if (schema.Columns.Contains("KEY_SEQ"))
        rows = rows.OrderBy(r => Convert.ToInt32(r["KEY_SEQ"]));

    var list = new List<string>();
    foreach (var row in rows)
    {
        var colName = row["COLUMN_NAME"]?.ToString();
        if (string.IsNullOrWhiteSpace(colName))
            continue;
        if (!list.Contains(colName))
            list.Add(colName);
    }

    return list;
}

        private static bool IsAceNotRegistered(OleDbException ex)
        {
            if (ex == null || string.IsNullOrWhiteSpace(ex.Message))
                return false;

            // Сообщение бывает разным, но почти всегда содержит "ACE.OLEDB" и "не зарегистрирован".
            return ex.Message.IndexOf("ACE.OLEDB", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   ex.Message.IndexOf("не зарегистрирован", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ThrowAceNotRegistered(OleDbException ex)
        {
            AppLogger.Error("Провайдер Microsoft Access Database Engine (ACE OLEDB) недоступен.", ex);

            throw new InvalidOperationException(
                "Невозможно открыть базу Access (.accdb/.mdb).\r\n" +
                "На этом компьютере не зарегистрирован OLEDB-провайдер Microsoft Access Database Engine (ACE).\r\n\r\n" +
                "Решение:\r\n" +
                "1) Установите Microsoft Access Database Engine нужной разрядности.\r\n" +
                "2) Если установлен Office x64 — используйте сборку программы x64.\r\n" +
                "   Если установлен Office x86 — используйте сборку программы x86.\r\n\r\n" +
                "Подсказка: если у вас источник .accdb, без ACE-драйвера его открыть невозможно.",
                ex);
        }


        public ProviderCapabilities GetCapabilities(string connectionString)
        {
            // Access: чтение + запись (apply/replace/drop)
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
            StaExecutor.Run(targetConnectionString, () => ApplyRowChangesCore(targetConnectionString, tableName, primaryKeyColumns, pairsToApply));
        }

        private static void ApplyRowChangesCore(
            string targetConnectionString,
            string tableName,
            string[] primaryKeyColumns,
            IEnumerable<RowPair> pairsToApply)
        {
            if (pairsToApply == null)
                return;

            using (var conn = new OleDbConnection(targetConnectionString))
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
                    catch
                    {
                        tran.Rollback();
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
            StaExecutor.Run(targetConnectionString, () => ReplaceTableCore(targetConnectionString, tableName, sourceTable));
        }

        private static void ReplaceTableCore(
            string targetConnectionString,
            string tableName,
            DataTable sourceTable)
        {
            if (sourceTable == null)
                throw new ArgumentNullException(nameof(sourceTable));

            using (var conn = new OleDbConnection(targetConnectionString))
            {
                conn.Open();
                using (var tran = conn.BeginTransaction())
                {
                    try
                    {
                        // чистим таблицу
                        using (var cmdDel = new OleDbCommand("DELETE FROM [" + tableName + "]", conn, tran))
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
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }
        }
        public void DropTable(string connectionString, string tableName)
        {
            StaExecutor.Run(connectionString, () => DropTableCore(connectionString, tableName));
        }

        private static void DropTableCore(string connectionString, string tableName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Пустая строка подключения.", nameof(connectionString));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("Не задано имя таблицы.", nameof(tableName));

            try
            {
                using (var conn = new OleDbConnection(connectionString))
                using (var cmd = conn.CreateCommand())
                {
                    conn.Open();
                    cmd.CommandText = "DROP TABLE " + QuoteAccessIdentifier(tableName);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Ошибка при удалении таблицы '{tableName}' в Access базе.", ex);
                throw;
            }
        }

        private static string QuoteAccessIdentifier(string name)
        {
            // В Access идентификаторы экранируются квадратными скобками.
            // Закрывающую скобку внутри имени удваиваем.
            name ??= string.Empty;
            return "[" + name.Replace("]", "]]") + "]";
        }


        private static void DeleteRow(
            OleDbConnection conn,
            OleDbTransaction tran,
            string tableName,
            string[] pkColumns,
            DataRow targetRow)
        {
            if (pkColumns == null || pkColumns.Length == 0 || targetRow == null)
                return;

            var whereParts = new List<string>();
            foreach (var c in pkColumns)
                whereParts.Add("[" + c + "] = ?");

            var where = string.Join(" AND ", whereParts);
            var sql = "DELETE FROM [" + tableName + "] WHERE " + where;

            using (var cmd = new OleDbCommand(sql, conn, tran))
            {
                foreach (var pk in pkColumns)
                {
                    var val = targetRow[pk];
                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue("?", val);
                }

                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertRow(
            OleDbConnection conn,
            OleDbTransaction tran,
            string tableName,
            DataRow srcRow)
        {
            var cols = srcRow.Table.Columns.Cast<DataColumn>().ToList();

            var colNames = string.Join(", ", cols.Select(c => "[" + c.ColumnName + "]"));
            var paramPlaceholders = string.Join(", ", cols.Select(c => "?"));

            var sql = "INSERT INTO [" + tableName + "] (" + colNames + ") VALUES (" + paramPlaceholders + ")";

            using (var cmd = new OleDbCommand(sql, conn, tran))
            {
                foreach (var col in cols)
                {
                    var val = srcRow[col];
                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue("?", val);
                }

                cmd.ExecuteNonQuery();
            }
        }

        private static void UpdateRow(
            OleDbConnection conn,
            OleDbTransaction tran,
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
            foreach (var c in nonPkCols)
            {
                setParts.Add("[" + c + "] = ?");
            }

            var whereParts = new List<string>();
            foreach (var c in pkColumns)
            {
                whereParts.Add("[" + c + "] = ?");
            }

            var setPart = string.Join(", ", setParts);
            var wherePart = string.Join(" AND ", whereParts);

            var sql = "UPDATE [" + tableName + "] SET " + setPart + " WHERE " + wherePart;

            using (var cmd = new OleDbCommand(sql, conn, tran))
            {
                // SET
                foreach (var colName in nonPkCols)
                {
                    var val = srcRow[colName];
                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue("?", val);
                }

                // WHERE
                foreach (var pk in pkColumns)
                {
                    var val = srcRow[pk];
                    if (val == null || val == DBNull.Value)
                        val = DBNull.Value;

                    cmd.Parameters.AddWithValue("?", val);
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

            return StaExecutor.Run(connectionString, () => GetTableColumnsCore(connectionString, tableName));
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

            return StaExecutor.Run(connectionString, () => LoadKeyHashMapCore(connectionString, tableName, keyColumns, tableColumns, cancellationToken));
        }

        private ColumnInfo[] GetTableColumnsCore(string connectionString, string tableName)
{
    bool disposeConn;
    var conn = AcquireConnection(connectionString, out disposeConn);
    try
    {
        using (var cmd = conn.CreateCommand())
        using (var adapter = new OleDbDataAdapter((OleDbCommand)cmd))
        {
            cmd.CommandText = $"SELECT * FROM {Q(tableName)} WHERE 1=0";
            var dt = new DataTable();
            adapter.FillSchema(dt, SchemaType.Source);

            var cols = new List<ColumnInfo>(dt.Columns.Count);
            foreach (DataColumn c in dt.Columns)
                cols.Add(new ColumnInfo(c.ColumnName, c.DataType));

            return cols.ToArray();
        }
    }
    finally
    {
        if (disposeConn)
        {
            try { conn.Close(); } catch { }
            try { conn.Dispose(); } catch { }
        }
    }
}

        

private Dictionary<string, ulong> LoadKeyHashMapCore(
    string connectionString,
    string tableName,
    string[] keyColumns,
    ColumnInfo[] tableColumns,
    CancellationToken cancellationToken)
{
    // Выбираем только нужные столбцы (все из tableColumns). Это быстрее, чем SELECT * + DataAdapter.
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

    bool disposeConn;
    var conn = AcquireConnection(connectionString, out disposeConn);
    try
    {
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT {string.Join(", ", selectColumns.ConvertAll(Q))} FROM {Q(tableName)}";

            using (var reader = ((OleDbCommand)cmd).ExecuteReader())
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

                    // как в DiffEngine: дубликаты ключей перезаписываются (остаётся последняя строка)
                    map[key] = h;
                }

                return map;
            }
        }
    }
    finally
    {
        if (disposeConn)
        {
            try { conn.Close(); } catch { }
            try { conn.Dispose(); } catch { }
        }
    }
}

private static string Q(string identifier)
        {
            // Экранирование для OleDb/Access в квадратных скобках
            if (identifier == null) return "[]";
            return "[" + identifier.Replace("]", "]]") + "]";
        }
}
}