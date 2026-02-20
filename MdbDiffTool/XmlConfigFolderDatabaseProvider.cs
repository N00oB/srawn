using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    /// <summary>
    /// Провайдер сравнения "папка с XML .config".
    /// Папка считается базой данных, каждый *.config — таблица.
    /// 
    /// Важно: провайдер пока read-only (Apply/Replace не поддержаны),
    /// потому что структура XML сложная и требует отдельного "писателя" под формат.
    /// </summary>
    internal sealed class XmlConfigFolderDatabaseProvider : IDatabaseProvider
    {
        private const string CsPrefix = "XmlConfigFolder=";

        private const string TableSuffixGroups = "groups";
        private const string TableSuffixRegs = "regs";

        private sealed class Options
        {
            public string FolderPath;
            public bool Recursive;
        }

        private sealed class PkCacheEntry
        {
            public DateTime LastWriteUtc;
            public string[] PkColumns;
        }

        private static readonly object _pkLock = new object();
        private static readonly Dictionary<string, PkCacheEntry> _pkCache =
            new Dictionary<string, PkCacheEntry>(StringComparer.OrdinalIgnoreCase);

        public List<string> GetTableNames(string connectionString)
        {
            var opt = ParseOptions(connectionString);
            if (string.IsNullOrWhiteSpace(opt.FolderPath) || !Directory.Exists(opt.FolderPath))
                throw new InvalidOperationException("Папка с .config файлами не найдена: '" + opt.FolderPath + "'.");

            var files = EnumerateConfigFiles(opt.FolderPath, opt.Recursive);
            var names = new List<string>();

            foreach (var f in files)
            {
                string baseName = BuildTableName(opt.FolderPath, f, opt.Recursive);
                if (string.IsNullOrWhiteSpace(baseName))
                    continue;

                // 1 файл = 2 таблицы: groups и regs
                names.Add(baseName + "\\" + TableSuffixGroups);
                names.Add(baseName + "\\" + TableSuffixRegs);
            }

            return names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public DataTable LoadTable(string connectionString, string tableName)
        {
            var opt = ParseOptions(connectionString);
            var parsed = ParseTableName(tableName);
            var baseTableName = parsed.BaseName;
            var kind = parsed.Kind;

            var path = ResolveFilePath(opt, baseTableName);

            if (!File.Exists(path))
            {
                return BuildEmptyTable(tableName, kind);
            }

            XDocument doc;
            try
            {
                doc = XDocument.Load(path, LoadOptions.None);
            }
            catch (Exception ex)
            {
                // Если XML битый — всё равно вернём таблицу, чтобы diff не падал.
                var dtErr = new DataTable(tableName);
                dtErr.Columns.Add("Key", typeof(string));
                dtErr.Columns.Add("Value", typeof(string));
                var r = dtErr.NewRow();
                r["Key"] = "ОшибкаXML";
                r["Value"] = ex.GetType().Name + ": " + ex.Message;
                dtErr.Rows.Add(r);
                CachePk(path, tableName, new[] { "Key" });
                return dtErr;
            }

            // Специализация под формат: Groups → group → reg
            if (doc.Root != null && string.Equals(doc.Root.Name.LocalName, "Groups", StringComparison.OrdinalIgnoreCase))
            {
                var built = BuildGroupsAndRegsTables(parsed.FullBaseNameForDisplay, doc);

                // Кэшируем PK обеих таблиц
                CachePk(path, built.Groups.TableName, new[] { "__groupKey" });
                CachePk(path, built.Regs.TableName, new[] { "__groupKey", "__regKey" });

                if (string.Equals(kind, TableSuffixRegs, StringComparison.OrdinalIgnoreCase))
                    return built.Regs;
                return built.Groups;
            }

            // 1) Пытаемся выбрать "строчный" элемент (повторяющиеся узлы)
            var rowElements = SelectRowElements(doc);

            DataTable dt;
            if (rowElements.Count == 0)
            {
                // 2) Фолбэк: листовые узлы в Key/Value
                dt = BuildKeyValueTable(tableName, doc);
                CachePk(path, tableName, new[] { "Key" });
                return dt;
            }

            dt = BuildRowsTable(tableName, rowElements);

            // Подбор ключа (PK) по данным
            var pk = GuessPrimaryKey(dt);
            if (pk == null || pk.Length == 0)
            {
                // крайний случай: ключ по номеру строки
                if (!dt.Columns.Contains("__row"))
                    dt.Columns.Add("__row", typeof(int));

                for (int i = 0; i < dt.Rows.Count; i++)
                    dt.Rows[i]["__row"] = i + 1;

                pk = new[] { "__row" };
            }

            CachePk(path, tableName, pk);
            return dt;
        }

        public string[] GetPrimaryKeyColumns(string connectionString, string tableName)
        {
            var opt = ParseOptions(connectionString);
            var parsed = ParseTableName(tableName);
            var path = ResolveFilePath(opt, parsed.BaseName);

            if (File.Exists(path))
            {
                var key = BuildPkCacheKey(path, tableName);
                var lw = File.GetLastWriteTimeUtc(path);

                lock (_pkLock)
                {
                    if (_pkCache.TryGetValue(key, out var e) &&
                        e != null &&
                        e.PkColumns != null &&
                        e.PkColumns.Length > 0 &&
                        e.LastWriteUtc == lw)
                    {
                        return e.PkColumns;
                    }
                }
            }

            // Если нет в кэше — загрузим таблицу (да, повторно), чтобы вычислить PK
            try
            {
                var dt = LoadTable(connectionString, tableName);

                // Для Groups/group/reg у нас фиксированные PK
                if (dt.Columns.Contains("__groupKey") && dt.Columns.Contains("__regKey"))
                    return new[] { "__groupKey", "__regKey" };
                if (dt.Columns.Contains("__groupKey"))
                    return new[] { "__groupKey" };

                var pk = GuessPrimaryKey(dt);
                if (pk != null && pk.Length > 0)
                    return pk;

                if (dt.Columns.Contains("__row"))
                    return new[] { "__row" };
            }
            catch
            {
                // игнорируем
            }

            return new[] { "Key" };
        }

        public void ApplyRowChanges(
            string targetConnectionString,
            string tableName,
            string[] primaryKeyColumns,
            IEnumerable<RowPair> pairsToApply)
        {
            throw new NotSupportedException("Применение изменений для XML .config пока не поддерживается (только сравнение).");
        }

        public void ReplaceTable(string targetConnectionString, string tableName, DataTable sourceTable)
        {
            throw new NotSupportedException("Замена таблицы для XML .config пока не поддерживается (только сравнение).");
        }

        public void DropTable(string connectionString, string tableName)
        {
            var opt = ParseOptions(connectionString);
            var path = ResolveFilePath(opt, tableName);

            if (File.Exists(path))
                File.Delete(path);
        }

        // -------------------- Parsing helpers --------------------

        private static List<XElement> SelectRowElements(XDocument doc)
        {
            var candidates = new Dictionary<string, List<XElement>>(StringComparer.OrdinalIgnoreCase);

            foreach (var e in doc.Descendants())
            {
                if (e == null) continue;
                if (e.Parent == null) continue; // root
                // строкой считаем узел, который содержит либо атрибуты, либо дочерние элементы
                if (!e.HasAttributes && !e.Elements().Any())
                    continue;

                var path = GetPathKey(e);
                if (!candidates.TryGetValue(path, out var list))
                {
                    list = new List<XElement>();
                    candidates[path] = list;
                }
                list.Add(e);
            }

            // берем только повторяющиеся
            var repeated = candidates
                .Where(kv => kv.Value != null && kv.Value.Count >= 2)
                .ToList();

            if (repeated.Count == 0)
                return new List<XElement>();

            double BestScore(KeyValuePair<string, List<XElement>> kv)
            {
                var list = kv.Value;
                if (list == null || list.Count == 0) return double.MinValue;

                int count = list.Count;
                int depth = list[0].Ancestors().Count(); // примерно одинаковая глубина
                double complexity = list.Take(10).Average(x => (x.Attributes().Count() + CountLeafChildren(x)));

                var name = list[0].Name.LocalName;
                double boost = 1.0;

                // фавориты
                if (IsPreferredRowName(name))
                    boost *= 1.8;

                // анти-фавориты
                if (IsBadRowName(name))
                    boost *= 0.5;

                // формула: много строк + чуть сложнее структура + чуть глубина
                return boost * (count * 10.0 + complexity * 5.0 + depth * 0.5);
            }

            var best = repeated
                .OrderByDescending(BestScore)
                .First();

            return best.Value ?? new List<XElement>();
        }

        private static bool IsPreferredRowName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = name.Trim();

            // наиболее частые имена "строк" в конфигурационных XML
            var p = new[]
            {
                "row","item","entry","record","signal","tag","point","channel",
                "reg","register","var","variable","device","module"
            };
            return p.Any(x => name.Equals(x, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsBadRowName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            name = name.Trim();

            var b = new[]
            {
                "value","text","string","int","double","float","bool",
                "comment","description","name"
            };
            return b.Any(x => name.Equals(x, StringComparison.OrdinalIgnoreCase));
        }

        private static int CountLeafChildren(XElement e)
        {
            if (e == null) return 0;
            int cnt = 0;
            foreach (var c in e.Elements())
            {
                if (!c.HasElements)
                    cnt++;
            }
            return cnt;
        }

        private static DataTable BuildRowsTable(string tableName, List<XElement> rows)
        {
            // Собираем схему по атрибутам и "листовым" дочерним элементам
            var attrNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var leafNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in rows)
            {
                foreach (var a in r.Attributes())
                {
                    var n = a.Name.LocalName;
                    if (!string.IsNullOrWhiteSpace(n))
                        attrNames.Add(n);
                }

                foreach (var c in r.Elements())
                {
                    if (c.HasElements)
                        continue;

                    var n = c.Name.LocalName;
                    if (!string.IsNullOrWhiteSpace(n))
                        leafNames.Add(n);
                }
            }

            var cols = new List<string>();

            // атрибуты: префикс a_
            foreach (var a in attrNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                cols.Add("a_" + a);

            // leaf children
            foreach (var n in leafNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                // избегаем конфликтов с a_*
                if (cols.Contains(n, StringComparer.OrdinalIgnoreCase))
                    cols.Add("c_" + n);
                else
                    cols.Add(n);
            }

            // если вообще ничего не нашли — добавим хотя бы XML
            if (cols.Count == 0)
                cols.Add("Xml");

            cols = MakeUnique(cols);

            var dt = new DataTable(tableName);
            foreach (var c in cols)
                dt.Columns.Add(c, typeof(string));

            foreach (var r in rows)
            {
                var row = dt.NewRow();

                foreach (var a in r.Attributes())
                {
                    var name = "a_" + a.Name.LocalName;
                    var col = FindColumn(dt, name);
                    if (col != null)
                        row[col] = a.Value ?? "";
                }

                foreach (var c in r.Elements())
                {
                    if (c.HasElements)
                        continue;

                    var baseName = c.Name.LocalName;
                    var col = FindColumn(dt, baseName) ?? FindColumn(dt, "c_" + baseName);
                    if (col != null)
                        row[col] = c.Value ?? "";
                }

                if (dt.Columns.Count == 1 && string.Equals(dt.Columns[0].ColumnName, "Xml", StringComparison.OrdinalIgnoreCase))
                    row[0] = r.ToString(SaveOptions.DisableFormatting);

                dt.Rows.Add(row);
            }

            return dt;
        }

        private static DataTable BuildKeyValueTable(string tableName, XDocument doc)
        {
            var dt = new DataTable(tableName);
            dt.Columns.Add("Key", typeof(string));
            dt.Columns.Add("Value", typeof(string));

            int i = 0;
            foreach (var e in doc.Descendants())
            {
                if (e.HasElements) continue;

                var key = GetPathKey(e);
                if (string.IsNullOrWhiteSpace(key))
                    key = "node_" + (++i).ToString();

                var row = dt.NewRow();
                row["Key"] = key;
                row["Value"] = e.Value ?? "";
                dt.Rows.Add(row);
            }

            if (dt.Rows.Count == 0)
            {
                var r = dt.NewRow();
                r["Key"] = "Empty";
                r["Value"] = "";
                dt.Rows.Add(r);
            }

            return dt;
        }

        private static string[] GuessPrimaryKey(DataTable dt)
        {
            if (dt == null || dt.Columns.Count == 0 || dt.Rows.Count == 0)
                return null;

            // приоритеты ключей
            var priority = new[]
            {
                "a_pk","pk",
                "a_id","id",
                "a_key","key",
                "a_name","name",
                "a_offset","offset",
                "offset(pk)","offsetpk","offset_pk",
                "n","a_n",
                "a_index","index",
                "a_uid","uid","a_guid","guid"
            };

            // список кандидатов (существующие колонки)
            var candidates = new List<string>();
            foreach (var p in priority)
            {
                var c = FindColumnName(dt, p);
                if (c != null && !candidates.Contains(c, StringComparer.OrdinalIgnoreCase))
                    candidates.Add(c);
            }

            // если приоритетов нет — пробуем первую колонку
            if (candidates.Count == 0)
                candidates.Add(dt.Columns[0].ColumnName);

            // 1) одиночный ключ
            foreach (var c in candidates)
            {
                if (IsUnique(dt, new[] { c }))
                    return new[] { c };
            }

            // 2) пары ключей (на всякий случай)
            for (int i = 0; i < candidates.Count; i++)
            {
                for (int j = i + 1; j < candidates.Count; j++)
                {
                    var pair = new[] { candidates[i], candidates[j] };
                    if (IsUnique(dt, pair))
                        return pair;
                }
            }

            return null;
        }

        private static bool IsUnique(DataTable dt, string[] cols)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow r in dt.Rows)
            {
                var parts = new List<string>(cols.Length);
                foreach (var c in cols)
                {
                    var v = r[c];
                    parts.Add(v == null ? "" : v.ToString());
                }
                var key = string.Join("||", parts);

                if (!set.Add(key))
                    return false;
            }
            return true;
        }

        private static DataColumn FindColumn(DataTable dt, string name)
        {
            if (dt == null || string.IsNullOrWhiteSpace(name)) return null;
            foreach (DataColumn c in dt.Columns)
            {
                if (string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            return null;
        }

        private static string FindColumnName(DataTable dt, string name)
        {
            var col = FindColumn(dt, name);
            return col?.ColumnName;
        }

        private static List<string> MakeUnique(IEnumerable<string> names)
        {
            var result = new List<string>();
            var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in names ?? Enumerable.Empty<string>())
            {
                var n = string.IsNullOrWhiteSpace(raw) ? "Col" : raw.Trim();
                if (!used.ContainsKey(n))
                {
                    used[n] = 1;
                    result.Add(n);
                    continue;
                }

                int idx = ++used[n];
                string unique = n + "_" + idx.ToString();
                while (used.ContainsKey(unique))
                {
                    idx++;
                    unique = n + "_" + idx.ToString();
                }

                used[unique] = 1;
                result.Add(unique);
            }

            return result;
        }

        private static string GetPathKey(XElement e)
        {
            if (e == null) return null;
            var stack = new Stack<string>();
            var cur = e;
            while (cur != null)
            {
                stack.Push(cur.Name.LocalName);
                cur = cur.Parent;
            }
            return string.Join("/", stack.ToArray());
        }

        // -------------------- File/CS helpers --------------------

        private static Options ParseOptions(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("connectionString пустой.", nameof(connectionString));

            var cs = connectionString.Trim();

            // поддержка "просто путь" (если кто-то передал напрямую)
            if (!cs.StartsWith(CsPrefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(cs))
                return new Options { FolderPath = cs, Recursive = false };

            if (!cs.StartsWith(CsPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Неверный формат строки подключения XML .config папки. Ожидалось: XmlConfigFolder=...;");

            // XmlConfigFolder=...;Recursive=0;
            var opt = new Options();

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
                    opt.Recursive = IsTrue(v);
            }

            return opt;
        }

        private static bool IsTrue(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return false;
            v = v.Trim();
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> EnumerateConfigFiles(string folder, bool recursive)
        {
            var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.EnumerateFiles(folder, "*.config", opt);
        }

        private static string BuildTableName(string rootFolder, string filePath, bool recursive)
        {
            if (!recursive)
                return Path.GetFileNameWithoutExtension(filePath);

            var rel = GetRelativePath(rootFolder, filePath);
            if (string.IsNullOrWhiteSpace(rel))
                rel = Path.GetFileName(filePath);

            // без расширения
            if (rel.EndsWith(".config", StringComparison.OrdinalIgnoreCase))
                rel = rel.Substring(0, rel.Length - ".config".Length);

            // нормализуем разделители
            rel = rel.Replace('/', '\\');
            return rel;
        }

        private static string GetRelativePath(string baseDir, string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(baseDir) || string.IsNullOrWhiteSpace(filePath))
                    return null;

                baseDir = Path.GetFullPath(baseDir.Trim().Trim('"'));
                filePath = Path.GetFullPath(filePath.Trim().Trim('"'));

                if (!baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    baseDir += Path.DirectorySeparatorChar;

                if (filePath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                    return filePath.Substring(baseDir.Length);

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string ResolveFilePath(Options opt, string tableName)
        {
            if (opt == null)
                throw new ArgumentNullException(nameof(opt));
            if (string.IsNullOrWhiteSpace(opt.FolderPath))
                throw new InvalidOperationException("Папка .config не задана.");
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentException("tableName пустой.", nameof(tableName));

            var safe = tableName.Trim().Trim('"');

            if (Path.IsPathRooted(safe))
                safe = Path.GetFileNameWithoutExtension(safe);

            // если в имени таблицы есть суффикс "\\groups" или "\\regs" — отрезаем
            safe = StripKnownSuffix(safe);

            var fileName = safe.EndsWith(".config", StringComparison.OrdinalIgnoreCase)
                ? safe
                : safe + ".config";

            return Path.Combine(opt.FolderPath, fileName);
        }

        private static string StripKnownSuffix(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                return tableName;

            var norm = tableName.Replace('/', '\\');
            var parts = norm.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1)
                return tableName;

            var last = parts[parts.Length - 1];
            if (last.Equals(TableSuffixGroups, StringComparison.OrdinalIgnoreCase) ||
                last.Equals(TableSuffixRegs, StringComparison.OrdinalIgnoreCase))
            {
                return string.Join("\\", parts.Take(parts.Length - 1));
            }
            return tableName;
        }

        private sealed class ParsedTableName
        {
            public string BaseName;
            public string Kind;
            public string FullBaseNameForDisplay;
        }

        private static ParsedTableName ParseTableName(string tableName)
        {
            var p = new ParsedTableName
            {
                BaseName = tableName,
                Kind = TableSuffixGroups,
                FullBaseNameForDisplay = StripKnownSuffix(tableName ?? "")
            };

            if (string.IsNullOrWhiteSpace(tableName))
                return p;

            var norm = tableName.Replace('/', '\\').Trim();
            var parts = norm.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return p;

            var last = parts[parts.Length - 1];
            if (last.Equals(TableSuffixGroups, StringComparison.OrdinalIgnoreCase) ||
                last.Equals(TableSuffixRegs, StringComparison.OrdinalIgnoreCase))
            {
                p.Kind = last;
                p.BaseName = string.Join("\\", parts.Take(parts.Length - 1));
                p.FullBaseNameForDisplay = p.BaseName;
                return p;
            }

            p.Kind = TableSuffixGroups;
            p.BaseName = norm;
            p.FullBaseNameForDisplay = StripKnownSuffix(norm);
            return p;
        }

        private static DataTable BuildEmptyTable(string tableName, string kind)
        {
            var dt = new DataTable(tableName);
            if (string.Equals(kind, TableSuffixRegs, StringComparison.OrdinalIgnoreCase))
            {
                dt.Columns.Add("__groupKey", typeof(string));
                dt.Columns.Add("__regKey", typeof(string));
            }
            else
            {
                dt.Columns.Add("__groupKey", typeof(string));
            }
            return dt;
        }

        private sealed class GroupsRegsTables
        {
            public DataTable Groups;
            public DataTable Regs;
        }

        private static GroupsRegsTables BuildGroupsAndRegsTables(string baseTableName, XDocument doc)
        {
            var groupsName = baseTableName + "\\" + TableSuffixGroups;
            var regsName = baseTableName + "\\" + TableSuffixRegs;

            var dtGroups = new DataTable(groupsName);
            var dtRegs = new DataTable(regsName);

            dtGroups.Columns.Add("__groupKey", typeof(string));
            dtRegs.Columns.Add("__groupKey", typeof(string));
            dtRegs.Columns.Add("__regKey", typeof(string));

            var groupEls = doc.Root?.Elements().Where(x => x != null && x.Name.LocalName.Equals("group", StringComparison.OrdinalIgnoreCase)).ToList()
                          ?? new List<XElement>();

            // --- Groups columns (attributes union) ---
            var groupAttrNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in groupEls)
            {
                foreach (var a in g.Attributes())
                {
                    var n = a.Name.LocalName;
                    if (!string.IsNullOrWhiteSpace(n))
                        groupAttrNames.Add(n);
                }
            }

            foreach (var a in groupAttrNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var col = "a_" + a;
                if (!dtGroups.Columns.Contains(col))
                    dtGroups.Columns.Add(col, typeof(string));
            }

            // --- Regs columns (attributes union) ---
            var regAttrNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var regLeafNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var g in groupEls)
            {
                foreach (var r in g.Elements().Where(x => x != null && x.Name.LocalName.Equals("reg", StringComparison.OrdinalIgnoreCase)))
                {
                    foreach (var a in r.Attributes())
                    {
                        var n = a.Name.LocalName;
                        if (!string.IsNullOrWhiteSpace(n))
                            regAttrNames.Add(n);
                    }

                    foreach (var c in r.Elements())
                    {
                        if (c.HasElements) continue;
                        var n = c.Name.LocalName;
                        if (!string.IsNullOrWhiteSpace(n))
                            regLeafNames.Add(n);
                    }
                }
            }

            foreach (var a in regAttrNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var col = "a_" + a;
                if (!dtRegs.Columns.Contains(col))
                    dtRegs.Columns.Add(col, typeof(string));
            }

            foreach (var n in regLeafNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var col = dtRegs.Columns.Contains(n) ? "c_" + n : n;
                if (!dtRegs.Columns.Contains(col))
                    dtRegs.Columns.Add(col, typeof(string));
            }

            // --- Build stable group keys ---
            var sigCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string GetGroupSignature(XElement g)
            {
                string GetAttr(string an)
                {
                    var a = g.Attribute(an);
                    return a == null ? "" : (a.Value ?? "").Trim();
                }

                // намеренно НЕ включаем name/start/step (они могут отличаться)
                var drv = GetAttr("drv");
                var rtu = GetAttr("rtu");
                var function = GetAttr("function");
                var table = GetAttr("table");
                var type = GetAttr("type");
                return (drv + "|" + rtu + "|" + function + "|" + table + "|" + type).Trim('|');
            }

            var groupKeyByElement = new Dictionary<XElement, string>();

            foreach (var g in groupEls)
            {
                var sig = GetGroupSignature(g);
                if (string.IsNullOrWhiteSpace(sig))
                    sig = "group";

                if (!sigCount.TryGetValue(sig, out var idx)) idx = 0;
                idx++;
                sigCount[sig] = idx;

                var groupKey = sig + "#" + idx.ToString();
                groupKeyByElement[g] = groupKey;

                var row = dtGroups.NewRow();
                row["__groupKey"] = groupKey;

                foreach (var a in g.Attributes())
                {
                    var col = "a_" + a.Name.LocalName;
                    if (dtGroups.Columns.Contains(col))
                        row[col] = a.Value ?? "";
                }

                dtGroups.Rows.Add(row);
            }

            // --- Fill regs rows ---
            foreach (var g in groupEls)
            {
                var groupKey = groupKeyByElement.TryGetValue(g, out var k) ? k : "group#1";
                int regOrdinal = 0;

                foreach (var r in g.Elements().Where(x => x != null && x.Name.LocalName.Equals("reg", StringComparison.OrdinalIgnoreCase)))
                {
                    regOrdinal++;

                    string regKey = "";
                    var an = r.Attribute("name");
                    if (an != null)
                        regKey = (an.Value ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(regKey))
                    {
                        var ai = r.Attribute("index");
                        if (ai != null)
                            regKey = "index:" + (ai.Value ?? "").Trim();
                    }
                    if (string.IsNullOrWhiteSpace(regKey))
                        regKey = "reg#" + regOrdinal.ToString();

                    var row = dtRegs.NewRow();
                    row["__groupKey"] = groupKey;
                    row["__regKey"] = regKey;

                    foreach (var a in r.Attributes())
                    {
                        var col = "a_" + a.Name.LocalName;
                        if (dtRegs.Columns.Contains(col))
                            row[col] = a.Value ?? "";
                    }

                    foreach (var c in r.Elements())
                    {
                        if (c.HasElements) continue;
                        var baseName = c.Name.LocalName;
                        var col = dtRegs.Columns.Contains(baseName) ? baseName : (dtRegs.Columns.Contains("c_" + baseName) ? "c_" + baseName : null);
                        if (col != null)
                            row[col] = c.Value ?? "";
                    }

                    dtRegs.Rows.Add(row);
                }
            }

            return new GroupsRegsTables { Groups = dtGroups, Regs = dtRegs };
        }

        private static void CachePk(string path, string tableName, string[] pk)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || pk == null || pk.Length == 0)
                    return;

                var lw = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
                var key = BuildPkCacheKey(path, tableName);

                lock (_pkLock)
                {
                    _pkCache[key] = new PkCacheEntry
                    {
                        LastWriteUtc = lw,
                        PkColumns = pk
                    };
                }
            }
            catch
            {
                // ignore
            }
        }

        private static string BuildPkCacheKey(string path, string tableName)
        {
            return (path ?? "") + "||" + (tableName ?? "");
        }
    }
}
