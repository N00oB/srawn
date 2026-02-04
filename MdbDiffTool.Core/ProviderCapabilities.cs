using System;

namespace MdbDiffTool.Core
{
    /// <summary>
    /// Возможности провайдера источника данных.
    /// Используется для корректного управления UI (read-only источники, например Excel/XML).
    /// </summary>
    [Flags]
    public enum ProviderCapabilities
    {
        None = 0,

        /// <summary>
        /// Поддерживает чтение таблиц/данных.
        /// </summary>
        Read = 1 << 0,

        /// <summary>
        /// Поддерживает применение построчных изменений (INSERT/UPDATE) в приёмник.
        /// </summary>
        ApplyRowChanges = 1 << 1,

        /// <summary>
        /// Поддерживает полную замену таблицы (ReplaceTable) в приёмнике.
        /// </summary>
        ReplaceTable = 1 << 2,

        /// <summary>
        /// Поддерживает удаление таблицы (DropTable).
        /// </summary>
        DropTable = 1 << 3,
    }
}
