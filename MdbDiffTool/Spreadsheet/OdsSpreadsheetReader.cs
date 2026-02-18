using System;
using System.IO;

namespace MdbDiffTool.Spreadsheet
{
    /// <summary>
    /// Reader по книге ODS.
    /// Важно: в batch-режиме используется уже распарсенная OdsWorkbook (кэш),
    /// чтобы не разбирать content.xml заново для каждого листа.
    /// </summary>
    internal sealed class OdsSpreadsheetReader : ISpreadsheetReader
    {
        private readonly OdsWorkbook _workbook;

        private int _sheetIndex;
        private int _runIndex;
        private int _runRepeatLeft;
        private object[] _currentRow;

        public OdsSpreadsheetReader(Stream odsStream)
        {
            if (odsStream == null) throw new ArgumentNullException(nameof(odsStream));
            _workbook = OdsWorkbook.Parse(odsStream);
            ResetToSheet(0);
        }

        public OdsSpreadsheetReader(OdsWorkbook workbook)
        {
            _workbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
            ResetToSheet(0);
        }

        public string Name
        {
            get
            {
                var sheet = CurrentSheet;
                return sheet?.Name ?? string.Empty;
            }
        }

        public int FieldCount
        {
            get
            {
                var sheet = CurrentSheet;
                return sheet?.MaxColumns ?? 0;
            }
        }

        public bool Read()
        {
            var sheet = CurrentSheet;
            if (sheet == null)
                return false;

            // если текущий run ещё не исчерпан — повторяем строку
            if (_runRepeatLeft > 0)
            {
                _runRepeatLeft--;
                return true;
            }

            if (_runIndex >= sheet.RowRuns.Count)
            {
                _currentRow = null;
                return false;
            }

            var run = sheet.RowRuns[_runIndex++];
            _currentRow = run.Cells;
            _runRepeatLeft = run.RepeatCount - 1;
            return true;
        }

        public object GetValue(int i)
        {
            if (i < 0)
                return null;

            var row = _currentRow;
            if (row == null)
                return null;

            if (i >= row.Length)
                return null;

            return row[i];
        }

        public bool NextResult()
        {
            int next = _sheetIndex + 1;
            if (next >= _workbook.Sheets.Count)
                return false;

            ResetToSheet(next);
            return true;
        }

        public void Dispose()
        {
            // Нечего освобождать — workbook иммутабельный.
        }

        private OdsWorkbook.OdsSheet CurrentSheet
        {
            get
            {
                if (_workbook == null || _workbook.Sheets == null)
                    return null;

                if (_sheetIndex < 0 || _sheetIndex >= _workbook.Sheets.Count)
                    return null;

                return (OdsWorkbook.OdsSheet)_workbook.Sheets[_sheetIndex];
            }
        }

        private void ResetToSheet(int sheetIndex)
        {
            _sheetIndex = sheetIndex;
            _runIndex = 0;
            _runRepeatLeft = 0;
            _currentRow = null;
        }
    }
}
