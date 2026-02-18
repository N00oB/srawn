using System;

namespace MdbDiffTool.Spreadsheet
{
    /// <summary>
    /// Минимальный унифицированный интерфейс чтения табличных файлов (Excel/ODS).
    /// Нужен, чтобы не тащить ExcelDataReader напрямую в код провайдера.
    /// </summary>
    internal interface ISpreadsheetReader : IDisposable
    {
        /// <summary>
        /// Имя текущего листа.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Количество колонок (A..), для текущего листа.
        /// </summary>
        int FieldCount { get; }

        /// <summary>
        /// Переход к следующей строке текущего листа.
        /// </summary>
        bool Read();

        /// <summary>
        /// Значение ячейки в текущей строке по индексу (0-based).
        /// </summary>
        object GetValue(int i);

        /// <summary>
        /// Переход к следующему листу.
        /// </summary>
        bool NextResult();
    }
}
