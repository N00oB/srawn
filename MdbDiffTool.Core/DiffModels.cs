using System.Collections.Generic;
using System.Data;

namespace MdbDiffTool.Core
{
    /// <summary>
    /// Тип различия строки.
    /// </summary>
    public enum RowDiffType
    {
        /// <summary>
        /// Есть только в источнике (база объекта).
        /// </summary>
        OnlyInSource,

        /// <summary>
        /// Есть только в приёмнике (дорабатываемая база).
        /// </summary>
        OnlyInTarget,

        /// <summary>
        /// Есть в обеих, но данные отличаются.
        /// </summary>
        Different
    }

    /// <summary>
    /// Пара строк (источник/приёмник) с типом различия.
    /// </summary>
    public sealed class RowPair
    {
        public string Key { get; set; }
        public DataRow SourceRow { get; set; }  // база объекта
        public DataRow TargetRow { get; set; }  // дорабатываемая база
        public RowDiffType DiffType { get; set; }
    }

    /// <summary>
    /// Контекст diff для текущей таблицы (используется в гриде).
    /// </summary>
    public sealed class DiffContext
    {
        public string TableName { get; set; }
        public string[] PrimaryKeyColumns { get; set; }
        public List<RowPair> Pairs { get; set; }
        public DataColumnCollection SourceColumns { get; set; }
    }

    /// <summary>
    /// Результат сравнения одной таблицы.
    /// </summary>
    public sealed class TableDiffResult
    {
        /// <summary>
        /// Имя таблицы.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Таблица из базы-источника (используем её схему/колонки).
        /// </summary>
        public DataTable SourceTable { get; set; }

        

        /// <summary>
        /// Столбцы, которые есть только в источнике (схемы отличаются).
        /// </summary>
        public string[] ColumnsOnlyInSource { get; set; }

        /// <summary>
        /// Столбцы, которые есть только в приёмнике (схемы отличаются).
        /// </summary>
        public string[] ColumnsOnlyInTarget { get; set; }

/// <summary>
        /// Ключевые столбцы (PK или пользовательский ключ, или fallback).
        /// </summary>
        public string[] KeyColumns { get; set; }

        /// <summary>
        /// Список отличий по строкам.
        /// </summary>
        public List<RowPair> DiffPairs { get; set; }
    }

    /// <summary>
    /// Краткая сводка по отличиям одной таблицы.
    /// Используется там, где нужно знать только количества,
    /// а тянуть все DataRow не нужно.
    /// </summary>
    public sealed class TableDiffSummary
    {
        /// <summary>
        /// Имя таблицы.
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Количество строк, которые есть только в источнике.
        /// </summary>
        public int OnlyInSourceCount { get; set; }

        /// <summary>
        /// Количество строк, которые есть только в приёмнике.
        /// </summary>
        public int OnlyInTargetCount { get; set; }

        /// <summary>
        /// Количество строк, которые есть в обеих таблицах,
        /// но отличаются по данным.
        /// </summary>
        public int DifferentCount { get; set; }

        /// <summary>
        /// Общее количество отличающихся строк
        /// (сумма трёх предыдущих).
        /// </summary>
        public int TotalDiffCount { get; set; }
    }
    /// <summary>
    /// Прогресс пакетного сравнения таблиц.
    /// Используется для отображения хода работы в UI.
    /// </summary>
    public sealed class TablesDiffProgress
    {
        /// <summary>
        /// Сколько таблиц уже обработано.
        /// </summary>
        public int Processed { get; set; }

        /// <summary>
        /// Общее количество таблиц.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Имя текущей таблицы (для показа в статусе).
        /// </summary>
        public string CurrentTable { get; set; }
    }
}
