using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    /// <summary>
    /// Строит содержимое DataGridView по результатам diff.
    /// Вынесен из Form1, чтобы разгрузить форму.
    /// </summary>
    internal sealed class DiffGridBuilder
    {
        private readonly DataGridView _grid;
        private readonly Label _statusLabel;
        private readonly Font _diffBoldFont;

        public DiffGridBuilder(DataGridView grid, Label statusLabel, Font diffBoldFont)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _statusLabel = statusLabel;
            _diffBoldFont = diffBoldFont;
        }

        /// <summary>
        /// Очищает грид и показывает сообщение, что сравнение недоступно.
        /// </summary>
        public void BuildEmptyGrid(string tableName)
        {
            _grid.Columns.Clear();
            _grid.Rows.Clear();
            _grid.Tag = null;

            if (_statusLabel != null)
            {
                _statusLabel.Text = "Таблица [" + tableName + "]: сравнение недоступно.";
            }
        }

        /// <summary>
        /// Заполняет грид результатами diff по одной таблице.
        /// </summary>
        public void ShowDiff(string tableName, List<RowPair> pairs, DataColumnCollection columns, string[] pkColumns)
        {
            var sw = Stopwatch.StartNew();

            // Отключаем лишние перерисовки и авторазмер на время построения
            _grid.SuspendLayout();

            var prevAutoSizeColumnsMode = _grid.AutoSizeColumnsMode;
            var prevAutoSizeRowsMode = _grid.AutoSizeRowsMode;

            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;

            try
            {
                // Сначала отсортируем diff по ключевым столбцам (PK или пользовательский ключ),
                // чтобы порядок строк был как в базе.
                if (pairs != null && pairs.Count > 1 &&
                    pkColumns != null && pkColumns.Length > 0 &&
                    columns != null)
                {
                    try
                    {
                        pairs.Sort(new RowPairKeyComparer(pkColumns, columns));
                    }
                    catch
                    {
                        AppLogger.Info("Мультисортировка не сработала, оставим исходный порядок.");
                    }
                }

                _grid.Columns.Clear();
                _grid.Rows.Clear();

                var ctx = new DiffContext
                {
                    TableName = tableName,
                    PrimaryKeyColumns = pkColumns,
                    Pairs = pairs,
                    SourceColumns = columns
                };
                _grid.Tag = ctx;

                // Колонка "Применить"
                var applyCol = new DataGridViewCheckBoxColumn
                {
                    Name = "Apply",
                    HeaderText = "Применить",
                    Width = 70,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                };
                _grid.Columns.Add(applyCol);

                // Колонка "Статус"
                var statusCol = new DataGridViewTextBoxColumn
                {
                    Name = "Status",
                    HeaderText = "Статус",
                    ReadOnly = true,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                };
                _grid.Columns.Add(statusCol);

                // Колонки по полям таблицы
                if (columns != null)
                {
                    foreach (DataColumn dc in columns)
                    {
                        var col = new DataGridViewTextBoxColumn
                        {
                            Name = dc.ColumnName,
                            HeaderText = dc.ColumnName,
                            ReadOnly = true,
                            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                        };
                        _grid.Columns.Add(col);
                    }
                }

                if (pairs == null || pairs.Count == 0)
                {
                    return;
                }

                var changedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pair in pairs)
                {
                    int rowIndex = _grid.Rows.Add();
                    var row = _grid.Rows[rowIndex];
                    row.Tag = pair;

                    // Разрешаем Apply для всех типов diff, но поведение по умолчанию разное
                    bool allowApply = true;
                    bool isChecked = false;

                    switch (pair.DiffType)
                    {
                        case RowDiffType.OnlyInSource:
                        case RowDiffType.Different:
                            isChecked = true;
                            break;

                        case RowDiffType.OnlyInTarget:
                            // удаление потенциально опасно, поэтому по умолчанию НЕ отмечаем
                            isChecked = false;
                            break;
                    }

                    row.Cells["Apply"].Value = isChecked;
                    row.Cells["Apply"].ReadOnly = !allowApply;

                    string statusText;
                    switch (pair.DiffType)
                    {
                        case RowDiffType.OnlyInSource:
                            statusText = "Только в источнике (добавить)";
                            break;
                        case RowDiffType.OnlyInTarget:
                            statusText = "Только в приёмнике (удалить)";
                            break;
                        case RowDiffType.Different:
                            statusText = "Отличается (обновить)";
                            break;
                        default:
                            statusText = "";
                            break;
                    }
                    row.Cells["Status"].Value = statusText;

                    var displayRow = pair.SourceRow ?? pair.TargetRow;
                    if (displayRow != null && columns != null)
                    {
                        foreach (DataColumn dc in columns)
                        {
                            row.Cells[dc.ColumnName].Value = displayRow[dc.ColumnName];
                        }
                    }

                    switch (pair.DiffType)
                    {
                        case RowDiffType.OnlyInSource:
                            // добавлять в приёмник
                            row.DefaultCellStyle.BackColor = Color.FromArgb(0, 80, 0);      // тёмно-зелёный
                            row.DefaultCellStyle.ForeColor = Color.White;
                            break;

                        case RowDiffType.OnlyInTarget:
                            // только в приёмнике, мы не трогаем
                            row.DefaultCellStyle.BackColor = Color.FromArgb(80, 80, 0);     // тёмно-жёлто-оливковый
                            row.DefaultCellStyle.ForeColor = Color.White;
                            break;

                        case RowDiffType.Different:
                            // есть и там, и там, но значения отличаются
                            row.DefaultCellStyle.BackColor = Color.FromArgb(90, 40, 40);
                            row.DefaultCellStyle.ForeColor = Color.White;

                            if (pair.SourceRow != null && pair.TargetRow != null &&
                                _diffBoldFont != null && columns != null)
                            {
                                foreach (DataColumn dc in columns)
                                {
                                    var colName = dc.ColumnName;

                                    object v1 = (pair.SourceRow.Table.Columns.Contains(colName) ? pair.SourceRow[colName] : DBNull.Value);
                                    object v2 = (pair.TargetRow.Table.Columns.Contains(colName) ? pair.TargetRow[colName] : DBNull.Value);

                                    bool isNull1 = (v1 == null || v1 == DBNull.Value);
                                    bool isNull2 = (v2 == null || v2 == DBNull.Value);

                                    // оба NULL → отличий нет
                                    if (isNull1 && isNull2)
                                        continue;

                                    // отличия есть, если:
                                    //  - один NULL, другой нет
                                    //  - или оба не NULL, но значения разные
                                    bool different = isNull1 != isNull2 || !Equals(v1, v2);
                                    if (!different)
                                        continue;

                                    var cell = row.Cells[colName];

                                    // жирный шрифт для отличающихся ячеек
                                    cell.Style.Font = _diffBoldFont;
                                    cell.Style.ForeColor = Color.Yellow;

                                    // запоминаем, что в этом столбце есть отличия
                                    changedColumns.Add(colName);

                                    // tooltip...
                                    string v1Text = isNull1 ? "<NULL>" : Convert.ToString(v1);
                                    string v2Text = isNull2 ? "<NULL>" : Convert.ToString(v2);

                                    cell.ToolTipText =
                                        "Источник : " + v1Text + Environment.NewLine +
                                        "Приёмник: " + v2Text;
                                }
                            }
                            break;
                    }
                }

                // Запрещаем автоматическую сортировку по заголовкам — иначе падаем на смеси типов
                foreach (DataGridViewColumn column in _grid.Columns)
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                }

                // Подсветка заголовков столбцов, в которых есть отличия
                foreach (DataGridViewColumn column in _grid.Columns)
                {
                    // служебные колонки Apply/Status не подсвечиваем
                    if (column.Name == "Apply" || column.Name == "Status")
                        continue;

                    if (changedColumns.Contains(column.Name))
                    {
                        // Подсветка шапки для изменённых колонок
                        column.HeaderCell.Style.BackColor = Color.Coral;
                        column.HeaderCell.Style.ForeColor = Color.Yellow;
                        column.HeaderCell.Style.Font = _diffBoldFont;
                    }
                    else
                    {
                        // Остальные — стандартные цвета
                        column.HeaderCell.Style.BackColor = _grid.ColumnHeadersDefaultCellStyle.BackColor;
                        column.HeaderCell.Style.ForeColor = _grid.ColumnHeadersDefaultCellStyle.ForeColor;
                        column.HeaderCell.Style.Font = _grid.ColumnHeadersDefaultCellStyle.Font;
                    }
                }

                // Вместо постоянного авторазмера — один раз подогнать ширину
                if (_grid.Columns.Count > 0)
                {
                    _grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
                }
            }
            finally
            {
                // Возвращаем прежние режимы авторазмера
                _grid.AutoSizeColumnsMode = prevAutoSizeColumnsMode;
                _grid.AutoSizeRowsMode = prevAutoSizeRowsMode;

                _grid.ResumeLayout();

                sw.Stop();
                AppLogger.Info(
                    $"Отрисовка diff для таблицы '{tableName}': " +
                    $"{sw.Elapsed.TotalMilliseconds:F0} мс, строк diff: {pairs?.Count ?? 0}, столбцов: {columns?.Count ?? 0}.");
            }
        }

        /// <summary>
        /// Сравнение двух RowPair по набору ключевых столбцов.
        /// </summary>
        private sealed class RowPairKeyComparer : System.Collections.Generic.IComparer<RowPair>
        {
            private readonly string[] _keyColumns;
            private readonly DataColumnCollection _columns;

            public RowPairKeyComparer(string[] keyColumns, DataColumnCollection columns)
            {
                _keyColumns = keyColumns;
                _columns = columns;
            }

            public int Compare(RowPair x, RowPair y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                foreach (var key in _keyColumns)
                {
                    var col = _columns[key];
                    if (col == null)
                        continue;

                    var v1 = x?.SourceRow?[key] ?? x?.TargetRow?[key];
                    var v2 = y?.SourceRow?[key] ?? y?.TargetRow?[key];

                    int result = System.Collections.Comparer.DefaultInvariant.Compare(v1, v2);
                    if (result != 0)
                        return result;
                }

                return 0;
            }
        }
    }
}
