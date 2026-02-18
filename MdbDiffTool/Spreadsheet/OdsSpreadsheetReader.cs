using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace MdbDiffTool.Spreadsheet
{
    /// <summary>
    /// Ридер для .ods (LibreOffice/OpenDocument Spreadsheet).
    /// Внутри .ods — ZIP, основной контент находится в content.xml.
    /// </summary>
    internal sealed class OdsSpreadsheetReader : ISpreadsheetReader
    {
        // Защита от патологических файлов (миллионы пустых колонок/строк из-за повторов).
        // ExcelDatabaseProvider всё равно строит DataTable и UI, поэтому слишком большие размеры не имеют смысла.
        private const int HardMaxColumns = 16384;
        private const int HardMaxRows = 1_048_576;

        private readonly List<OdsSheet> _sheets;
        private int _sheetIndex;

        private OdsSheet CurrentSheet => _sheetIndex >= 0 && _sheetIndex < _sheets.Count ? _sheets[_sheetIndex] : null;

        private int _rowRunIndex;
        private int _rowRepeatLeft;
        private object[] _currentRow;

        public OdsSpreadsheetReader(Stream odsStream)
        {
            if (odsStream == null) throw new ArgumentNullException(nameof(odsStream));
            if (!odsStream.CanSeek)
                throw new InvalidOperationException("Поток .ods должен поддерживать Seek.");

            _sheets = OdsParser.Parse(odsStream, HardMaxColumns, HardMaxRows);
            _sheetIndex = _sheets.Count > 0 ? 0 : -1;

            ResetRowEnumerator();
        }

        public string Name
        {
            get
            {
                var s = CurrentSheet;
                return s?.Name ?? string.Empty;
            }
        }

        public int FieldCount
        {
            get
            {
                var s = CurrentSheet;
                return s?.MaxColumns ?? 0;
            }
        }

        public bool Read()
        {
            var sheet = CurrentSheet;
            if (sheet == null)
                return false;

            while (true)
            {
                if (_rowRepeatLeft > 0)
                {
                    _rowRepeatLeft--;
                    _currentRow = sheet.RowRuns[_rowRunIndex].Cells;
                    return true;
                }

                _rowRunIndex++;
                if (_rowRunIndex >= sheet.RowRuns.Count)
                {
                    _currentRow = null;
                    return false;
                }

                _rowRepeatLeft = sheet.RowRuns[_rowRunIndex].Repeat;
                // loop продолжится и отдаст первую строку текущего run
            }
        }

        public bool NextResult()
        {
            if (_sheets.Count == 0)
                return false;

            if (_sheetIndex < 0)
                _sheetIndex = 0;
            else
                _sheetIndex++;

            if (_sheetIndex >= _sheets.Count)
            {
                _sheetIndex = _sheets.Count; // фиксация
                _currentRow = null;
                return false;
            }

            ResetRowEnumerator();
            return true;
        }

        public object GetValue(int index)
        {
            if (_currentRow == null)
                return null;

            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return index < _currentRow.Length ? _currentRow[index] : null;
        }

        public void Dispose()
        {
            // ресурсов нет
        }

        private void ResetRowEnumerator()
        {
            _rowRunIndex = -1;
            _rowRepeatLeft = 0;
            _currentRow = null;
        }

        private sealed class OdsSheet
        {
            public string Name { get; set; }
            public int MaxColumns { get; set; }
            public List<OdsRowRun> RowRuns { get; } = new List<OdsRowRun>();
        }

        private sealed class OdsRowRun
        {
            public object[] Cells { get; set; } = Array.Empty<object>();
            public int Repeat { get; set; } = 1;
        }

        private static class OdsParser
        {
            private const string NsOffice = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
            private const string NsTable = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
            private const string NsText = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

            public static List<OdsSheet> Parse(Stream odsStream, int hardMaxColumns, int hardMaxRows)
            {
                if (odsStream == null) throw new ArgumentNullException(nameof(odsStream));

                odsStream.Position = 0;

                using (var zip = new ZipArchive(odsStream, ZipArchiveMode.Read, leaveOpen: true))
                {
                    var entry = zip.GetEntry("content.xml");
                    if (entry == null)
                        throw new InvalidOperationException("Файл .ods повреждён: не найден content.xml.");

                    using (var xmlStream = entry.Open())
                    using (var reader = XmlReader.Create(xmlStream, new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Prohibit,
                        IgnoreComments = true,
                        IgnoreProcessingInstructions = true,
                        IgnoreWhitespace = false
                    }))
                    {
                        var sheets = new List<OdsSheet>();

                        while (reader.Read())
                        {
                            if (reader.NodeType != XmlNodeType.Element)
                                continue;

                            if (reader.NamespaceURI == NsTable && reader.LocalName == "table")
                            {
                                var sheet = ReadSheet(reader, hardMaxColumns, hardMaxRows);
                                sheets.Add(sheet);
                            }
                        }

                        // Нормализуем имена листов (пустые -> SheetX)
                        for (var i = 0; i < sheets.Count; i++)
                        {
                            if (string.IsNullOrWhiteSpace(sheets[i].Name))
                                sheets[i].Name = "Sheet" + (i + 1);
                        }

                        return sheets;
                    }
                }
            }

            private static OdsSheet ReadSheet(XmlReader reader, int hardMaxColumns, int hardMaxRows)
            {
                // reader сейчас на <table:table>
                var sheet = new OdsSheet
                {
                    Name = reader.GetAttribute("name", NsTable) ?? reader.GetAttribute("table:name")
                };

                if (reader.IsEmptyElement)
                    return sheet;

                var tableDepth = reader.Depth;

                var runs = new List<OdsRowRun>();

                int totalRows = 0;
                int lastMeaningfulRowIndex = -1;
                int maxCols = 0;

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == tableDepth && reader.NamespaceURI == NsTable && reader.LocalName == "table")
                        break;

                    if (reader.NodeType != XmlNodeType.Element)
                        continue;

                    if (reader.NamespaceURI == NsTable && reader.LocalName == "table-row")
                    {
                        var (cells, repeatRows, hasMeaningful) = ReadRow(reader, hardMaxColumns);

                        if (repeatRows <= 0)
                            repeatRows = 1;

                        if (totalRows + repeatRows > hardMaxRows)
                            throw new InvalidOperationException($"Лист '{sheet.Name}': слишком много строк (>{hardMaxRows}).");

                        runs.Add(new OdsRowRun { Cells = cells, Repeat = repeatRows });

                        totalRows += repeatRows;

                        if (cells.Length > maxCols)
                            maxCols = cells.Length;

                        if (hasMeaningful)
                            lastMeaningfulRowIndex = totalRows - 1; // учитываем повтор
                    }
                }

                // Обрезаем хвостовые пустые строки (как ExcelDataReader: обычно читает только до последней заполненной строки)
                var includeRows = lastMeaningfulRowIndex + 1;
                if (includeRows < 0) includeRows = 0;

                var trimmedRuns = new List<OdsRowRun>();
                int consumed = 0;
                int trimmedMaxCols = 0;

                foreach (var r in runs)
                {
                    if (consumed >= includeRows)
                        break;

                    var canTake = Math.Min(r.Repeat, includeRows - consumed);
                    if (canTake <= 0)
                        break;

                    trimmedRuns.Add(new OdsRowRun { Cells = r.Cells, Repeat = canTake });

                    consumed += canTake;

                    if (r.Cells.Length > trimmedMaxCols)
                        trimmedMaxCols = r.Cells.Length;
                }

                sheet.RowRuns.AddRange(trimmedRuns);
                sheet.MaxColumns = Math.Min(trimmedMaxCols, hardMaxColumns);

                // Если лист оказался шире лимита — это лучше явно сказать, чем молча сместить данные.
                if (trimmedMaxCols > hardMaxColumns)
                    throw new InvalidOperationException($"Лист '{sheet.Name}': слишком много столбцов (>{hardMaxColumns}).");

                // Информативный лог: если из-за повторов/форматирования лист выглядел большим, но по факту пустой
                if (totalRows > 0 && includeRows == 0)
                {
                    AppLogger.Info($"ODS-лист '{sheet.Name}': обнаружены строки, но все они пустые. Будет считаться пустым листом.");
                }

                return sheet;
            }

            private static (object[] cells, int repeatRows, bool hasMeaningful) ReadRow(XmlReader reader, int hardMaxColumns)
            {
                // reader сейчас на <table:table-row>
                var repeatRows = ReadIntAttr(reader, NsTable, "number-rows-repeated", 1);
                if (repeatRows <= 0) repeatRows = 1;

                if (reader.IsEmptyElement)
                    return (Array.Empty<object>(), repeatRows, false);

                var rowDepth = reader.Depth;

                var cells = new List<object>();
                int pendingEmpty = 0;
                bool hasMeaningful = false;

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == rowDepth && reader.NamespaceURI == NsTable && reader.LocalName == "table-row")
                        break;

                    if (reader.NodeType != XmlNodeType.Element)
                        continue;

                    if (reader.NamespaceURI == NsTable && (reader.LocalName == "table-cell" || reader.LocalName == "covered-table-cell"))
                    {
                        var parsed = ReadCell(reader);

                        // Пустые ячейки (в том числе повторённые) — держим в pending и не добавляем сразу.
                        // Если дальше появится реальное значение — pending будет сброшен, сохранив выравнивание.
                        if (!parsed.IsMeaningful)
                        {
                            // Если уже есть что-то в строке и пустая ячейка между значениями — она важна для выравнивания.
                            // Мы не знаем, будет ли дальше значение, поэтому кладём в pending.
                            pendingEmpty = AddPending(pendingEmpty, parsed.Repeat, cells.Count, hardMaxColumns);
                            continue;
                        }

                        FlushPending(ref pendingEmpty, cells, hardMaxColumns);

                        EnsureMaxColumns(cells.Count, parsed.Repeat, hardMaxColumns);

                        for (var i = 0; i < parsed.Repeat; i++)
                            cells.Add(parsed.Value);

                        hasMeaningful = true;
                    }
                }

                // Не сбрасываем pendingEmpty: это хвостовые пустые колонки — они нам не нужны.

                return (cells.ToArray(), repeatRows, hasMeaningful);
            }

            private static int AddPending(int pending, int add, int alreadyHave, int hardMaxColumns)
            {
                if (add <= 0) return pending;

                // pending может накапливаться очень большим; если потом встретится значение, придётся реально развернуть.
                // Поэтому ограничиваем общий потенциальный размер строки.
                if (alreadyHave + pending + add > hardMaxColumns)
                    throw new InvalidOperationException($"ODS: слишком много столбцов (>{hardMaxColumns}).");

                return pending + add;
            }

            private static void FlushPending(ref int pendingEmpty, List<object> cells, int hardMaxColumns)
            {
                if (pendingEmpty <= 0) return;

                EnsureMaxColumns(cells.Count, pendingEmpty, hardMaxColumns);

                for (var i = 0; i < pendingEmpty; i++)
                    cells.Add(null);

                pendingEmpty = 0;
            }

            private static void EnsureMaxColumns(int currentCount, int toAdd, int hardMaxColumns)
            {
                if (toAdd <= 0) return;

                if (currentCount + toAdd > hardMaxColumns)
                    throw new InvalidOperationException($"ODS: слишком много столбцов (>{hardMaxColumns}).");
            }

            private static ParsedCell ReadCell(XmlReader reader)
            {
                // reader сейчас на <table:table-cell> или <table:covered-table-cell>
                var isCovered = reader.LocalName == "covered-table-cell";
                var repeat = ReadIntAttr(reader, NsTable, "number-columns-repeated", 1);
                if (repeat <= 0) repeat = 1;

                if (isCovered)
                {
                    return new ParsedCell
                    {
                        Repeat = repeat,
                        Value = null,
                        IsMeaningful = false
                    };
                }

                var valueType = reader.GetAttribute("value-type", NsOffice);
                var formula = reader.GetAttribute("formula", NsTable);

                // В .ods кэш значения чаще всего лежит в атрибутах office:*.
                var strValue = reader.GetAttribute("string-value", NsOffice);
                var rawValue = reader.GetAttribute("value", NsOffice);
                var rawDate = reader.GetAttribute("date-value", NsOffice);
                var rawTime = reader.GetAttribute("time-value", NsOffice);
                var rawBool = reader.GetAttribute("boolean-value", NsOffice);

                string text = null;

                if (!reader.IsEmptyElement)
                {
                    var cellDepth = reader.Depth;

                    // Читаем до закрывающего тега ячейки, собирая текст из text:p.
                    var sb = new StringBuilder();
                    bool firstPara = true;

                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == NsText && reader.LocalName == "p")
                        {
                            if (!firstPara)
                                sb.Append("\n");
                            firstPara = false;
                        }

                        if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA || reader.NodeType == XmlNodeType.SignificantWhitespace)
                        {
                            sb.Append(reader.Value);
                        }

                        if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == cellDepth && reader.NamespaceURI == NsTable && reader.LocalName == "table-cell")
                            break;
                    }

                    text = sb.ToString();
                }

                // Правило по формулам (согласовано):
                // 1) если есть кэш-значение — берём его,
                // 2) иначе берём текст формулы.
                var value = TryParseCachedValue(valueType, strValue, rawValue, rawDate, rawTime, rawBool, text);
                if (value == null && !string.IsNullOrWhiteSpace(formula))
                    value = formula;

                return new ParsedCell
                {
                    Repeat = repeat,
                    Value = value,
                    IsMeaningful = IsMeaningful(value)
                };
            }

            private static object TryParseCachedValue(
                string valueType,
                string strValue,
                string rawValue,
                string rawDate,
                string rawTime,
                string rawBool,
                string text)
            {
                // Пустые типы
                if (string.Equals(valueType, "void", StringComparison.OrdinalIgnoreCase))
                    return null;

                // Строка
                if (string.Equals(valueType, "string", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(valueType, "text", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(strValue))
                        return strValue;

                    if (!string.IsNullOrEmpty(text))
                        return text;

                    return null;
                }

                // Число
                if (string.Equals(valueType, "float", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(valueType, "currency", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(valueType, "percentage", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(rawValue) &&
                        double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        return d;

                    // иногда число может лежать как текст
                    if (!string.IsNullOrWhiteSpace(text) &&
                        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d2))
                        return d2;

                    return string.IsNullOrWhiteSpace(text) ? null : text;
                }

                // Булево
                if (string.Equals(valueType, "boolean", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(rawBool) && bool.TryParse(rawBool, out var b))
                        return b;

                    if (!string.IsNullOrWhiteSpace(text) && bool.TryParse(text, out var b2))
                        return b2;

                    return string.IsNullOrWhiteSpace(text) ? null : text;
                }

                // Дата/время
                if (string.Equals(valueType, "date", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(rawDate) &&
                        DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                        return dt;

                    if (!string.IsNullOrWhiteSpace(text) &&
                        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt2))
                        return dt2;

                    return string.IsNullOrWhiteSpace(text) ? null : text;
                }

                if (string.Equals(valueType, "time", StringComparison.OrdinalIgnoreCase))
                {
                    // Обычно xsd:duration, например PT1H2M3S
                    if (!string.IsNullOrWhiteSpace(rawTime))
                    {
                        try
                        {
                            return XmlConvert.ToTimeSpan(rawTime);
                        }
                        catch
                        {
                            // fallback на строку
                            return rawTime;
                        }
                    }

                    return string.IsNullOrWhiteSpace(text) ? null : text;
                }

                // Если тип неизвестен — пробуем то, что есть
                if (!string.IsNullOrWhiteSpace(rawValue))
                    return rawValue;

                if (!string.IsNullOrWhiteSpace(text))
                    return text;

                return null;
            }

            private static bool IsMeaningful(object value)
            {
                if (value == null || value is DBNull)
                    return false;

                if (value is string s)
                    return !string.IsNullOrWhiteSpace(s);

                return true;
            }

            private static int ReadIntAttr(XmlReader reader, string ns, string localName, int defaultValue)
            {
                var s = reader.GetAttribute(localName, ns);
                if (string.IsNullOrWhiteSpace(s))
                    return defaultValue;

                if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    return i;

                return defaultValue;
            }

            private struct ParsedCell
            {
                public int Repeat;
                public object Value;
                public bool IsMeaningful;
            }
        }
    }
}
