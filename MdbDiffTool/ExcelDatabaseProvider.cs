using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;
using ExcelDataReader;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    /// <summary>
    /// "Провайдер" Excel-файлов (.xlsx/.xlsm/.xlam/.xls/.xla) с моделью "книга = база, лист = таблица".
    /// 
    /// Важно:
    /// - Для Excel нет первичных ключей, поэтому по умолчанию ключи берутся из всех колонок, либо пользователь задаёт свои.
    /// - Колонки как в excel A,B,C...
    /// - Значения приводятся к строке (CurrentCulture) для предсказуемого сравнения Excel↔Excel.
    /// </summary>
    internal sealed class ExcelDatabaseProvider : IDatabaseProvider, IBatchDatabaseProvider, IFastRowHashProvider
    {
        private const string ExcelRowNumberColumnName = "Row";

        // Чтобы не держать открытый файл (и не ловить lock Excel), в режиме batch читаем файл в память один раз.
        private readonly ConcurrentDictionary<string, byte[]> _bytesCache =
            new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        public bool SupportsBatchMode => true;

        public void BeginBatch(string connectionString)
        {
            var path = ExtractFilePath(connectionString);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            // Если уже загружено — не трогаем.
            _bytesCache.GetOrAdd(connectionString, _ => File.ReadAllBytes(path));
        }

        public void EndBatch(string connectionString)
        {
            byte[] _;
            _bytesCache.TryRemove(connectionString, out _);
        }

        public List<string> GetTableNames(string connectionString)
        {
            using (var stream = OpenStream(connectionString))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var list = new List<string>();

                do
                {
                    var name = reader.Name;
                    if (string.IsNullOrWhiteSpace(name))
                        name = "Sheet" + (list.Count + 1);

                    list.Add(name);
                }
                while (reader.NextResult());

                return MakeUnique(list).ToList();
            }
        }

        public DataTable LoadTable(string connectionString, string tableName)
{
    using (var stream = OpenStream(connectionString))
    using (var reader = ExcelReaderFactory.CreateReader(stream))
    {
        MoveToSheet(reader, tableName);

        // Для Excel: первая строка — это данные, а "шапка" формируется как A,B,C...
        // Дополнительно добавляем системный столбец "Row" (номер строки) — он используется как ключ.
        //
        // Важно: ExcelDataReader иногда возвращает большой FieldCount из-за форматирования/стилей.
        // Чтобы не тянуть сотни пустых колонок (и не падать UI), мы добавляем только те столбцы,
        // в которых реально встречаются осмысленные значения.
        var dt = new DataTable(tableName)
        {
            Locale = CultureInfo.InvariantCulture
        };

        dt.Columns.Add(ExcelRowNumberColumnName, typeof(int));
        dt.BeginLoadData();

        var rowNumber = 0;
        var maxFieldCountSeen = 0;
        var maxEffectiveColsSeen = 0;

        while (reader.Read())
        {
            rowNumber++;

            var fc = reader.FieldCount;
            if (fc > maxFieldCountSeen)
                maxFieldCountSeen = fc;

            object[] normalized = fc > 0 ? new object[fc] : System.Array.Empty<object>();
            var lastNonEmpty = -1;

            for (var i = 0; i < fc; i++)
            {
                var raw = reader.GetValue(i);
                normalized[i] = NormalizeCellValueToStringOrDbNull(raw);

                if (HasMeaningfulExcelValue(raw))
                    lastNonEmpty = i;
            }

            var effectiveCols = lastNonEmpty + 1; // 0..fc
            if (effectiveCols > maxEffectiveColsSeen)
                maxEffectiveColsSeen = effectiveCols;

            // 0 = Row, 1.. = A,B,C...
            EnsureExcelColumns(dt, effectiveCols);

            var dr = dt.NewRow();
            dr[0] = rowNumber;

            for (var i = 0; i < effectiveCols; i++)
            {
                dr[i + 1] = normalized[i];
            }

            dt.Rows.Add(dr);
        }

        dt.EndLoadData();

        if (maxFieldCountSeen > 0 && maxFieldCountSeen > maxEffectiveColsSeen)
        {
            AppLogger.Info(
                $"Excel-лист '{tableName}': FieldCount={maxFieldCountSeen}, " +
                $"фактически по данным={maxEffectiveColsSeen}. Лишние пустые столбцы игнорируются.");
        }

        return dt;
    }
}


        public string[] GetPrimaryKeyColumns(string connectionString, string tableName)
{
    // Для Excel по умолчанию считаем ключом номер строки.
    // Это нужно, когда в листе нет явного PK/ID-колонки, а первая строка — это данные, а не заголовки.
    return new[] { ExcelRowNumberColumnName };
}

        public int GetTableRowCount(string connectionString, string tableName)
{
    using (var stream = OpenStream(connectionString))
    using (var reader = ExcelReaderFactory.CreateReader(stream))
    {
        MoveToSheet(reader, tableName);

        var count = 0;
        while (reader.Read())
        {
            count++;
        }

        return count;
    }
}

    public ColumnInfo[] GetTableColumns(string connectionString, string tableName)
{
    using (var stream = OpenStream(connectionString))
    using (var reader = ExcelReaderFactory.CreateReader(stream))
    {
        MoveToSheet(reader, tableName);

        // Первая строка НЕ считается заголовком.
        var fieldCount = 0;
        if (reader.Read())
            fieldCount = reader.FieldCount;

        var cols = new List<ColumnInfo>(1 + fieldCount)
        {
            new ColumnInfo(ExcelRowNumberColumnName, typeof(int))
        };

        for (var i = 0; i < fieldCount; i++)
        {
            cols.Add(new ColumnInfo(ToExcelColumnName(i), typeof(string)));
        }

        return cols.ToArray();
    }
}

    public Dictionary<string, ulong> LoadKeyHashMap(
    string connectionString,
    string tableName,
    string[] keyColumns,
    ColumnInfo[] tableColumns,
    CancellationToken cancellationToken)
{
    if (tableColumns == null || tableColumns.Length == 0)
        throw new ArgumentException("tableColumns пустой.", nameof(tableColumns));
    if (keyColumns == null || keyColumns.Length == 0)
        throw new ArgumentException("keyColumns пустой.", nameof(keyColumns));

    // Маппинг имя->ординал для RowHashing
    var nameToOrdinal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < tableColumns.Length; i++)
        nameToOrdinal[tableColumns[i].Name] = i;

    var keyOrdinals = keyColumns
        .Select(k => nameToOrdinal.TryGetValue(k, out var ord)
            ? ord
            : throw new InvalidOperationException($"Колонка ключа '{k}' не найдена в схеме листа '{tableName}'."))
        .ToArray();

    var hashOrdinals = Enumerable.Range(0, tableColumns.Length).ToArray();
    var expectedTypes = tableColumns.Select(c => c.DataType).ToArray();

    var map = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

    using (var stream = OpenStream(connectionString))
    using (var reader = ExcelReaderFactory.CreateReader(stream))
    {
        MoveToSheet(reader, tableName);

        var record = new ArrayDataRecord(tableColumns.Length);
        var rowNumber = 0;

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            // 0 = Row, 1.. = A,B,C...
            record.Values[0] = rowNumber;

            var fc = reader.FieldCount;
            var maxDataCols = tableColumns.Length - 1;

            for (var i = 0; i < maxDataCols; i++)
            {
                object v = i < fc ? reader.GetValue(i) : null;
                record.Values[i + 1] = NormalizeCellValueToStringOrDbNull(v);
            }

            var key = RowHashing.BuildKey(record, keyOrdinals);
            var hash = RowHashing.ComputeRowHash(record, hashOrdinals, expectedTypes);

            map[key] = hash;
        }
    }

    return map;
}


        public ProviderCapabilities GetCapabilities(string connectionString)
        {
            // Excel: только чтение
            return ProviderCapabilities.Read;
        }

        public void ApplyRowChanges(
            string targetConnectionString,
            string tableName,
            string[] primaryKeyColumns,
            IEnumerable<RowPair> pairsToApply)
        {
            throw new NotSupportedException("Excel-провайдер пока поддерживает только сравнение (чтение). Применение изменений в Excel не реализовано.");
        }

        public void ReplaceTable(string connectionString, string tableName, DataTable dataTable)
        {
            throw new NotSupportedException("Excel-провайдер пока поддерживает только сравнение (чтение). Замена листа не реализована.");
        }

        public void DropTable(string connectionString, string tableName)
        {
            throw new NotSupportedException("Excel-провайдер пока поддерживает только сравнение (чтение). Удаление листа не реализовано.");
        }

        private Stream OpenStream(string connectionString)
        {
            byte[] bytes;
            if (_bytesCache.TryGetValue(connectionString, out bytes) && bytes != null)
                return new MemoryStream(bytes, writable: false);

            var path = ExtractFilePath(connectionString);
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        private static bool MoveToSheet(IExcelDataReader reader, string tableName)
        {
            if (reader == null)
                return false;

            if (string.IsNullOrWhiteSpace(tableName))
                return true;

            do
            {
                if (string.Equals(reader.Name, tableName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            while (reader.NextResult());

            return false;
        }

        private static string ExtractFilePath(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return string.Empty;

            var cs = connectionString.Trim();

            const string prefix = "ExcelFile=";
            if (cs.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cs = cs.Substring(prefix.Length);
                int semi = cs.IndexOf(';');
                if (semi >= 0)
                    cs = cs.Substring(0, semi);

                return cs.Trim().Trim('"');
            }

            // на всякий случай — если кто-то передал прям путь
            return cs.Trim().Trim('"');
        }

        private static string[] MakeUnique(IEnumerable<string> names)
        {
            var list = new List<string>();
            var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var n in names)
            {
                var name = string.IsNullOrWhiteSpace(n) ? "Column" + (list.Count + 1) : n;

                int count;
                if (!used.TryGetValue(name, out count))
                {
                    used[name] = 1;
                    list.Add(name);
                }
                else
                {
                    count++;
                    used[name] = count;
                    list.Add(name + "_" + count);
                }
            }

            return list.ToArray();
        }


private static bool HasMeaningfulExcelValue(object raw)
{
    if (raw == null || raw is DBNull)
        return false;

    if (raw is string s)
        return !string.IsNullOrWhiteSpace(s);

    // числа, даты, bool и т.п. считаем осмысленными значениями (включая 0)
    return true;
}

private static void EnsureExcelColumns(DataTable dt, int fieldCount)
{
    if (dt == null) throw new ArgumentNullException(nameof(dt));
    if (dt.Columns.Count == 0)
        throw new InvalidOperationException("Ожидался системный столбец номера строки (Row) в DataTable.");

    if (fieldCount < 0) fieldCount = 0;

    // 0 = Row, 1.. = A,B,C...
    var existingDataCols = dt.Columns.Count - 1;
    if (existingDataCols >= fieldCount) return;

    for (var i = existingDataCols; i < fieldCount; i++)
    {
        dt.Columns.Add(ToExcelColumnName(i), typeof(string));
    }
}

private static object NormalizeCellValueToStringOrDbNull(object value)
{
    if (value == null || value is DBNull)
        return DBNull.Value;

    // ExcelDataReader может вернуть double/DateTime/bool и т.п.
    // Для сравнения нам нужен максимально стабильный (кросс-машинный) формат, без зависимости от CultureInfo.CurrentCulture.
    // Поэтому нормализуем ключевые типы в InvariantCulture.
    if (value is string s)
        return s;

    if (value is bool b)
        return b ? "true" : "false";

    if (value is DateTime dt)
        return dt.ToString("o", CultureInfo.InvariantCulture);

    if (value is DateTimeOffset dto)
        return dto.ToString("o", CultureInfo.InvariantCulture);

    if (value is double d)
        return d.ToString("G17", CultureInfo.InvariantCulture);

    if (value is float f)
        return f.ToString("R", CultureInfo.InvariantCulture);

    if (value is decimal dec)
        return dec.ToString(CultureInfo.InvariantCulture);

    if (value is IFormattable formattable)
        return formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}

private static string ToExcelColumnName(int zeroBasedIndex)
{
    if (zeroBasedIndex < 0)
        throw new ArgumentOutOfRangeException(nameof(zeroBasedIndex));

    // 0 -> A, 25 -> Z, 26 -> AA, ...
    var sb = new StringBuilder();
    var n = zeroBasedIndex;

    while (true)
    {
        var rem = n % 26;
        sb.Insert(0, (char)('A' + rem));
        n = (n / 26) - 1;
        if (n < 0)
            break;
    }

    return sb.ToString();
}

/// <summary>
/// Лёгкая обёртка над object[] чтобы можно было использовать RowHashing без DataTable/DataReader.
/// </summary>
private sealed class ArrayDataRecord : IDataRecord
{
    public ArrayDataRecord(int fieldCount)
    {
        if (fieldCount <= 0) throw new ArgumentOutOfRangeException(nameof(fieldCount));
        Values = new object[fieldCount];
    }

    public object[] Values { get; }

    public int FieldCount => Values.Length;

    public object this[int i] => GetValue(i);

    public object this[string name] => throw new NotSupportedException();

    public bool GetBoolean(int i) => Convert.ToBoolean(GetValue(i), CultureInfo.CurrentCulture);

    public byte GetByte(int i) => Convert.ToByte(GetValue(i), CultureInfo.CurrentCulture);

    public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        => throw new NotSupportedException();

    public char GetChar(int i) => Convert.ToChar(GetValue(i), CultureInfo.CurrentCulture);

    public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        => throw new NotSupportedException();

    public IDataReader GetData(int i) => throw new NotSupportedException();

    public string GetDataTypeName(int i) => GetFieldType(i).Name;

    public DateTime GetDateTime(int i) => Convert.ToDateTime(GetValue(i), CultureInfo.CurrentCulture);

    public decimal GetDecimal(int i) => Convert.ToDecimal(GetValue(i), CultureInfo.CurrentCulture);

    public double GetDouble(int i) => Convert.ToDouble(GetValue(i), CultureInfo.CurrentCulture);

    public Type GetFieldType(int i)
    {
        var v = GetValue(i);
        return v == null || v is DBNull ? typeof(object) : v.GetType();
    }

    public float GetFloat(int i) => Convert.ToSingle(GetValue(i), CultureInfo.CurrentCulture);

    public Guid GetGuid(int i)
    {
        var v = GetValue(i);
        if (v is Guid g) return g;
        if (v is string s) return Guid.Parse(s);
        throw new InvalidCastException();
    }

    public short GetInt16(int i) => Convert.ToInt16(GetValue(i), CultureInfo.CurrentCulture);

    public int GetInt32(int i) => Convert.ToInt32(GetValue(i), CultureInfo.CurrentCulture);

    public long GetInt64(int i) => Convert.ToInt64(GetValue(i), CultureInfo.CurrentCulture);

    public string GetName(int i) => i.ToString(CultureInfo.InvariantCulture);

    public int GetOrdinal(string name) => throw new NotSupportedException();

    public string GetString(int i) => Convert.ToString(GetValue(i), CultureInfo.CurrentCulture);

    public object GetValue(int i) => Values[i];

    public int GetValues(object[] values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        var n = Math.Min(values.Length, Values.Length);
        Array.Copy(Values, values, n);
        return n;
    }

    public bool IsDBNull(int i)
    {
        var v = Values[i];
        return v == null || v is DBNull;
    }
}
    }
}
