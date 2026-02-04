using System;
using System.Collections.Generic;
using System.Data;
using MdbDiffTool.Core;
using System.Threading;

namespace MdbDiffTool
{
    /// <summary>
    /// Провайдер, который умеет работать с разными типами БД
    /// (Access через OleDb и SQLite через System.Data.SQLite),
    /// переключаясь по строке подключения.
    /// </summary>
    internal sealed class MultiDatabaseProvider : IDatabaseProvider, IBatchDatabaseProvider, IFastRowHashProvider
    {
        private readonly AccessDatabaseProvider _accessProvider = new AccessDatabaseProvider();
        private readonly SqliteDatabaseProvider _sqliteProvider = new SqliteDatabaseProvider();
        private readonly ExcelDatabaseProvider _excelProvider = new ExcelDatabaseProvider();
        private readonly PostgresDatabaseProvider _postgresProvider = new PostgresDatabaseProvider();
        private readonly ConnectionStringService _connectionStringService = new ConnectionStringService();

        private string Normalize(string input)
        {
            // Позволяем передавать как пути к файлам, так и строки подключения,
            // включая URI-формат postgresql://...
            return _connectionStringService.BuildFromInput(input);
        }

        private IDatabaseProvider Resolve(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Пустая строка подключения.", nameof(connectionString));

            var cs = connectionString;

            // PostgreSQL: URI-формат (postgresql://...)
            if (cs.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
                cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
                return _postgresProvider;

            // Access: OleDb c Provider=...
            if (cs.IndexOf("Provider=", StringComparison.OrdinalIgnoreCase) >= 0)
                return _accessProvider;

            // Excel: наш внутренний формат "ExcelFile=...;"
            if (cs.StartsWith("ExcelFile=", StringComparison.OrdinalIgnoreCase))
                return _excelProvider;

            // PostgreSQL: классический формат Npgsql c Host=/Server=
            if (cs.IndexOf("Host=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cs.IndexOf("Server=", StringComparison.OrdinalIgnoreCase) >= 0)
                return _postgresProvider;

            // Всё остальное считаем SQLite (Data Source=...;Version=3;)
            return _sqliteProvider;
        }



        public ProviderCapabilities GetCapabilities(string connectionString)
        {
            var cs = Normalize(connectionString);
            return Resolve(cs).GetCapabilities(cs);
        }

        public List<string> GetTableNames(string connectionString)
        {
            var cs = Normalize(connectionString);
            return Resolve(cs).GetTableNames(cs);
        }

        public DataTable LoadTable(string connectionString, string tableName)
        {
            var cs = Normalize(connectionString);
            return Resolve(cs).LoadTable(cs, tableName);
        }

        public string[] GetPrimaryKeyColumns(string connectionString, string tableName)
        {
            var cs = Normalize(connectionString);
            return Resolve(cs).GetPrimaryKeyColumns(cs, tableName);
        }

        public void ApplyRowChanges(
            string targetConnectionString,
            string tableName,
            string[] primaryKeyColumns,
            IEnumerable<RowPair> pairsToApply)
        {
            var cs = Normalize(targetConnectionString);
            Resolve(cs).ApplyRowChanges(
                cs,
                tableName,
                primaryKeyColumns,
                pairsToApply);
        }

        public void ReplaceTable(
            string targetConnectionString,
            string tableName,
            DataTable sourceTable)
        {
            var cs = Normalize(targetConnectionString);
            Resolve(cs).ReplaceTable(
                cs,
                tableName,
                sourceTable);
        }


        public void DropTable(string connectionString, string tableName)
        {
            var cs = Normalize(connectionString);
            Resolve(cs).DropTable(cs, tableName);
        }

        public void BeginBatch(string connectionString)
        {
            var cs = Normalize(connectionString);
            var resolved = Resolve(cs);
            if (resolved is IBatchDatabaseProvider batch)
            {
                batch.BeginBatch(cs);
            }
        }

        public void EndBatch(string connectionString)
        {
            var cs = Normalize(connectionString);
            var resolved = Resolve(cs);
            if (resolved is IBatchDatabaseProvider batch)
            {
                batch.EndBatch(cs);
            }
        }
 
        private string GetProviderNameByConnectionString(string normalizedConnectionString)
        {
            var p = Resolve(normalizedConnectionString);

            if (ReferenceEquals(p, _accessProvider))
                return "Access";
            if (ReferenceEquals(p, _sqliteProvider))
                return "SQLite";
            if (ReferenceEquals(p, _postgresProvider))
                return "PostgreSQL";

            return p == null ? "Unknown" : p.GetType().Name;
        }

        public ColumnInfo[] GetTableColumns(string connectionString, string tableName)
        {
            var cs = Normalize(connectionString);

            var p = Resolve(cs) as IFastRowHashProvider;
            if (p == null)
                return null;

            return p.GetTableColumns(cs, tableName);
        }

        public Dictionary<string, ulong> LoadKeyHashMap(
            string connectionString,
            string tableName,
            string[] keyColumns,
            ColumnInfo[] tableColumns,
            CancellationToken cancellationToken)
        {
            var cs = Normalize(connectionString);

            var p = Resolve(cs) as IFastRowHashProvider;
            if (p == null)
                throw new NotSupportedException($"Провайдер '{GetProviderNameByConnectionString(cs)}' не поддерживает быстрый режим сравнения.");

            return p.LoadKeyHashMap(cs, tableName, keyColumns, tableColumns, cancellationToken);
        }
    }
}