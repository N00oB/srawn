using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    /// <summary>
    /// Провайдер для XML-конфигураций Тюрино (*.cfg).
    /// 
    /// Важно: эти *.cfg — это XML (не INI).
    /// Мы представляем их как набор "логических таблиц" для удобного diff.
    /// </summary>
    public sealed class CfgDatabaseProvider : IDatabaseProvider, IBatchDatabaseProvider, IFastRowHashProvider
    {
        private readonly object _sync = new object();

        // Кэш живёт ТОЛЬКО в рамках BeginBatch/EndBatch.
        private readonly Dictionary<string, ParsedCfg> _batchCache =
            new Dictionary<string, ParsedCfg>(StringComparer.OrdinalIgnoreCase);

        public List<string> GetTableNames(string connectionString)
        {
            // Список фиксированный (какие-то таблицы могут быть пустыми — это нормально).
            return new List<string>
            {
                "ProjectParams_общее",
                "Box_шкафы",
                "Crate_корзины",
                "Net_сети",
                "Device_модули",
                "signals_сигналы",
            };
        }

        public DataTable LoadTable(string connectionString, string tableName)
        {
            var parsed = TryGetFromBatch(connectionString);
            if (parsed == null)
            {
                // Вне batch читаем файл заново (кэш не держим намеренно).
                parsed = Parse(connectionString);
            }

            if (!parsed.Tables.TryGetValue(tableName, out var table) || table == null)
            {
                // Возвращаем пустую таблицу с именем — чтобы UI не падал.
                return new DataTable(tableName);
            }

            return table;
        }

        public string[] GetPrimaryKeyColumns(string connectionString, string tableName)
        {
            // Ключи должны существовать в схеме ВСЕГДА.
            switch (tableName)
            {
                case "ProjectParams_общее":
                    return new[] { "Key" };

                case "Box_шкафы":
                    return new[] { "boxIndex" };

                case "Crate_корзины":
                    // crateKey = логическая группа (без префикса объекта и без "- A<N>")
                    // crateOrdinal = порядковый номер в группе (стабильный мэппинг A2<->A4 как "обновить")
                    return new[] { "crateKey", "crateClass", "type", "crateOrdinal" };

                case "Net_сети":
                    return new[] { "netKey" };

                case "Device_модули":
                    // deviceKey — синтетический ключ для сопоставления между разными объектами
                    // (например, МНС.КЦ - A1.1 <-> ПТ.КЦ - A1.1). В UI его скрываем.
                    return new[] { "deviceKey" };

                case "signals_сигналы":
                    // В *.cfg signalName часто содержит ГЛОБАЛЬНЫЙ индекс (например AIn[42]),
                    // который может отличаться между объектами при одинаковой структуре устройства.
                    // Поэтому для корректного сопоставления используем стабильный индекс элемента (<_0>, <_1>, ...).
                    return new[] { "deviceKey", "signalIndex" };
            }

            return null;
        }

        public void ApplyRowChanges(
            string targetConnectionString,
            string tableName,
            string[] primaryKeyColumns,
            IEnumerable<RowPair> pairsToApply)
        {
            throw new NotSupportedException(
                "Провайдер CFG пока поддерживает только сравнение (чтение). Применение изменений не реализовано.");
        }

        public void ReplaceTable(string connectionString, string tableName, DataTable dataTable)
        {
            throw new NotSupportedException(
                "Провайдер CFG пока поддерживает только сравнение (чтение). Замена таблиц не реализована.");
        }

        public void DropTable(string connectionString, string tableName)
        {
            throw new NotSupportedException(
                "Провайдер CFG пока поддерживает только сравнение (чтение). Удаление таблиц не реализовано.");
        }

        // ---------- Batch cache ----------

        public void BeginBatch(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return;

            lock (_sync)
            {
                if (_batchCache.ContainsKey(connectionString))
                    return;
            }

            var sw = Stopwatch.StartNew();
            var parsed = Parse(connectionString);
            sw.Stop();

            lock (_sync)
            {
                // Могли проиграть гонку, но не страшно.
                _batchCache[connectionString] = parsed;
            }

            var path = ExtractFilePath(connectionString);
            long size = 0;
            try { size = new FileInfo(path).Length; } catch { /* ignore */ }
            AppLogger.Info($"CFG: разобран файл и создан кэш. Файл '{path}', размер={size} байт, время={sw.ElapsedMilliseconds} мс.");
        }

        public void EndBatch(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return;

            lock (_sync)
            {
                _batchCache.Remove(connectionString);
            }
        }

        // ---------- Fast hash ----------

        public ColumnInfo[] GetTableColumns(string connectionString, string tableName)
        {
            var parsed = TryGetFromBatch(connectionString) ?? Parse(connectionString);

            if (!parsed.Tables.TryGetValue(tableName, out var table) || table == null)
                return Array.Empty<ColumnInfo>();

            var cols = new ColumnInfo[table.Columns.Count];
            for (int i = 0; i < table.Columns.Count; i++)
            {
                var c = table.Columns[i];
                cols[i] = new ColumnInfo(c.ColumnName, c.DataType);
            }
            return cols;
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

            // Маппинг имя->ординал в МОДЕЛИ (tableColumns)
            var nameToOrdinal = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < tableColumns.Length; i++)
                nameToOrdinal[tableColumns[i].Name] = i;

            var keyOrdinals = keyColumns
                .Select(k => nameToOrdinal.TryGetValue(k, out var ord)
                    ? ord
                    : throw new InvalidOperationException($"Колонка ключа '{k}' не найдена в схеме таблицы '{tableName}'."))
                .ToArray();

            var hashOrdinals = Enumerable.Range(0, tableColumns.Length).ToArray();
            var expectedTypes = tableColumns.Select(c => c.DataType).ToArray();

            var parsed = TryGetFromBatch(connectionString) ?? Parse(connectionString);

            if (!parsed.Tables.TryGetValue(tableName, out var table) || table == null)
                return new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);

            // Маппинг имя->ординал в реальной DataTable (может не содержать всех колонок из tableColumns)
            var actualOrdinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < table.Columns.Count; i++)
                actualOrdinals[table.Columns[i].ColumnName] = i;

            var map = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
            var record = new ArrayDataRecord(tableColumns.Length);

            var rowNumber = 0;
            foreach (DataRow row in table.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;

                for (int i = 0; i < tableColumns.Length; i++)
                {
                    var colName = tableColumns[i].Name;
                    if (actualOrdinals.TryGetValue(colName, out var ord))
                    {
                        record.Values[i] = NormalizeToStringOrNull(row[ord]);
                    }
                    else
                    {
                        record.Values[i] = null;
                    }
                }

                var key = RowHashing.BuildKey(record, keyOrdinals);
                var hash = RowHashing.ComputeRowHash(record, hashOrdinals, expectedTypes);
                map[key] = hash;
            }

            return map;
        }

        // ---------- Parsing ----------

        private ParsedCfg TryGetFromBatch(string connectionString)
        {
            lock (_sync)
            {
                return _batchCache.TryGetValue(connectionString, out var p) ? p : null;
            }
        }

        private static string ExtractFilePath(string connectionString)
        {
            // Формат: CfgXmlFile=...;
            const string prefix = "CfgXmlFile=";
            if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Неверная строка подключения для CFG.");

            var cs = connectionString.Substring(prefix.Length);
            if (cs.EndsWith(";"))
                cs = cs.Substring(0, cs.Length - 1);

            return cs.Trim();
        }

        private static object NormalizeToStringOrNull(object value)
        {
            if (value == null || value == DBNull.Value) return null;
            return value.ToString();
        }

        private static ParsedCfg Parse(string connectionString)
        {
            var path = ExtractFilePath(connectionString);

            if (!File.Exists(path))
                throw new FileNotFoundException("Файл не найден.", path);

            XDocument doc;
            try
            {
                doc = XDocument.Load(path, LoadOptions.None);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"CFG: не удалось прочитать XML из файла '{path}'.", ex);
            }

            var root = doc.Root;
            if (root == null)
                throw new InvalidOperationException($"CFG: пустой XML в файле '{path}'.");

            // Встречаются варианты, когда в одном файле присутствуют оба узла:
            // <Configuration> и <ConfigurationProject>. При этом структуры могут отличаться,
            // а часть данных (например, <signals>) бывает только в одном из них.
            // Поэтому выбираем узел, который реально содержит нужные секции.
            XElement cfg;

            if (root.Name.LocalName == "Configuration" || root.Name.LocalName == "ConfigurationProject")
            {
                cfg = root;
            }
            else
            {
                var cfgA = root.Element("Configuration");
                var cfgB = root.Element("ConfigurationProject");

                // Предпочитаем тот, где есть signals (если присутствуют оба).
                if (cfgA != null && cfgA.Descendants("signals").Any())
                    cfg = cfgA;
                else if (cfgB != null && cfgB.Descendants("signals").Any())
                    cfg = cfgB;
                else
                    cfg = cfgA ?? cfgB;
            }

            if (cfg == null)
                throw new InvalidOperationException(
                    $"CFG: не найден узел <Configuration> (или <ConfigurationProject>) в файле '{path}'.");

            try
            {
                var selected = cfg.Name.LocalName;
                var signalsCount = cfg.Descendants("signals").Elements().Count();
                AppLogger.Info($"CFG: выбран узел <{selected}>. signals={signalsCount}.");
            }
            catch
            {
                // ignore
            }

            var tables = new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);

            tables["ProjectParams_общее"] = BuildProjectParamsTable(root, cfg);
            tables["Box_шкафы"] = BuildBoxesTable(cfg);
            tables["Crate_корзины"] = BuildCratesTable(cfg);

            // Nets/Devices/Signals завязаны на обход дерева.
            var netsRows = new List<Dictionary<string, string>>();
            var devicesRows = new List<Dictionary<string, string>>();
            var signalsRows = new List<Dictionary<string, string>>();

            var netKeyUniq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var net in cfg.Elements("Net"))
            {
                TraverseNet(net, parentNetKey: null, parentDeviceName: null,
                    netsRows: netsRows,
                    devicesRows: devicesRows,
                    signalsRows: signalsRows,
                    netKeyUniq: netKeyUniq);
            }

            // Стабилизируем ключи сетей: убираем префиксы объекта и адресные суффиксы, чтобы чаще попадало в "Обновить".
            RebuildStableNetKeys(netsRows, devicesRows);

            // Стабилизируем ключи устройств: сопоставляем A4.* <-> A2.* как "обновить" (через порядковый номер стойки внутри группы).
            RebuildStableDeviceKeys(devicesRows, signalsRows);

            tables["Net_сети"] = BuildTableFromRows("Net_сети", netsRows);
            tables["Device_модули"] = BuildTableFromRows("Device_модули", devicesRows);
            tables["signals_сигналы"] = BuildTableFromRows("signals_сигналы", signalsRows);

            // Служебные колонки (ключи/связи), которые нужны для сопоставления, но обычно не интересны в UI.
            HideInGrid(tables["Net_сети"], "netKey");
            HideInGrid(tables["Net_сети"], "parentNetKey");
            HideInGrid(tables["Net_сети"], "parentDeviceName");

            HideInGrid(tables["Device_модули"], "netKey");
            HideInGrid(tables["Device_модули"], "parentDeviceName");
            HideInGrid(tables["Device_модули"], "parentNetKey");
            HideInGrid(tables["Device_модули"], "deviceKey");

            HideInGrid(tables["signals_сигналы"], "deviceKey");
            HideInGrid(tables["signals_сигналы"], "signalIndex");

            EnsureMandatoryColumns(tables);
            return new ParsedCfg(tables);
        }

        private static void EnsureMandatoryColumns(Dictionary<string, DataTable> tables)
        {
            // Гарантируем наличие PK-колонок, иначе ResolveKeyColumns вернёт PK и diff упадёт.
            EnsureColumns(tables, "ProjectParams_общее", new[] { "Key", "Value" });
            EnsureColumns(tables, "Box_шкафы", new[] { "boxIndex", "name", "type" });
            EnsureColumns(tables, "Crate_корзины", new[] { "boxName", "crateName", "crateKey", "crateClass", "type", "crateOrdinal" });
            EnsureColumns(tables, "Net_сети", new[] { "netKey" });
            EnsureColumns(tables, "Device_модули", new[] { "deviceKey", "deviceName" });
            EnsureColumns(tables, "signals_сигналы", new[] { "deviceKey", "deviceName", "signalName", "signalIndex" });
        }

        private static void EnsureColumns(Dictionary<string, DataTable> tables, string tableName, string[] columns)
        {
            if (!tables.TryGetValue(tableName, out var t) || t == null)
            {
                t = new DataTable(tableName);
                tables[tableName] = t;
            }

            foreach (var c in columns)
            {
                if (!t.Columns.Contains(c))
                    t.Columns.Add(c, typeof(string));
            }
        }

        private static DataTable BuildProjectParamsTable(XElement root, XElement cfg)
        {
            var dt = new DataTable("ProjectParams_общее");
            dt.Columns.Add("Key", typeof(string));
            dt.Columns.Add("Value", typeof(string));

            // version атрибут на <ConfiguratorOutput>
            var verAttr = root.Attribute("version")?.Value;
            if (!string.IsNullOrWhiteSpace(verAttr))
                dt.Rows.Add("file_version", verAttr);

            var pp = cfg.Element("ProjectParams");
            if (pp == null) return dt;

            foreach (var el in pp.Elements())
            {
                // Берём только простые скаляры.
                if (el.HasElements) continue;
                var key = el.Name.LocalName;
                var value = (el.Value ?? "").Trim();
                dt.Rows.Add(key, value);
            }

            return dt;
        }

        private static DataTable BuildBoxesTable(XElement cfg)
        {
            var rows = new List<Dictionary<string, string>>();

            foreach (var box in cfg.Elements("Box"))
            {
                var pc = box.Element("ParamsCommon");
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (pc != null)
                    AddScalarChildren(dict, pc);
                rows.Add(dict);
            }

            var dt = BuildTableFromRows("Box_шкафы", rows);

            // Переименуем для единообразия PK (boxIndex) и обязательные колонки.
            // В XML имя поля уже "boxIndex", поэтому тут просто гарантируем наличие.
            if (!dt.Columns.Contains("boxIndex")) dt.Columns.Add("boxIndex", typeof(string));
            if (!dt.Columns.Contains("name")) dt.Columns.Add("name", typeof(string));
            if (!dt.Columns.Contains("type")) dt.Columns.Add("type", typeof(string));

            return dt;
        }

        private static DataTable BuildCratesTable(XElement cfg)
        {
            var rows = new List<Dictionary<string, string>>();

            // Для стабильного сопоставления ("A2" <-> "A4" как обновление) используем:
            //  - crateKey: базовое имя без префикса объекта и без суффикса "- A<N>"
            //  - crateOrdinal: порядковый номер внутри группы (crateKey + crateClass + type)
            // Это позволяет видеть переименования/перенумерации как "Отличается (обновить)",
            // а не как "удалить/добавить".
            var indexItems = new List<CrateIndexItem>();

            foreach (var box in cfg.Elements("Box"))
            {
                var boxName = box.Element("ParamsCommon")?.Element("name")?.Value ?? "";

                var crates = box.Element("Crates")?.Elements("Crate");
                if (crates == null) continue;

                foreach (var crate in crates)
                {
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    dict["boxName"] = boxName;

                    var pc = crate.Element("ParamsCommon");
                    if (pc != null)
                        AddScalarChildren(dict, pc);

                    if (dict.TryGetValue("name", out var crateName))
                    {
                        dict["crateName"] = crateName;
                        dict.Remove("name");

                        var localName = StripObjectPrefix(crateName); // например "КЦ - A4"
                        var baseKey = StripCrateSlotSuffix(localName); // например "КЦ"
                        dict["crateKey"] = baseKey;
                    }

                    // crateOrdinal заполним после группировки.
                    if (!dict.ContainsKey("crateOrdinal"))
                        dict["crateOrdinal"] = "";

                    rows.Add(dict);

                    // Данные для сортировки/нумерации внутри группы.
                    var key = dict.TryGetValue("crateKey", out var ck) ? (ck ?? "") : "";
                    var crateClass = dict.TryGetValue("crateClass", out var cc) ? (cc ?? "") : "";
                    var type = dict.TryGetValue("type", out var tp) ? (tp ?? "") : "";
                    var localForSort = dict.TryGetValue("crateName", out var cn) ? StripObjectPrefix(cn ?? "") : "";

                    indexItems.Add(new CrateIndexItem(dict, key, crateClass, type, localForSort));
                }
            }

            // Нумеруем внутри каждой группы (crateKey + crateClass + type)
            foreach (var grp in indexItems
                         .GroupBy(x => x.GroupKey, StringComparer.OrdinalIgnoreCase))
            {
                int ordinal = 1;
                foreach (var item in grp
                             .OrderBy(x => x.SortSlot)
                             .ThenBy(x => x.SortName, StringComparer.OrdinalIgnoreCase))
                {
                    item.Row["crateOrdinal"] = ordinal.ToString(CultureInfo.InvariantCulture);
                    ordinal++;
                }
            }

            var dt = BuildTableFromRows("Crate_корзины", rows);
            if (!dt.Columns.Contains("boxName")) dt.Columns.Add("boxName", typeof(string));
            if (!dt.Columns.Contains("crateName")) dt.Columns.Add("crateName", typeof(string));
            if (!dt.Columns.Contains("crateKey")) dt.Columns.Add("crateKey", typeof(string));
            if (!dt.Columns.Contains("crateClass")) dt.Columns.Add("crateClass", typeof(string));
            if (!dt.Columns.Contains("type")) dt.Columns.Add("type", typeof(string));
            if (!dt.Columns.Contains("crateOrdinal")) dt.Columns.Add("crateOrdinal", typeof(string));

            // Служебные (синтетические) колонки скрываем в гриде, но оставляем в данных и PK.
            HideInGrid(dt, "crateKey");
            HideInGrid(dt, "crateOrdinal");

            return dt;
        }

        private sealed class CrateIndexItem
        {
            public readonly Dictionary<string, string> Row;
            public readonly string CrateKey;
            public readonly string CrateClass;
            public readonly string Type;
            public readonly string SortName;
            public readonly int SortSlot;

            public CrateIndexItem(Dictionary<string, string> row, string crateKey, string crateClass, string type, string sortName)
            {
                Row = row;
                CrateKey = crateKey ?? "";
                CrateClass = crateClass ?? "";
                Type = type ?? "";
                SortName = sortName ?? "";
                SortSlot = TryParseCrateSlot(sortName, out var n) ? n : int.MaxValue;
            }

            public string GroupKey => $"{CrateKey}|{CrateClass}|{Type}";
        }

        
        private sealed class NetIndexItem
        {
            public readonly Dictionary<string, string> Row;
            public readonly string OldKey;
            public readonly string ParentOldKey;
            public string NewKey;

            public readonly string Type;
            public readonly string Name;
            public readonly string ParentDeviceName;

            public readonly string BaseLabel;
            public readonly string SortName;
            public readonly int SortAddrMajor;
            public readonly int SortAddrMinor;
            public readonly int SortChannel;

            public NetIndexItem(
                Dictionary<string, string> row,
                string oldKey,
                string parentOldKey,
                string type,
                string name,
                string parentDeviceName)
            {
                Row = row;
                OldKey = oldKey ?? "";
                ParentOldKey = parentOldKey ?? "";
                Type = (type ?? "").Trim();
                Name = (name ?? "").Trim();
                ParentDeviceName = (parentDeviceName ?? "").Trim();

                SortName = NormalizeNameForKey(Name);
                if (string.IsNullOrWhiteSpace(SortName))
                    SortName = NormalizeNameForKey(Type);

                BaseLabel = ComputeNetBaseLabel(Type, Name, ParentDeviceName);

                if (!TryParseAddressA(Name, out var maj, out var min) &&
                    !TryParseAddressA(ParentDeviceName, out maj, out min))
                {
                    maj = int.MaxValue;
                    min = int.MaxValue;
                }

                SortAddrMajor = maj;
                SortAddrMinor = min;
                SortChannel = TryParseChannel(Name, out var ch) ? ch : int.MaxValue;
            }
        }

        private static void RebuildStableNetKeys(
            List<Dictionary<string, string>> netsRows,
            List<Dictionary<string, string>> devicesRows)
        {
            if (netsRows == null || netsRows.Count == 0)
                return;

            // Собираем набор всех старых ключей.
            var oldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in netsRows)
            {
                if (r == null) continue;
                if (r.TryGetValue("netKey", out var k) && !string.IsNullOrWhiteSpace(k))
                    oldKeys.Add(k.Trim());
            }

            // Строим дерево: parentNetKey -> children
            var children = new Dictionary<string, List<NetIndexItem>>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in netsRows)
            {
                if (r == null) continue;

                var oldKey = GetOrEmpty(r, "netKey");
                if (string.IsNullOrWhiteSpace(oldKey))
                    continue;

                var parentOld = GetOrEmpty(r, "parentNetKey");
                if (string.IsNullOrWhiteSpace(parentOld) || !oldKeys.Contains(parentOld))
                    parentOld = "";

                var type = GetOrEmpty(r, "type");
                var name = GetOrEmpty(r, "name");
                var parentDeviceName = GetOrEmpty(r, "parentDeviceName");

                var item = new NetIndexItem(r, oldKey, parentOld, type, name, parentDeviceName);

                if (!children.TryGetValue(parentOld, out var list))
                {
                    list = new List<NetIndexItem>();
                    children[parentOld] = list;
                }

                list.Add(item);
            }

            // Назначаем новые ключи рекурсивно.
            var newKeyByOld = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AssignNetKeysRecursive(parentOldKey: "", parentNewKey: "", children: children, newKeyByOld: newKeyByOld);

            // Проставляем новые ключи в Net_сети.
            foreach (var r in netsRows)
            {
                if (r == null) continue;

                var oldKey = GetOrEmpty(r, "netKey");
                if (string.IsNullOrWhiteSpace(oldKey))
                    continue;

                if (newKeyByOld.TryGetValue(oldKey, out var newKey))
                    r["netKey"] = newKey;

                var parentOld = GetOrEmpty(r, "parentNetKey");
                if (!string.IsNullOrWhiteSpace(parentOld) && newKeyByOld.TryGetValue(parentOld, out var parentNew))
                    r["parentNetKey"] = parentNew;
                else
                    r["parentNetKey"] = "";
            }

            // Обновляем ссылки из Device_модули на parentNetKey / netKey (если такие колонки присутствуют).
            if (devicesRows != null)
            {
                foreach (var r in devicesRows)
                {
                    if (r == null) continue;

                    if (r.TryGetValue("parentNetKey", out var p) &&
                        !string.IsNullOrWhiteSpace(p) &&
                        newKeyByOld.TryGetValue(p.Trim(), out var pn))
                        r["parentNetKey"] = pn;

                    if (r.TryGetValue("netKey", out var nk) &&
                        !string.IsNullOrWhiteSpace(nk) &&
                        newKeyByOld.TryGetValue(nk.Trim(), out var nn))
                        r["netKey"] = nn;
                }
            }
        }

        // ---------- Device key stabilization (Device_модули / signals_сигналы) ----------

        private static readonly Regex _reDeviceAddr = new Regex(
            @"^(?<group>.+?)\s*[-–—]\s*[AА](?<rack>\d+)\.(?<slot>\d+)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static void RebuildStableDeviceKeys(
            List<Dictionary<string, string>> devicesRows,
            List<Dictionary<string, string>> signalsRows)
        {
            if (devicesRows == null || devicesRows.Count == 0)
                return;

            // Группируем стойки (A1/A2/A4/...) внутри каждой логической группы ("КЦ", "УСО.1(1)" и т.д.).
            // Затем заменяем номер стойки на порядковый номер (1..N) — это даёт стабильное сопоставление
            // между разными объектами: например МНС.КЦ - A4.3 <-> ПТ.КЦ - A2.3.
            var groupToRacks = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            int parsedWithAddr = 0;
            foreach (var r in devicesRows)
            {
                if (r == null) continue;

                var deviceName = GetOrEmpty(r, "deviceName");
                if (string.IsNullOrWhiteSpace(deviceName))
                    deviceName = GetOrEmpty(r, "name");

                var local = StripObjectPrefix(deviceName);

                if (TryParseDeviceAddress(local, out var group, out var rack, out _))
                {
                    parsedWithAddr++;

                    if (!groupToRacks.TryGetValue(group, out var set))
                    {
                        set = new HashSet<int>();
                        groupToRacks[group] = set;
                    }

                    set.Add(rack);
                }
            }

            // Строим мэп: group -> rack -> ordinal (1..N).
            var rackOrdinalByGroup = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in groupToRacks)
            {
                var group = kv.Key;
                var racks = kv.Value;
                if (racks == null || racks.Count == 0)
                    continue;

                var sorted = racks.OrderBy(x => x).ToArray();
                var map = new Dictionary<int, int>();
                for (int i = 0; i < sorted.Length; i++)
                    map[sorted[i]] = i + 1;

                rackOrdinalByGroup[group] = map;
            }

            int changedDevices = 0;
            foreach (var r in devicesRows)
            {
                if (r == null) continue;

                var deviceName = GetOrEmpty(r, "deviceName");
                if (string.IsNullOrWhiteSpace(deviceName))
                    deviceName = GetOrEmpty(r, "name");

                var newKey = BuildStableDeviceKey(deviceName, rackOrdinalByGroup);

                if (!string.Equals(GetOrEmpty(r, "deviceKey"), newKey, StringComparison.OrdinalIgnoreCase))
                {
                    r["deviceKey"] = newKey;
                    changedDevices++;
                }
                else
                {
                    // всё равно проставим, чтобы гарантировать наличие
                    r["deviceKey"] = newKey;
                }
            }

            int changedSignals = 0;
            if (signalsRows != null && signalsRows.Count > 0)
            {
                foreach (var s in signalsRows)
                {
                    if (s == null) continue;

                    var deviceName = GetOrEmpty(s, "deviceName");
                    var newKey = BuildStableDeviceKey(deviceName, rackOrdinalByGroup);

                    if (!string.Equals(GetOrEmpty(s, "deviceKey"), newKey, StringComparison.OrdinalIgnoreCase))
                    {
                        s["deviceKey"] = newKey;
                        changedSignals++;
                    }
                    else
                    {
                        s["deviceKey"] = newKey;
                    }
                }
            }

            try
            {
                AppLogger.Info(
                    $"CFG: нормализация ключей устройств завершена. Устройств={devicesRows.Count}, с адресом={parsedWithAddr}, групп={groupToRacks.Count}, изменено ключей устройств={changedDevices}, сигналов={changedSignals}.");
            }
            catch
            {
                // ignore
            }
        }

        private static string BuildStableDeviceKey(
            string deviceName,
            Dictionary<string, Dictionary<int, int>> rackOrdinalByGroup)
        {
            var local = StripObjectPrefix(deviceName ?? "");
            local = NormalizeSpaces(local);

            if (TryParseDeviceAddress(local, out var group, out var rack, out var slot))
            {
                int rackOrd = rack;

                if (rackOrdinalByGroup != null &&
                    rackOrdinalByGroup.TryGetValue(group, out var map) &&
                    map != null &&
                    map.TryGetValue(rack, out var ord))
                {
                    rackOrd = ord;
                }

                return $"{group}|R{rackOrd.ToString(CultureInfo.InvariantCulture)}|S{slot.ToString(CultureInfo.InvariantCulture)}";
            }

            if (string.IsNullOrWhiteSpace(local))
                return "(без имени)";

            return local;
        }

        private static bool TryParseDeviceAddress(string localName, out string group, out int rack, out int slot)
        {
            group = "";
            rack = 0;
            slot = 0;

            if (string.IsNullOrWhiteSpace(localName))
                return false;

            var m = _reDeviceAddr.Match(localName.Trim());
            if (!m.Success)
                return false;

            group = NormalizeSpaces(m.Groups["group"].Value).Trim();
            if (string.IsNullOrWhiteSpace(group))
                group = "(без имени)";

            if (!int.TryParse(m.Groups["rack"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out rack))
                return false;

            if (!int.TryParse(m.Groups["slot"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot))
                return false;

            return true;
        }


        private static void AssignNetKeysRecursive(
            string parentOldKey,
            string parentNewKey,
            Dictionary<string, List<NetIndexItem>> children,
            Dictionary<string, string> newKeyByOld)
        {
            parentOldKey = parentOldKey ?? "";
            parentNewKey = parentNewKey ?? "";

            if (!children.TryGetValue(parentOldKey, out var list) || list == null || list.Count == 0)
                return;

            foreach (var grp in list
                         .GroupBy(x => x.BaseLabel, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                int ordinal = 1;

                foreach (var item in grp
                             .OrderBy(x => x.SortAddrMajor)
                             .ThenBy(x => x.SortAddrMinor)
                             .ThenBy(x => x.SortChannel)
                             .ThenBy(x => x.SortName, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(x => x.OldKey, StringComparer.OrdinalIgnoreCase))
                {
                    var key = grp.Key;

                    var newKey = string.IsNullOrEmpty(parentNewKey)
                        ? $"{key}#{ordinal.ToString(CultureInfo.InvariantCulture)}"
                        : $"{parentNewKey}|{key}#{ordinal.ToString(CultureInfo.InvariantCulture)}";

                    newKeyByOld[item.OldKey] = newKey;
                    item.NewKey = newKey;

                    ordinal++;
                }
            }

            // Вглубь — после назначения ключей текущему уровню.
            foreach (var item in list
                         .OrderBy(x => newKeyByOld.TryGetValue(x.OldKey, out var nk) ? nk : x.OldKey,
                             StringComparer.OrdinalIgnoreCase))
            {
                if (!newKeyByOld.TryGetValue(item.OldKey, out var childNew))
                    continue;

                AssignNetKeysRecursive(item.OldKey, childNew, children, newKeyByOld);
            }
        }

        private static string ComputeNetBaseLabel(string type, string name, string parentDeviceName)
        {
            var t = (type ?? "").Trim();
            if (string.IsNullOrWhiteSpace(t))
                t = "(без типа)";

            var normalized = NormalizeNameForKey(name ?? "");

            if (string.IsNullOrWhiteSpace(normalized))
                normalized = NormalizeNameForKey(t);

            // В отличие от crate, сети различаем по типу, даже если имена совпали.
            return $"{t}|{normalized}";
        }

        private static string NormalizeNameForKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            var s = NormalizeSpaces(value);

            // Если есть двоеточие (например "Modbus Master: ..."), вырезаем префикс объекта
            // только из первого dotted-сегмента после двоеточия.
            int colon = s.IndexOf(':');
            if (colon >= 0 && colon < s.Length - 1)
            {
                var head = s.Substring(0, colon + 1).TrimEnd();
                var tail = s.Substring(colon + 1).TrimStart();

                if (tail.Length > 0)
                {
                    int cut = 0;
                    while (cut < tail.Length && tail[cut] != ' ' && tail[cut] != '-')
                        cut++;

                    var first = cut > 0 ? tail.Substring(0, cut) : tail;
                    var rest = cut > 0 ? tail.Substring(cut) : "";

                    if (first.Contains("."))
                        first = StripObjectPrefix(first);

                    tail = first + rest;
                }

                s = head + " " + tail.Trim();
            }
            else
            {
                // Общий случай: если строка начинается с "МНС.КЦ ..." -> "КЦ ..."
                var trimmed = s.Trim();
                int cut = 0;
                while (cut < trimmed.Length && trimmed[cut] != ' ' && trimmed[cut] != '-')
                    cut++;

                var first = cut > 0 ? trimmed.Substring(0, cut) : trimmed;
                var rest = cut > 0 ? trimmed.Substring(cut) : "";

                if (first.Contains("."))
                    first = StripObjectPrefix(first);

                s = (first + rest).Trim();
            }

            // Убираем адресные сегменты вида "- A4.2" — это должно идти как обновление, а не как add/del.
            s = StripAddressSegments(s);

            return NormalizeSpaces(s).Trim();
        }

        private static string StripAddressSegments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            // Вырезаем все повторы " - A<число>[.<число>]"
            return Regex.Replace(
                value,
                @"\s*-\s*A\d+(?:\.\d+)?",
                "",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }

        private static string NormalizeSpaces(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var sb = new System.Text.StringBuilder(value.Length);
            bool ws = false;

            foreach (var ch in value)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!ws)
                    {
                        sb.Append(' ');
                        ws = true;
                    }
                }
                else
                {
                    sb.Append(ch);
                    ws = false;
                }
            }

            return sb.ToString().Trim();
        }

        private static bool TryParseAddressA(string value, out int major, out int minor)
        {
            major = 0;
            minor = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var m = Regex.Match(
                value,
                @"\bA(?<maj>\d+)(?:\.(?<min>\d+))?\b",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            if (!m.Success)
                return false;

            if (!int.TryParse(m.Groups["maj"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out major))
                return false;

            if (m.Groups["min"].Success)
                int.TryParse(m.Groups["min"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out minor);

            return true;
        }

        private static bool TryParseChannel(string value, out int channel)
        {
            channel = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            var m = Regex.Match(
                value,
                @"\b(?:Канал|Channel)\s*(?<n>\d+)\b",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            if (!m.Success)
                return false;

            return int.TryParse(m.Groups["n"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out channel);
        }

        private static string GetOrEmpty(Dictionary<string, string> row, string key)
        {
            if (row == null) return "";
            if (!row.TryGetValue(key, out var v) || v == null) return "";
            return v.Trim();
        }

private static void TraverseNet(
            XElement net,
            string parentNetKey,
            string parentDeviceName,
            List<Dictionary<string, string>> netsRows,
            List<Dictionary<string, string>> devicesRows,
            List<Dictionary<string, string>> signalsRows,
            Dictionary<string, int> netKeyUniq)
        {
            if (net == null) return;

            var pc = net.Element("ParamsCommon");
            var ps = net.Element("ParamsSpecific");

            var netName = pc?.Element("name")?.Value ?? "";
            var baseKey = string.IsNullOrWhiteSpace(parentNetKey) ? netName : (parentNetKey + "|" + netName);
            if (string.IsNullOrWhiteSpace(baseKey)) baseKey = "(без имени)";

            // Уникализация ключа внутри файла.
            var netKey = MakeUniqueKey(baseKey, netKeyUniq);

            var netRow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            netRow["netKey"] = netKey;
            netRow["parentNetKey"] = parentNetKey ?? "";
            netRow["parentDeviceName"] = parentDeviceName ?? "";

            if (pc != null) AddScalarChildren(netRow, pc);
            if (ps != null) AddScalarChildren(netRow, ps, prefix: "ps_");
            netsRows.Add(netRow);

            // Devices внутри net
            var devices = net.Element("Devices")?.Elements("Device");
            if (devices != null)
            {
                foreach (var dev in devices)
                {
                    TraverseDevice(dev, netKey, netsRows, devicesRows, signalsRows, netKeyUniq);
                }
            }
        }

        private static void TraverseDevice(
            XElement device,
            string parentNetKey,
            List<Dictionary<string, string>> netsRows,
            List<Dictionary<string, string>> devicesRows,
            List<Dictionary<string, string>> signalsRows,
            Dictionary<string, int> netKeyUniq)
        {
            var pc = device.Element("ParamsCommon");
            var ps = device.Element("ParamsSpecific");

            var deviceName = pc?.Element("name")?.Value ?? "";
            if (string.IsNullOrWhiteSpace(deviceName))
                deviceName = "(без имени)";

            // Нормализованный ключ для сопоставления между разными объектами
            // (например, "МНС.КЦ - A1.1" <-> "ПТ.КЦ - A1.1").
            var deviceKey = StripObjectPrefix(deviceName);
            if (string.IsNullOrWhiteSpace(deviceKey))
                deviceKey = "(без имени)";

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            row["deviceKey"] = deviceKey;
            row["deviceName"] = deviceName;
            row["parentNetKey"] = parentNetKey ?? "";

            // Адресация (box/crate/slotNum)
            var addr = device.Element("Addressing");
            if (addr != null)
            {
                row["box"] = (addr.Element("box")?.Value ?? "").Trim();
                row["crate"] = (addr.Element("crate")?.Value ?? "").Trim();
                row["slotNum"] = (addr.Element("slotNum")?.Value ?? "").Trim();
            }

            if (pc != null)
            {
                AddScalarChildren(row, pc);
                // Чтобы не дублировать одно и то же двумя колонками (name и deviceName)
                // оставляем только deviceName.
                row.Remove("name");
            }

            // ParamsSpecific: берём скаляры, но исключаем signals (они идут в отдельную таблицу)
            if (ps != null)
            {
                foreach (var el in ps.Elements())
                {
                    var name = el.Name.LocalName;
                    if (string.Equals(name, "signals", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (el.HasElements) continue;
                    row["ps_" + name] = (el.Value ?? "").Trim();
                }
            }

            devicesRows.Add(row);

            // Signals
            var signals = ps?.Element("signals");
            if (signals != null)
            {
                foreach (var sig in signals.Elements())
                {
                    // Сигнал — это элемент вроде <_0 name="AIn[0]">...
                    var sigName = sig.Attribute("name")?.Value ?? sig.Name.LocalName;
                    var sigIndex = sig.Name.LocalName;

                    var srow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["deviceKey"] = deviceKey,
                        ["deviceName"] = deviceName,
                        ["signalName"] = sigName,
                        ["signalIndex"] = sigIndex,
                    };

                    // Внутри бывают Params + InputChannelParams/OutputChannelParams и т.п.
                    var p = sig.Element("Params");
                    if (p != null) AddScalarChildren(srow, p, prefix: "p_");

                    var inp = sig.Element("InputChannelParams");
                    if (inp != null) AddScalarChildren(srow, inp, prefix: "in_");

                    var outp = sig.Element("OutputChannelParams");
                    if (outp != null) AddScalarChildren(srow, outp, prefix: "out_");

                    // На всякий — остальные простые скаляры верхнего уровня сигнала
                    foreach (var el in sig.Elements())
                    {
                        var n = el.Name.LocalName;
                        if (n == "Params" || n == "InputChannelParams" || n == "OutputChannelParams")
                            continue;
                        if (el.HasElements) continue;
                        srow["sig_" + n] = (el.Value ?? "").Trim();
                    }

                    signalsRows.Add(srow);
                }
            }

            // Внутри устройства могут быть вложенные сети
            var nets = device.Element("Nets")?.Elements("Net");
            if (nets != null)
            {
                foreach (var net in nets)
                {
                    TraverseNet(net,
                        parentNetKey: parentNetKey,
                        parentDeviceName: deviceName,
                        netsRows: netsRows,
                        devicesRows: devicesRows,
                        signalsRows: signalsRows,
                        netKeyUniq: netKeyUniq);
                }
            }
        }

        private static string StripObjectPrefix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            var s = value.Trim();
            var idx = s.IndexOf('.');
            if (idx <= 0 || idx >= s.Length - 1)
                return s;

            return s.Substring(idx + 1).Trim();
        }

        /// <summary>
        /// Убирает из строки суффикс вида " - A&lt;число&gt;" (если он есть).
        /// Пример: "КЦ - A4" -> "КЦ".
        /// </summary>
        private static string StripCrateSlotSuffix(string localName)
        {
            if (string.IsNullOrWhiteSpace(localName))
                return localName;

            var s = localName.Trim();

            // Ищем последний маркер "- A" и цифры в конце.
            // Поддержим варианты с пробелами: " - A4", "-A4", "- A 4" (на всякий).
            // Если не похоже — возвращаем как есть.
            int dash = s.LastIndexOf('-');
            if (dash < 0 || dash >= s.Length - 1)
                return s;

            var tail = s.Substring(dash + 1).Trim(); // например "A4"
            if (tail.Length < 2)
                return s;

            if (tail[0] != 'A' && tail[0] != 'a')
                return s;

            var numPart = tail.Substring(1).Trim();
            if (numPart.Length == 0)
                return s;

            if (!int.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                return s;

            // Удаляем "- A<число>" вместе с пробелами слева.
            return s.Substring(0, dash).Trim();
        }

        /// <summary>
        /// Пытается распарсить номер "A&lt;число&gt;" из конца строки.
        /// Пример: "КЦ - A4" -> 4.
        /// </summary>
        private static bool TryParseCrateSlot(string localName, out int slot)
        {
            slot = 0;
            if (string.IsNullOrWhiteSpace(localName))
                return false;

            var s = localName.Trim();
            int dash = s.LastIndexOf('-');
            if (dash < 0 || dash >= s.Length - 1)
                return false;

            var tail = s.Substring(dash + 1).Trim();
            if (tail.Length < 2)
                return false;
            if (tail[0] != 'A' && tail[0] != 'a')
                return false;

            var numPart = tail.Substring(1).Trim();
            return int.TryParse(numPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out slot);
        }

        private static void HideInGrid(DataTable dt, string columnName)
        {
            if (dt == null || string.IsNullOrWhiteSpace(columnName))
                return;
            if (!dt.Columns.Contains(columnName))
                return;

            dt.Columns[columnName].ExtendedProperties["HideInGrid"] = true;
        }

        private static string MakeUniqueKey(string baseKey, Dictionary<string, int> uniq)
        {
            if (!uniq.TryGetValue(baseKey, out var n))
            {
                uniq[baseKey] = 1;
                return baseKey;
            }

            n++;
            uniq[baseKey] = n;
            return baseKey + "#" + n;
        }

        private static void AddScalarChildren(Dictionary<string, string> target, XElement parent, string prefix = "")
        {
            foreach (var el in parent.Elements())
            {
                if (el.HasElements) continue;
                var key = prefix + el.Name.LocalName;
                target[key] = (el.Value ?? "").Trim();
            }
        }

        private static DataTable BuildTableFromRows(string name, List<Dictionary<string, string>> rows)
        {
            var dt = new DataTable(name);

            if (rows == null || rows.Count == 0)
                return dt;

            // Собираем все колонки (в порядке первого появления)
            var colOrder = new List<string>();
            var colSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in rows)
            {
                if (r == null) continue;
                foreach (var k in r.Keys)
                {
                    if (colSet.Add(k))
                        colOrder.Add(k);
                }
            }

            foreach (var c in colOrder)
                dt.Columns.Add(c, typeof(string));

            foreach (var r in rows)
            {
                var dr = dt.NewRow();
                foreach (var kv in r)
                {
                    if (!dt.Columns.Contains(kv.Key))
                        dt.Columns.Add(kv.Key, typeof(string));
                    dr[kv.Key] = kv.Value ?? (object)DBNull.Value;
                }
                dt.Rows.Add(dr);
            }

            return dt;
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

        private sealed class ParsedCfg
        {
            public ParsedCfg(Dictionary<string, DataTable> tables)
            {
                Tables = tables ?? new Dictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);
            }

            public Dictionary<string, DataTable> Tables { get; }
        }
    }
}