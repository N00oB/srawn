using System.Collections.Generic;
using System.Threading;

namespace MdbDiffTool.Core
{
    /// <summary>
    /// Быстрый режим "сводного" сравнения без загрузки DataTable.
    /// Провайдер отдаёт словарь: Key -> Hash(row), где Hash соответствует логике DiffEngine.RowsEqual.
    /// </summary>
    public interface IFastRowHashProvider
    {
        /// <summary>
        /// Получить список столбцов и CLR-типы (как они приходят в DataTable при обычной загрузке).
        /// </summary>
        ColumnInfo[] GetTableColumns(string connectionString, string tableName);

        /// <summary>
        /// Быстро загрузить Key->Hash по таблице без построения DataTable.
        /// </summary>
        Dictionary<string, ulong> LoadKeyHashMap(
            string connectionString,
            string tableName,
            string[] keyColumns,
            ColumnInfo[] tableColumns,
            CancellationToken cancellationToken);
    }
}
