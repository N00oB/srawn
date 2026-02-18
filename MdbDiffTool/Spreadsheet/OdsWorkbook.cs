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
    /// Иммутабельная модель книги ODS, пригодная для кэширования.
    /// Парсит content.xml и хранит значения ячеек (кэш-значение, а если его нет — формулу текстом).
    /// </summary>
    internal sealed class OdsWorkbook
    {
        public IReadOnlyList<OdsSheet> Sheets { get; }

        private OdsWorkbook(List<OdsSheet> sheets)
        {
            Sheets = sheets ?? throw new ArgumentNullException(nameof(sheets));
        }

        public static OdsWorkbook Parse(Stream odsStream)
        {
            if (odsStream == null) throw new ArgumentNullException(nameof(odsStream));

            using (var zip = new ZipArchive(odsStream, ZipArchiveMode.Read, leaveOpen: true))
            {
                var entry = zip.GetEntry("content.xml");
                if (entry == null)
                    throw new InvalidOperationException("ODS: в архиве не найден файл content.xml.");

                using (var contentStream = entry.Open())
                {
                    return ParseContentXml(contentStream);
                }
            }
        }

        private static OdsWorkbook ParseContentXml(Stream xmlStream)
        {
            var sheets = new List<OdsSheet>();

            var settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Ignore
            };

            using (var reader = XmlReader.Create(xmlStream, settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element)
                        continue;

                    if (reader.NamespaceURI == Ns.Table && reader.LocalName == "table")
                    {
                        var sheet = ParseSheet(reader);
                        sheets.Add(sheet);
                    }
                }
            }

            return new OdsWorkbook(sheets);
        }

        private static OdsSheet ParseSheet(XmlReader reader)
        {
            // reader стоит на <table:table ...>
            var name = reader.GetAttribute("name", Ns.Table) ?? string.Empty;
            var runs = new List<OdsRowRun>();
            int maxColumns = 0;

            // В ODS (особенно после конвертации из XLSX) LibreOffice часто хранит "пустой хвост" листа
            // как одну строку с table:number-rows-repeated="1048576" (или близко к этому).
            // Если разворачивать повторы, получаем миллионы пустых строк и медленную работу.
            // Поэтому после парсинга листа обрезаем хвостовые повторяющиеся пустые строки
            // до последней строки, где есть хоть одно осмысленное значение.
            int totalRows = 0;
            int lastMeaningfulRow = 0; // 1-based индекс последней осмысленной строки в развёрнутом виде

            int tableDepth = reader.Depth;

            if (reader.IsEmptyElement)
                return new OdsSheet(name, maxColumns, runs);

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Ns.Table && reader.LocalName == "table-row")
                {
                    var run = ParseRowRun(reader, ref maxColumns);
                    if (run != null)
                    {
                        runs.Add(run);

                        totalRows += run.RepeatCount;
                        if (run.Cells != null)
                            lastMeaningfulRow = totalRows;
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == tableDepth && reader.NamespaceURI == Ns.Table && reader.LocalName == "table")
                {
                    break;
                }
            }

            TrimTrailingEmptyRowRuns(runs, totalRows, lastMeaningfulRow);

            return new OdsSheet(name, maxColumns, runs);
        }

        private static void TrimTrailingEmptyRowRuns(List<OdsRowRun> runs, int totalRows, int lastMeaningfulRow)
        {
            if (runs == null || runs.Count == 0)
                return;

            // Если нет ни одной осмысленной строки — оставляем пустой лист (строк 0)
            if (lastMeaningfulRow <= 0)
            {
                runs.Clear();
                return;
            }

            int keep = lastMeaningfulRow;
            int current = totalRows;

            // Обрезаем только хвост (после последней осмысленной строки)
            for (int i = runs.Count - 1; i >= 0 && current > keep; i--)
            {
                var run = runs[i];
                int excess = current - keep;

                if (run.RepeatCount > excess)
                {
                    runs[i] = new OdsRowRun(run.RepeatCount - excess, run.Cells);
                    current = keep;
                    break;
                }

                current -= run.RepeatCount;
                runs.RemoveAt(i);
            }
        }

        private static OdsRowRun ParseRowRun(XmlReader reader, ref int maxColumns)
        {
            // reader стоит на <table:table-row>
            int repeat = GetIntAttr(reader, "number-rows-repeated", Ns.Table, 1);
            if (repeat < 1) repeat = 1;

            int rowDepth = reader.Depth;

            object[] rowCells = null;

            if (!reader.IsEmptyElement)
            {
                var cells = new List<object>();

                int colIndex = 0;
                int lastMeaningful = -1;
                int pendingNulls = 0;

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Ns.Table &&
                        (reader.LocalName == "table-cell" || reader.LocalName == "covered-table-cell"))
                    {
                        var cell = ParseCell(reader, out int colRepeat);
                        if (colRepeat < 1) colRepeat = 1;

                        if (!HasMeaningfulValue(cell))
                        {
                            // пустые/пробельные значения — откладываем (чтобы не раздувать хвост)
                            pendingNulls += colRepeat;
                            colIndex += colRepeat;
                            continue;
                        }

                        // встретилось осмысленное значение — фиксируем пропуски
                        if (pendingNulls > 0)
                        {
                            AddNulls(cells, pendingNulls);
                            pendingNulls = 0;
                        }

                        // добавляем значение с учётом повторов
                        for (int i = 0; i < colRepeat; i++)
                            cells.Add(cell);

                        lastMeaningful = colIndex + colRepeat - 1;
                        colIndex += colRepeat;
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == rowDepth && reader.NamespaceURI == Ns.Table && reader.LocalName == "table-row")
                    {
                        break;
                    }
                }

                // trailing pendingNulls игнорируем (хвост пустых ячеек)
                if (lastMeaningful >= 0)
                {
                    // cells уже содержит все внутренние null-ы до последнего осмысленного
                    rowCells = cells.ToArray();
                    if (rowCells.Length > maxColumns)
                        maxColumns = rowCells.Length;
                }
            }

            return new OdsRowRun(repeat, rowCells);
        }

        private static object ParseCell(XmlReader reader, out int repeat)
        {
            repeat = GetIntAttr(reader, "number-columns-repeated", Ns.Table, 1);
            if (repeat < 1) repeat = 1;

            // covered-table-cell: это «покрытая» ячейка (мерж), считаем пустой
            bool covered = reader.LocalName == "covered-table-cell";

            var formula = reader.GetAttribute("formula", Ns.Table);

            var valueType = reader.GetAttribute("value-type", Ns.Office);
            var value = reader.GetAttribute("value", Ns.Office);
            var dateValue = reader.GetAttribute("date-value", Ns.Office);
            var timeValue = reader.GetAttribute("time-value", Ns.Office);
            var boolValue = reader.GetAttribute("boolean-value", Ns.Office);
            var stringValue = reader.GetAttribute("string-value", Ns.Office);

            string text = null;

            if (!reader.IsEmptyElement)
            {
                int cellDepth = reader.Depth;
                var sb = new StringBuilder();

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Ns.Text && reader.LocalName == "p")
                    {
                        // ReadElementContentAsString() падает, если внутри <text:p> есть дочерние элементы (например, text:span).
                        var p = ReadTextP(reader);
                        if (!string.IsNullOrEmpty(p))
                        {
                            if (sb.Length > 0)
                                sb.Append('\n');
                            sb.Append(p);
                        }

                        continue;
                    }

                    if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == cellDepth && reader.NamespaceURI == Ns.Table &&
                        (reader.LocalName == "table-cell" || reader.LocalName == "covered-table-cell"))
                    {
                        break;
                    }
                }

                if (sb.Length > 0)
                    text = sb.ToString();
            }

            if (covered)
                return null;

            object cached = null;

            if (!string.IsNullOrWhiteSpace(valueType))
            {
                switch (valueType)
                {
                    case "string":
                        cached = FirstNonEmpty(stringValue, text);
                        break;

                    case "float":
                    case "percentage":
                    case "currency":
                        if (TryParseDoubleInvariant(value, out var d))
                            cached = d;
                        else
                            cached = FirstNonEmpty(stringValue, text);
                        break;

                    case "boolean":
                        if (!string.IsNullOrWhiteSpace(boolValue))
                            cached = string.Equals(boolValue, "true", StringComparison.OrdinalIgnoreCase);
                        break;

                    case "date":
                        if (!string.IsNullOrWhiteSpace(dateValue) &&
                            DateTime.TryParse(dateValue, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                        {
                            cached = dt;
                        }
                        else
                        {
                            cached = FirstNonEmpty(stringValue, text);
                        }
                        break;

                    case "time":
                        if (!string.IsNullOrWhiteSpace(timeValue))
                        {
                            try
                            {
                                // обычно это xsd:duration, например PT1H30M
                                cached = XmlConvert.ToTimeSpan(timeValue);
                            }
                            catch
                            {
                                cached = timeValue;
                            }
                        }
                        else
                        {
                            cached = FirstNonEmpty(stringValue, text);
                        }
                        break;

                    case "void":
                        cached = null;
                        break;

                    default:
                        cached = FirstNonEmpty(stringValue, text);
                        break;
                }
            }
            else
            {
                // Иногда value-type отсутствует, но текст есть.
                cached = FirstNonEmpty(stringValue, text);
            }

            // Правило: если кэш-значение пустое — берём формулу текстом.
            if (!HasMeaningfulValue(cached) && !string.IsNullOrWhiteSpace(formula))
                return formula;

            return cached;
        }

        /// <summary>
        /// Читает содержимое &lt;text:p&gt; как текст, корректно обрабатывая вложенные элементы (text:span, text:a и т.п.).
        /// После возврата reader будет стоять на EndElement &lt;/text:p&gt;.
        /// </summary>
        private static string ReadTextP(XmlReader reader)
        {
            if (reader == null)
                return string.Empty;

            // reader стоит на <text:p>
            if (reader.IsEmptyElement)
                return string.Empty;

            int pDepth = reader.Depth;
            var sb = new StringBuilder();

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == pDepth && reader.NamespaceURI == Ns.Text && reader.LocalName == "p")
                    break;

                if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA)
                {
                    sb.Append(reader.Value);
                    continue;
                }

                // Управляющие элементы текста LibreOffice (пробелы/таб/перенос строки)
                if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Ns.Text)
                {
                    if (reader.LocalName == "s")
                    {
                        int c = GetIntAttr(reader, "c", Ns.Text, 1);
                        if (c < 1) c = 1;
                        sb.Append(' ', c);
                        continue;
                    }
                    if (reader.LocalName == "tab")
                    {
                        sb.Append('\t');
                        continue;
                    }
                    if (reader.LocalName == "line-break")
                    {
                        sb.Append('\n');
                        continue;
                    }
                }
            }

            return sb.ToString();
        }

        private static void AddNulls(List<object> list, int count)
        {
            if (count <= 0) return;
            for (int i = 0; i < count; i++)
                list.Add(null);
        }

        private static bool HasMeaningfulValue(object v)
        {
            if (v == null || v is DBNull)
                return false;

            if (v is string s)
                return !string.IsNullOrWhiteSpace(s);

            return true;
        }

        private static string FirstNonEmpty(string a, string b)
        {
            if (!string.IsNullOrWhiteSpace(a)) return a;
            if (!string.IsNullOrWhiteSpace(b)) return b;
            return null;
        }

        private static bool TryParseDoubleInvariant(string s, out double value)
        {
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static int GetIntAttr(XmlReader reader, string localName, string ns, int defaultValue)
        {
            var s = reader.GetAttribute(localName, ns);
            if (string.IsNullOrWhiteSpace(s))
                return defaultValue;

            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                return v;

            return defaultValue;
        }

        internal static class Ns
        {
            public const string Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
            public const string Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
            public const string Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        }

        internal sealed class OdsSheet
        {
            public string Name { get; }
            public int MaxColumns { get; }
            public IReadOnlyList<OdsRowRun> RowRuns { get; }

            public OdsSheet(string name, int maxColumns, List<OdsRowRun> rowRuns)
            {
                Name = name ?? string.Empty;
                MaxColumns = maxColumns < 0 ? 0 : maxColumns;
                RowRuns = rowRuns ?? new List<OdsRowRun>();
            }
        }

        internal sealed class OdsRowRun
        {
            public int RepeatCount { get; }

            /// <summary>
            /// Значения ячеек. Если null — строка пустая.
            /// Хвост пустых ячеек отрезан (т.е. последний элемент всегда осмысленный).
            /// </summary>
            public object[] Cells { get; }

            public OdsRowRun(int repeatCount, object[] cells)
            {
                RepeatCount = repeatCount < 1 ? 1 : repeatCount;
                Cells = cells; // может быть null
            }
        }
    }
}
