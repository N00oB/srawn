using System;
using System.Collections.Generic;

namespace MdbDiffTool.Core
{
    [Serializable]
    public sealed class AppConfig
    {
        public string LastSourcePath { get; set; }
        public string LastTargetPath { get; set; }

        /// <summary>
        /// Последний выбранный тип файлов в диалоге "Обзор" (Источник).
        /// 1-based индекс, как в OpenFileDialog.FilterIndex.
        /// </summary>
        public int LastSourceBrowseFilterIndex { get; set; } = 1;

        /// <summary>
        /// Последний выбранный тип файлов в диалоге "Обзор" (Приёмник).
        /// 1-based индекс, как в OpenFileDialog.FilterIndex.
        /// </summary>
        public int LastTargetBrowseFilterIndex { get; set; } = 1;

        /// <summary>
        /// Папка для логов.
        /// Если пусто — используется значение по умолчанию (%LocalAppData%\\MdbDiffTool\\Logs).
        /// </summary>
        public string LogsDirectory { get; set; }

        public List<string> ExcludedTables { get; set; } = new List<string>();
        public List<CustomKeyConfig> CustomKeys { get; set; } = new List<CustomKeyConfig>();
        public int MaxParallelTables { get; set; } = 4;

        /// <summary>
        /// Показывать в diff-таблице маркеры различий для NULL и пустой строки.
        /// Включено по умолчанию.
        /// </summary>
        public bool ShowNullEmptyMarkers { get; set; } = true;
    }

    [Serializable]
    public sealed class CustomKeyConfig
    {
        public string TableName { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
    }
}
