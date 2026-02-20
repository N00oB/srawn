using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    /// <summary>
    /// Провайдер сравнения "папка с CSV".
    /// Папка считается базой данных, каждый *.csv — таблица.
    /// </summary>
    internal sealed class CsvFolderDatabaseProvider : IDatabaseProvider, IFastRowHashProvider
    {
        private const string CsPrefix = "CsvFolder=";

        private sealed class CsvFolderOptions
        {
            public string FolderPath;
            public bool Recursive;
        }

        public List<string> GetTableNames(string connectionString)
        {
            var opt = ParseOptions(connectionString);
            if (string.IsNullOrWhiteSpace(opt.FolderPath) || !Directory.Exists(opt.FolderPath))
                throw new InvalidOperationException("Папка с CSV-файлами не найдена: '" + opt.FolderPath + "'.");

            var files = EnumerateCsvFiles(opt.FolderPath, opt.Recursive);
            var names = new List<string>();

            foreach (var f in files)
            {
                string tableName = BuildTableName(opt.FolderPath, f, opt.Recursive);
                if (!string.IsNullOrWhiteSpace(tableName))
                    names.Add(tableName);
            }

            return names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public DataTable LoadTable(string connectionString, string tableName)
        {
            var opt = ParseOptions(connectionString);
            var path = ResolveCsvFilePath(opt, tableName);

            if (!File.Exists(path))
            {
                // Пустая таблица, но со схемой хотя бы из 1 колонки "Key".
                var empty = new DataTable(tableName);
                empty.Columns.Add("Key", typeof(string));
                return empty;
            }

            var enc = DetectEncoding(path);
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, enc, detectEncodingFromByteOrderMarks: true))
            {
                string headerLine = ReadFirstNonEmptyLine(sr);
                if (headerLine == null)
                {
                    var empty = new DataTable(tableName);
                    empty.Columns.Add("Key", typeof(string));
                    return empty;
                }

                var delimiter = DetectDelimiter(headerLine);
                var headerFields = ParseCsvLine(headerLine, delimiter);

                var colNames = MakeUnique(headerFields);

                var dt = new DataTable(tableName);
                foreach (var c in colNames)
                    dt.Columns.Add(c, typeof(string));

                // строки
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var fields = ParseCsvLine(line, delimiter);

                    EnsureColumns(dt, fields.Count);

                    var row = dt.NewRow();
                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        string v = i < fields.Count ? (fields[i] ?? "") : "";
                        row[i] = v;
                    }
                    dt.Rows.Add(row);
                }

                return dt;
            }
        }

        public string[] GetPrimaryKeyColumns(string connectionString, string tableName)
        {
            // По умолчанию — первая колонка.
            // Пользователь может переопределить ключи через существующий механизм CustomKeys в конфиге.
            try
            {
                var cols = GetTableColumns(connectionString, tableName);
                if (cols != null && cols.Length > 0 && !string.IsNullOrWhiteSpace(cols[0].Name))
                    return new[] { cols[0].Name };
            }
            catch
            {
                // игнорируем
            }

            return new[] { "Key" };
        }

        public void ApplyRowChanges(
            string connectionString,
            string tableName,
            string[] primaryKeyColumns,
            IEnumerable<RowPair> rows)
        {
            if (rows == null)
                return;

            var opt = ParseOptions(connectionString);
            var path = ResolveCsvFilePath(opt, tableName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            // Загружаем таблицу приёмника (если файла нет — создадим новую по схеме источника).
            DataTable target;
            if (File.Exists(path))
            {
                target = LoadTable(connectionString, tableName);
            }
            else
            {
                // Берём схему из первой строки источника
                var first = rows.FirstOrDefault(r => r?.SourceRow != null)?.SourceRow;
                target = new DataTable(tableName);

                if (first?.Table?.Columns != null && first.Table.Columns.Count > 0)
                {
                    foreach (DataColumn c in first.Table.Columns)
                        target.Columns.Add(c.ColumnName, typeof(string));
                }
                else
                {
                    target.Columns.Add("Key", typeof(string));
                }
            }

            // PK
            var pk = (primaryKeyColumns != null && primaryKeyColumns.Length > 0 &&
                      !string.IsNullOrWhiteSpace(primaryKeyColumns[0]))
                ? primaryKeyColumns[0]
                : (target.Columns.Count > 0 ? target.Columns[0].ColumnName : "Key");

            if (!target.Columns.Contains(pk))
                target.Columns.Add(pk, typeof(string));

            // Индекс по ключу
            var index = new Dictionary<string, DataRow>(StringComparer.Ordinal);
            foreach (DataRow tr in target.Rows)
            {
                var key = Convert.ToString(tr[pk], CultureInfo.CurrentCulture) ?? "";
                index[key] = tr;
            }

            foreach (var pair in rows)
            {
                if (pair == null) continue;
                if (pair.SourceRow == null) continue;

                var srcRow = pair.SourceRow;
                var key = srcRow.Table.Columns.Contains(pk)
                    ? (Convert.ToString(srcRow[pk], CultureInfo.CurrentCulture) ?? "")
                    : (Convert.ToString(srcRow[0], CultureInfo.CurrentCulture) ?? "");

                if (!index.TryGetValue(key, out var trgRow))
                {
                    trgRow = target.NewRow();
                    target.Rows.Add(trgRow);
                    index[key] = trgRow;
                }

                // Обновляем/добавляем колонки из источника
                foreach (DataColumn sc in srcRow.Table.Columns)
                {
                    if (sc == null || string.IsNullOrWhiteSpace(sc.ColumnName))
                        continue;

                    if (!target.Columns.Contains(sc.ColumnName))
                        target.Columns.Add(sc.ColumnName, typeof(string));

                    trgRow[sc.ColumnName] = Convert.ToString(srcRow[sc.ColumnName], CultureInfo.CurrentCulture) ?? "";
                }
            }

            // Пишем обратно. Стараемся сохранить исходные разделитель/кодировку.
            var enc = DetectEncoding(path);
            var delimiter = DetectDelimiterFromFile(path, enc);
            SaveTableToCsv(path, target, delimiter, enc);
        }

        public void ReplaceTable(string connectionString, string tableName, DataTable newTable)
        {
            if (newTable == null)
                throw new ArgumentNullException(nameof(newTable));

            var opt = ParseOptions(connectionString);
            var path = ResolveCsvFilePath(opt, tableName);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var enc = DetectEncoding(path);
            var delimiter = DetectDelimiterFromFile(path, enc);
            SaveTableToCsv(path, newTable, delimiter, enc);
        }

        public void DropTable(string connectionString, string tableName)
        {
            var opt = ParseOptions(connectionString);
            var path = ResolveCsvFilePath(opt, tableName);
            if (!File.Exists(path))
                return;

            File.Delete(path);
        }

        // -------------------- FAST SUMMARY --------------------

        public ColumnInfo[] GetTableColumns(string connectionString, string tableName)
        {
            var opt = ParseOptions(connectionString);
            var path = ResolveCsvFilePath(opt, tableName);
            if (!File.Exists(path))
                return new[] { new ColumnInfo("Key", typeof(string)) };

            var enc = DetectEncoding(path);
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, enc, detectEncodingFromByteOrderMarks: true))
            {
                var headerLine = ReadFirstNonEmptyLine(sr);
                if (headerLine == null)
                    return new[] { new ColumnInfo("Key", typeof(string)) };

                var delimiter = DetectDelimiter(headerLine);
                var headerFields = ParseCsvLine(headerLine, delimiter);
                var names = MakeUnique(headerFields);

                int maxFields = names.Length;

                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var fields = ParseCsvLine(line, delimiter);
                    if (fields.Count > maxFields)
                        maxFields = fields.Count;
                }

                if (maxFields > names.Length)
                {
                    var extended = new List<string>(names);
                    for (int i = names.Length; i < maxFields; i++)
                        extended.Add("Column" + (i + 1));
                    names = MakeUnique(extended).ToArray();
                }

                return names.Select(n => new ColumnInfo(n, typeof(string))).ToArray();
            }
        }

        public Dictionary<string, ulong> LoadKeyHashMap(
            string connectionString,
            string tableName,
            string[] keyColumns,
            ColumnInfo[] tableColumns,
            CancellationToken cancellationToken)
        {
            if (keyColumns == null || keyColumns.Length == 0)
                throw new ArgumentException("keyColumns пустой.", nameof(keyColumns));
            if (tableColumns == null || tableColumns.Length == 0)
                throw new ArgumentException("tableColumns пустой.", nameof(tableColumns));

            var opt = ParseOptions(connectionString);
            var path = ResolveCsvFilePath(opt, tableName);

            var map = new Dictionary<string, ulong>(StringComparer.Ordinal);

            if (!File.Exists(path))
                return map;

            // Ordinals
            var nameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tableColumns.Length; i++)
            {
                var n = tableColumns[i]?.Name;
                if (string.IsNullOrWhiteSpace(n)) continue;
                if (!nameToIndex.ContainsKey(n))
                    nameToIndex[n] = i;
            }

            var keyOrdinals = new int[keyColumns.Length];
            for (int i = 0; i < keyColumns.Length; i++)
            {
                var kc = keyColumns[i];
                if (string.IsNullOrWhiteSpace(kc) || !nameToIndex.TryGetValue(kc, out var ord))
                    ord = 0;
                keyOrdinals[i] = ord;
            }

            int colCount = tableColumns.Length;
            var enc = DetectEncoding(path);

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, enc, detectEncodingFromByteOrderMarks: true))
            {
                var headerLine = ReadFirstNonEmptyLine(sr);
                if (headerLine == null)
                    return map;

                var delimiter = DetectDelimiter(headerLine);

                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var fields = ParseCsvLine(line, delimiter);
                    if (fields.Count < colCount)
                    {
                        // дополняем пустыми
                        for (int i = fields.Count; i < colCount; i++)
                            fields.Add("");
                    }

                    string key = BuildKey(fields, keyOrdinals);
                    ulong hash = ComputeStringRowHash(fields, colCount);

                    map[key] = hash;
                }
            }

            return map;
        }

        // -------------------- Helpers --------------------

        private static CsvFolderOptions ParseOptions(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("connectionString пустой.", nameof(connectionString));

            var cs = connectionString.Trim();

            // поддержка "просто путь" (если кто-то передал напрямую)
            if (!cs.StartsWith(CsPrefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(cs))
                return new CsvFolderOptions { FolderPath = cs, Recursive = false };

            if (!cs.StartsWith(CsPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Неверный формат строки подключения CSV-папки. Ожидалось: CsvFolder=...;");

            // CsvFolder=...;Recursive=0;
            var opt = new CsvFolderOptions();

            // FolderPath
            var rest = cs.Substring(CsPrefix.Length);
            int semi = rest.IndexOf(';');
            var folder = semi >= 0 ? rest.Substring(0, semi) : rest;
            folder = folder.Trim().Trim('"');
            opt.FolderPath = folder;

            // остальные параметры
            var parts = cs.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var kv = p.Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;

                var k = kv[0]?.Trim();
                var v = kv[1]?.Trim();
                if (string.IsNullOrWhiteSpace(k)) continue;

                if (string.Equals(k, "Recursive", StringComparison.OrdinalIgnoreCase))
                {
                    opt.Recursive = IsTrue(v);
                }
            }

            return opt;
        }

        private static bool IsTrue(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return false;
            v = v.Trim();
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> EnumerateCsvFiles(string folder, bool recursive)
        {
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.EnumerateFiles(folder, "*.csv", option);
        }

        private static string BuildTableName(string rootFolder, string filePath, bool recursive)
        {
            if (!recursive)
                return Path.GetFileNameWithoutExtension(filePath);

            var rel = GetRelativePath(rootFolder, filePath);
            if (string.IsNullOrWhiteSpace(rel))
                rel = Path.GetFileName(filePath);

            // без расширения
            if (rel.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                rel = rel.Substring(0, rel.Length - 4);

            // нормализуем разделители
            rel = rel.Replace('/', '\\');
            return rel;
        }

        private static string GetRelativePath(string baseDir, string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(baseDir) || string.IsNullOrWhiteSpace(filePath))
                    return filePath;

                string b = baseDir;
                if (!b.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    b += Path.DirectorySeparatorChar;

                var baseUri = new Uri(b, UriKind.Absolute);
                var fileUri = new Uri(filePath, UriKind.Absolute);
                var rel = Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString());
                return rel.Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return filePath;
            }
        }

        private static string ResolveCsvFilePath(CsvFolderOptions opt, string tableName)
        {
            if (opt == null)
                throw new ArgumentNullException(nameof(opt));
            if (string.IsNullOrWhiteSpace(opt.FolderPath))
                throw new InvalidOperationException("Папка CSV не задана.");
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("tableName пустой.", nameof(tableName));

            // tableName может содержать подпапки (если включена рекурсия)
            var safe = tableName.Trim().Trim('"');

            // запрещаем абсолютные пути из tableName
            if (Path.IsPathRooted(safe))
                safe = Path.GetFileNameWithoutExtension(safe);

            // если расширение уже есть — не дублируем
            var fileName = safe.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? safe : safe + ".csv";
            return Path.Combine(opt.FolderPath, fileName);
        }

        private static Encoding DetectEncoding(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // BOM
                    byte[] bom = new byte[4];
                    int read = fs.Read(bom, 0, 4);

                    if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

                    if (read >= 2)
                    {
                        if (bom[0] == 0xFF && bom[1] == 0xFE)
                            return Encoding.Unicode; // UTF-16 LE
                        if (bom[0] == 0xFE && bom[1] == 0xFF)
                            return Encoding.BigEndianUnicode;
                    }

                    // Без BOM: эвристика UTF-8 vs Windows-1251.
                    fs.Position = 0;
                    byte[] sample = new byte[4096];
                    int n = fs.Read(sample, 0, sample.Length);
                    if (n > 0)
                    {
                        if (LooksLikeUtf8(sample, n))
                            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                    }
                }
            }
            catch
            {
                // игнорируем
            }

            // Частый случай для "конфигов" — Windows-1251.
            return Encoding.GetEncoding(1251);
        }

        /// <summary>
        /// Простейшая проверка валидности UTF-8 последовательностей.
        /// Достаточно для различения UTF-8/ANSI на реальных CSV.
        /// </summary>
        private static bool LooksLikeUtf8(byte[] bytes, int length)
        {
            if (bytes == null || length <= 0) return false;

            int i = 0;
            while (i < length)
            {
                byte b = bytes[i];
                if (b <= 0x7F)
                {
                    i++;
                    continue;
                }

                // 2-byte
                if (b >= 0xC2 && b <= 0xDF)
                {
                    if (i + 1 >= length) return false;
                    byte b2 = bytes[i + 1];
                    if (b2 < 0x80 || b2 > 0xBF) return false;
                    i += 2;
                    continue;
                }

                // 3-byte
                if (b >= 0xE0 && b <= 0xEF)
                {
                    if (i + 2 >= length) return false;
                    byte b2 = bytes[i + 1];
                    byte b3 = bytes[i + 2];
                    if (b2 < 0x80 || b2 > 0xBF) return false;
                    if (b3 < 0x80 || b3 > 0xBF) return false;

                    // overlongs / surrogates basic checks
                    if (b == 0xE0 && b2 < 0xA0) return false;
                    if (b == 0xED && b2 >= 0xA0) return false;

                    i += 3;
                    continue;
                }

                // 4-byte
                if (b >= 0xF0 && b <= 0xF4)
                {
                    if (i + 3 >= length) return false;
                    byte b2 = bytes[i + 1];
                    byte b3 = bytes[i + 2];
                    byte b4 = bytes[i + 3];
                    if (b2 < 0x80 || b2 > 0xBF) return false;
                    if (b3 < 0x80 || b3 > 0xBF) return false;
                    if (b4 < 0x80 || b4 > 0xBF) return false;

                    if (b == 0xF0 && b2 < 0x90) return false; // overlong
                    if (b == 0xF4 && b2 >= 0x90) return false; // > U+10FFFF

                    i += 4;
                    continue;
                }

                return false;
            }

            return true;
        }

        private static string ReadFirstNonEmptyLine(StreamReader sr)
        {
            if (sr == null) return null;
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    return line;
            }
            return null;
        }

        private static char DetectDelimiter(string headerLine)
        {
            if (string.IsNullOrEmpty(headerLine))
                return ';';

            var candidates = new[] { ';', ',', '\t', '|' };
            int bestCount = -1;
            char best = ';';

            foreach (var c in candidates)
            {
                int count = 0;
                for (int i = 0; i < headerLine.Length; i++)
                {
                    if (headerLine[i] == c)
                        count++;
                }

                if (count > bestCount)
                {
                    bestCount = count;
                    best = c;
                }
            }

            return best;
        }

        private static char DetectDelimiterFromFile(string path, Encoding enc)
        {
            try
            {
                if (!File.Exists(path))
                    return ';';

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, enc, detectEncodingFromByteOrderMarks: true))
                {
                    var header = ReadFirstNonEmptyLine(sr);
                    if (header == null)
                        return ';';

                    return DetectDelimiter(header);
                }
            }
            catch
            {
                return ';';
            }
        }

        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            if (line == null)
            {
                result.Add("");
                return result;
            }

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];

                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // экранированная кавычка ""
                        sb.Append('"');
                        i++;
                        continue;
                    }

                    inQuotes = !inQuotes;
                    continue;
                }

                if (ch == delimiter && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Length = 0;
                    continue;
                }

                sb.Append(ch);
            }

            result.Add(sb.ToString());
            return result;
        }

        private static string[] MakeUnique(IEnumerable<string> names)
        {
            var list = new List<string>();
            var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            int i = 0;
            foreach (var raw in names ?? Enumerable.Empty<string>())
            {
                i++;
                var name = string.IsNullOrWhiteSpace(raw) ? "Column" + i : raw.Trim();

                if (!used.TryGetValue(name, out var count))
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

            // если вдруг вообще пусто
            if (list.Count == 0)
                list.Add("Column1");

            return list.ToArray();
        }

        private static void EnsureColumns(DataTable dt, int fieldCount)
        {
            if (dt == null) return;
            if (fieldCount <= dt.Columns.Count) return;

            for (int i = dt.Columns.Count; i < fieldCount; i++)
            {
                dt.Columns.Add("Column" + (i + 1), typeof(string));
            }
        }

        private static void SaveTableToCsv(string path, DataTable table, char delimiter, Encoding enc)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));
            if (enc == null)
                enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

            // Если файл новый — по умолчанию пишем UTF-8 с BOM.
            if (!File.Exists(path))
                enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, enc))
            {
                // header
                var header = new List<string>();
                foreach (DataColumn c in table.Columns)
                    header.Add(EscapeCsv(Convert.ToString(c.ColumnName, CultureInfo.CurrentCulture) ?? "", delimiter));

                sw.WriteLine(string.Join(delimiter.ToString(), header));

                foreach (DataRow r in table.Rows)
                {
                    var fields = new List<string>(table.Columns.Count);
                    for (int i = 0; i < table.Columns.Count; i++)
                    {
                        var raw = r[i];
                        var s = raw == null || raw == DBNull.Value ? "" : Convert.ToString(raw, CultureInfo.CurrentCulture) ?? "";
                        fields.Add(EscapeCsv(s, delimiter));
                    }

                    sw.WriteLine(string.Join(delimiter.ToString(), fields));
                }
            }
        }

        private static string EscapeCsv(string value, char delimiter)
        {
            if (value == null) value = "";

            bool mustQuote = value.IndexOf(delimiter) >= 0 ||
                             value.IndexOf('"') >= 0 ||
                             value.IndexOf('\r') >= 0 ||
                             value.IndexOf('\n') >= 0;

            if (!mustQuote)
                return value;

            var escaped = value.Replace("\"", "\"\"");
            return "\"" + escaped + "\"";
        }

        private static string BuildKey(List<string> fields, int[] keyOrdinals)
        {
            var sb = new StringBuilder(64);

            for (int i = 0; i < keyOrdinals.Length; i++)
            {
                if (i > 0) sb.Append('|');

                int ord = keyOrdinals[i];
                string v = (ord >= 0 && ord < fields.Count) ? fields[ord] : "";
                if (v == null)
                    sb.Append("<NULL>");
                else
                    sb.Append(v);
            }

            return sb.ToString();
        }

        // FNV-1a 64-bit, как в RowHashing
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private static ulong ComputeStringRowHash(List<string> fields, int colCount)
        {
            ulong hash = FnvOffset;

            for (int i = 0; i < colCount; i++)
            {
                // разделитель между столбцами
                hash = AddByte(hash, 0x1F);
                // маркер типа string
                hash = AddByte(hash, 1);

                string s = i < fields.Count ? (fields[i] ?? "") : "";
                hash = AddString(hash, s);
            }

            return hash;
        }

        private static ulong AddString(ulong hash, string s)
        {
            if (s == null) s = "";
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            return AddBytes(hash, bytes);
        }

        private static ulong AddBytes(ulong hash, byte[] bytes)
        {
            if (bytes == null) return hash;
            for (int i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= FnvPrime;
            }
            return hash;
        }

        private static ulong AddByte(ulong hash, byte b)
        {
            hash ^= b;
            hash *= FnvPrime;
            return hash;
        }
    }
}
