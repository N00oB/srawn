using System.Collections.Generic;
using System.Data;

namespace MdbDiffTool.Core
{
    public interface IDatabaseProvider
    {
        List<string> GetTableNames(string connectionString);
        DataTable LoadTable(string connectionString, string tableName);
        string[] GetPrimaryKeyColumns(string connectionString, string tableName);

        /// <summary>
        /// Применить изменения по строкам (INSERT/UPDATE) для выбранных diff-строк.
        /// </summary>
        void ApplyRowChanges(
            string targetConnectionString,
            string tableName,
            string[] primaryKeyColumns,
            IEnumerable<RowPair> pairsToApply);

        /// <summary>
        /// Полностью заменить содержимое таблицы данными из sourceTable.
        /// </summary>
        void ReplaceTable(
            string targetConnectionString,
            string tableName,
            DataTable sourceTable);
    
        /// <summary>
        /// Удалить таблицу.
        /// </summary>
        void DropTable(string connectionString, string tableName);
}
}
