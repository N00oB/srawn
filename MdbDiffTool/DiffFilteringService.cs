using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MdbDiffTool
{
    /// <summary>
    /// Фильтрация diff-грида по значениям колонок + режим "только изменённые столбцы".
    /// Вынесено из Form1, чтобы разгрузить UI-код.
    /// </summary>
    internal sealed class DiffFilteringService
    {
        private readonly Dictionary<string, string> _filters =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private enum ColumnFilterMode
        {
            Equals,
            Contains
        }

        private readonly Dictionary<string, ColumnFilterMode> _modes =
            new Dictionary<string, ColumnFilterMode>(StringComparer.OrdinalIgnoreCase);

        public bool ShowOnlyChangedColumns { get; private set; }

        public bool HasAnyFilters => _filters.Count > 0 || _modes.Count > 0;

        public void SetShowOnlyChangedColumns(bool enabled)
        {
            ShowOnlyChangedColumns = enabled;
        }

        public void SetEquals(string columnName, string value)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return;

            _filters[columnName] = value ?? string.Empty;
            _modes[columnName] = ColumnFilterMode.Equals;
        }

        public void SetContains(string columnName, string value)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return;

            if (string.IsNullOrWhiteSpace(value))
            {
                Remove(columnName);
                return;
            }

            _filters[columnName] = value.Trim();
            _modes[columnName] = ColumnFilterMode.Contains;
        }

        public void Remove(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return;

            _filters.Remove(columnName);
            _modes.Remove(columnName);
        }

        public void ClearAll()
        {
            _filters.Clear();
            _modes.Clear();
        }

        public void Apply(DataGridView grid)
        {
            if (grid == null || grid.Columns.Count == 0)
                return;

            grid.SuspendLayout();
            try
            {
                ApplyChangedColumnsVisibility(grid);
                ApplyRowFilters(grid);
            }
            finally
            {
                grid.ResumeLayout();
            }
        }

        private void ApplyChangedColumnsVisibility(DataGridView grid)
        {
            if (!ShowOnlyChangedColumns)
            {
                foreach (DataGridViewColumn column in grid.Columns)
                    column.Visible = true;
                return;
            }

            foreach (DataGridViewColumn column in grid.Columns)
            {
                // служебные колонки всегда показываем
                if (IsServiceColumn(column.Name))
                {
                    column.Visible = true;
                    continue;
                }

                // DiffGridBuilder подсвечивает изменённые столбцы через BackColor = Color.Coral
                var backColor = column.HeaderCell.Style.BackColor;
                bool isChanged = backColor == Color.Coral;

                column.Visible = isChanged;
            }
        }

        private void ApplyRowFilters(DataGridView grid)
        {
            if (_filters.Count == 0)
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (!row.IsNewRow)
                        row.Visible = true;
                }
                return;
            }

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                {
                    row.Visible = false;
                    continue;
                }

                bool visible = true;

                foreach (var kvp in _filters)
                {
                    var colName = kvp.Key;
                    var filterValue = kvp.Value ?? string.Empty;

                    if (!grid.Columns.Contains(colName))
                        continue;

                    var cell = row.Cells[colName];
                    string text = Convert.ToString(cell.Value) ?? string.Empty;

                    ColumnFilterMode mode;
                    if (!_modes.TryGetValue(colName, out mode))
                        mode = ColumnFilterMode.Equals;

                    bool match;

                    if (mode == ColumnFilterMode.Equals)
                    {
                        match = string.Equals(text, filterValue, StringComparison.Ordinal);
                    }
                    else
                    {
                        match = text.IndexOf(filterValue, StringComparison.OrdinalIgnoreCase) >= 0;
                    }

                    if (!match)
                    {
                        visible = false;
                        break;
                    }
                }

                row.Visible = visible;
            }
        }

        public static bool IsServiceColumn(string columnName)
        {
            return string.Equals(columnName, "Apply", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(columnName, "Status", StringComparison.OrdinalIgnoreCase);
        }
    }
}
