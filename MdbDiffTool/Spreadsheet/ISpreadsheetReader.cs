using System;

namespace MdbDiffTool.Spreadsheet
{
    /// <summary>
    /// Минимальная абстракция "табличного ридера" (Excel/ODS и т.п.), чтобы ExcelDatabaseProvider
    /// не зависел напрямую от конкретного формата.
    /// </summary>
    internal interface ISpreadsheetReader : IDisposable
    {
        /// <summary>
        /// Имя текущего листа.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Максимальное количество колонок в текущем листе.
        /// </summary>
        int FieldCount { get; }

        /// <summary>
        /// Переходит к следующей строке в текущем листе.
        /// </summary>
        bool Read();

        /// <summary>
        /// Переходит к следующему листу.
        /// </summary>
        bool NextResult();

        /// <summary>
        /// Возвращает значение ячейки по индексу колонки (0-based). Для отсутствующих колонок возвращает null.
        /// </summary>
        object GetValue(int index);
    }
}
