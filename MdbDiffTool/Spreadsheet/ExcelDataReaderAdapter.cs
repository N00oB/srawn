using System;
using ExcelDataReader;

namespace MdbDiffTool.Spreadsheet
{
    /// <summary>
    /// Адаптер ExcelDataReader под ISpreadsheetReader.
    /// </summary>
    internal sealed class ExcelDataReaderAdapter : ISpreadsheetReader
    {
        private readonly IExcelDataReader _reader;

        public ExcelDataReaderAdapter(IExcelDataReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public string Name => _reader.Name;

        public int FieldCount => _reader.FieldCount;

        public bool Read() => _reader.Read();

        public object GetValue(int i) => _reader.GetValue(i);

        public bool NextResult() => _reader.NextResult();

        public void Dispose()
        {
            try { _reader.Dispose(); } catch { }
        }
    }
}
