using System;
using ExcelDataReader;

namespace MdbDiffTool.Spreadsheet
{
    /// <summary>
    /// Адаптер ExcelDataReader -> ISpreadsheetReader.
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

        public bool NextResult() => _reader.NextResult();

        public object GetValue(int index) => _reader.GetValue(index);

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
