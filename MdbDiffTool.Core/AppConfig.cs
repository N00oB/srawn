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

        /// <summary>
        /// Для сравнения набора CSV-файлов в папке: искать файлы в подпапках (рекурсивно).
        /// По умолчанию выключено (только верхний уровень).
        /// </summary>
        public bool CsvFolderRecursive { get; set; } = false;

        /// <summary>
        /// Для сравнения набора XML .config файлов в папке: искать файлы в подпапках (рекурсивно).
        /// По умолчанию выключено (только верхний уровень).
        /// </summary>
        public bool XmlConfigFolderRecursive { get; set; } = false;

        // -------------------------------
        // UI (окно и панели)
        // -------------------------------

        /// <summary>
        /// Позиция окна (X). -1 означает "не задано".
        /// </summary>
        public int UiWindowX { get; set; } = -1;

        /// <summary>
        /// Позиция окна (Y). -1 означает "не задано".
        /// </summary>
        public int UiWindowY { get; set; } = -1;

        /// <summary>
        /// Ширина окна. 0 означает "не задано".
        /// </summary>
        public int UiWindowWidth { get; set; } = 0;

        /// <summary>
        /// Высота окна. 0 означает "не задано".
        /// </summary>
        public int UiWindowHeight { get; set; } = 0;

        /// <summary>
        /// Состояние окна: 0=Normal, 1=Minimized, 2=Maximized.
        /// </summary>
        public int UiWindowState { get; set; } = 0;

        /// <summary>
        /// Положение разделителя основной панели (SplitContainer.SplitterDistance).
        /// 0 означает "не задано".
        /// </summary>
        public int UiSplitMainDistance { get; set; } = 0;
    }

    [Serializable]
    public sealed class CustomKeyConfig
    {
        public string TableName { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
    }
}
