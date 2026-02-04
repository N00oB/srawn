using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace MdbDiffTool
{
    public partial class ValueDiffForm : Form
    {
        private string[] _sourceLines = Array.Empty<string>();
        private string[] _targetLines = Array.Empty<string>();

        private Color _srcBackColor;
        private Color _srcForeColor;
        private Color _tgtBackColor;
        private Color _tgtForeColor;

        // Цвета для подсветки отличий
        private readonly Color _diffDeletedBackColor = Color.FromArgb(180, 50, 50);
        private readonly Color _diffInsertedBackColor = Color.FromArgb(50, 130, 50);
        private readonly Color _diffForeColor = Color.Yellow;

        // Защита от OOM/зависаний: LCS использует матрицу (m+1)*(n+1).
        // Порог подбирается так, чтобы подсветка оставалась быстрой и безопасной.
        private const long MAX_LCS_CELLS = 2_000_000;

        private bool _lastInlineDiffSimplified;
        private bool _anyInlineDiffSimplified;
        private string _baseHeaderText;

        private enum InlineSegmentKind
        {
            Equal,
            Deleted,
            Inserted
        }

        private sealed class InlineSegment
        {
            public InlineSegmentKind Kind;
            public string Text;
        }

        public ValueDiffForm()
        {
            InitializeComponent();

            // Связываем панели для синхронного скролла
            txtSource.SyncPartner = txtTarget;
            txtTarget.SyncPartner = txtSource;

            var mono = new Font(FontFamily.GenericMonospace, 9f);
            txtSource.Font = mono;
            txtTarget.Font = mono;

            // После UiTheme.ApplyDark(this) берём реальные цвета
            _srcBackColor = txtSource.BackColor;
            _srcForeColor = txtSource.ForeColor;
            _tgtBackColor = txtTarget.BackColor;
            _tgtForeColor = txtTarget.ForeColor;

            txtSource.HideSelection = false;
            txtTarget.HideSelection = false;

            EnableDoubleBuffering(dgvLineDiff);
        }

        public void SetData(
            string tableName,
            string columnName,
            string keyDescription,
            string sourceText,
            string targetText)
        {
            lblHeader.Text =
                $"Таблица: {tableName} | Колонка: {columnName} | Ключ: {keyDescription}";
            _baseHeaderText = lblHeader.Text;
            _anyInlineDiffSimplified = false;

            lblSourceTitle.Text = "Источник";
            lblTargetTitle.Text = "Приёмник";

            // Нормализуем переводы строк
            var srcNorm = NormalizeNewlines(sourceText) ?? string.Empty;
            var tgtNorm = NormalizeNewlines(targetText) ?? string.Empty;

            // Разбиваем на строки
            var srcLines = srcNorm.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var tgtLines = tgtNorm.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            // Делаем длины одинаковыми, дополняя пустыми строками
            int maxLines = Math.Max(srcLines.Length, tgtLines.Length);

            if (srcLines.Length < maxLines)
                Array.Resize(ref srcLines, maxLines);
            if (tgtLines.Length < maxLines)
                Array.Resize(ref tgtLines, maxLines);

            for (int i = 0; i < srcLines.Length; i++)
                srcLines[i] ??= string.Empty;

            for (int i = 0; i < tgtLines.Length; i++)
                tgtLines[i] ??= string.Empty;

            // Сохраняем в поля, чтобы использовать в гриде и подсветке
            _sourceLines = srcLines;
            _targetLines = tgtLines;

            // Собираем текст для RichTextBox-ов из ВЫРОВНЕННЫХ массивов
            txtSource.Text = string.Join(Environment.NewLine, _sourceLines);
            txtTarget.Text = string.Join(Environment.NewLine, _targetLines);

            // Таб "Строки" строим сразу из массивов
            BuildLineDiffFromArrays();

            // Подсветка всех отличий во вкладке "Текст"
            RebuildAllInlineHighlights();
        }
        private void BuildLineDiffFromArrays()
        {
            var mono = new Font(FontFamily.GenericMonospace, 9f);

            dgvLineDiff.SuspendLayout();
            try
            {
                dgvLineDiff.Columns.Clear();
                dgvLineDiff.Rows.Clear();

                dgvLineDiff.DefaultCellStyle.Font = mono;

                var colIndex = new DataGridViewTextBoxColumn
                {
                    Name = "LineIndex",
                    HeaderText = "№",
                    ReadOnly = true,
                    Width = 60
                };
                dgvLineDiff.Columns.Add(colIndex);

                var colSrc = new DataGridViewTextBoxColumn
                {
                    Name = "SourceLine",
                    HeaderText = "Источник",
                    ReadOnly = true
                };
                dgvLineDiff.Columns.Add(colSrc);

                var colTgt = new DataGridViewTextBoxColumn
                {
                    Name = "TargetLine",
                    HeaderText = "Приёмник",
                    ReadOnly = true
                };
                dgvLineDiff.Columns.Add(colTgt);

                int maxLines = Math.Max(_sourceLines?.Length ?? 0, _targetLines?.Length ?? 0);

                for (int i = 0; i < maxLines; i++)
                {
                    string srcLine = (i < (_sourceLines?.Length ?? 0)) ? _sourceLines[i] : string.Empty;
                    string tgtLine = (i < (_targetLines?.Length ?? 0)) ? _targetLines[i] : string.Empty;

                    int rowIndex = dgvLineDiff.Rows.Add();
                    var row = dgvLineDiff.Rows[rowIndex];

                    row.Cells["LineIndex"].Value = i + 1;
                    row.Cells["SourceLine"].Value = srcLine;
                    row.Cells["TargetLine"].Value = tgtLine;

                    if (!string.Equals(srcLine, tgtLine, StringComparison.Ordinal))
                    {
                        row.DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(90, 40, 40);
                        row.DefaultCellStyle.ForeColor = System.Drawing.Color.White;
                    }
                }

                if (!string.IsNullOrEmpty(_baseHeaderText))
                {
                    lblHeader.Text = _anyInlineDiffSimplified
                        ? (_baseHeaderText + " | Упрощённое сравнение: длинные строки")
                        : _baseHeaderText;
                }

            }
            finally
            {
                dgvLineDiff.ResumeLayout();
            }
        }

        /// <summary>
        /// Построить список фрагментов (Equal / Deleted / Inserted) для двух строк.
        /// </summary>
        private List<InlineSegment> BuildInlineDiffSegments(string oldText, string newText)
        {
            oldText ??= string.Empty;
            newText ??= string.Empty;

            int m = oldText.Length;
            int n = newText.Length;

            _lastInlineDiffSimplified = false;

            // Если строки очень длинные, матрица LCS может занять гигабайты памяти.
            // Переходим в упрощённый режим, чтобы не зависать и не падать по OOM.
            long cells = (long)(m + 1) * (n + 1);
            if (cells > MAX_LCS_CELLS)
            {
                _lastInlineDiffSimplified = true;

                // Если строки одинаковые — считаем их равными.
                if (string.Equals(oldText, newText, StringComparison.Ordinal))
                {
                    return new List<InlineSegment>
                    {
                        new InlineSegment { Kind = InlineSegmentKind.Equal, Text = oldText }
                    };
                }

                var simple = new List<InlineSegment>();
                if (!string.IsNullOrEmpty(oldText))
                    simple.Add(new InlineSegment { Kind = InlineSegmentKind.Deleted, Text = oldText });
                if (!string.IsNullOrEmpty(newText))
                    simple.Add(new InlineSegment { Kind = InlineSegmentKind.Inserted, Text = newText });

                if (simple.Count == 0)
                {
                    simple.Add(new InlineSegment { Kind = InlineSegmentKind.Equal, Text = string.Empty });
                }

                return simple;
            }

                        var lcs = new int[m + 1, n + 1];

            for (int i = m - 1; i >= 0; i--)
            {
                for (int j = n - 1; j >= 0; j--)
                {
                    if (oldText[i] == newText[j])
                        lcs[i, j] = lcs[i + 1, j + 1] + 1;
                    else
                        lcs[i, j] = Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                }
            }

            var result = new List<InlineSegment>();

            var eq = new StringBuilder();
            var del = new StringBuilder();
            var ins = new StringBuilder();

            void FlushEqual()
            {
                if (eq.Length > 0)
                {
                    result.Add(new InlineSegment
                    {
                        Kind = InlineSegmentKind.Equal,
                        Text = eq.ToString()
                    });
                    eq.Clear();
                }
            }

            void FlushDeleted()
            {
                if (del.Length > 0)
                {
                    result.Add(new InlineSegment
                    {
                        Kind = InlineSegmentKind.Deleted,
                        Text = del.ToString()
                    });
                    del.Clear();
                }
            }

            void FlushInserted()
            {
                if (ins.Length > 0)
                {
                    result.Add(new InlineSegment
                    {
                        Kind = InlineSegmentKind.Inserted,
                        Text = ins.ToString()
                    });
                    ins.Clear();
                }
            }

            int ii = 0, jj = 0;
            while (ii < m && jj < n)
            {
                if (oldText[ii] == newText[jj])
                {
                    FlushDeleted();
                    FlushInserted();
                    eq.Append(oldText[ii]);
                    ii++;
                    jj++;
                }
                else if (lcs[ii + 1, jj] >= lcs[ii, jj + 1])
                {
                    FlushEqual();
                    FlushInserted();
                    del.Append(oldText[ii]);
                    ii++;
                }
                else
                {
                    FlushEqual();
                    FlushDeleted();
                    ins.Append(newText[jj]);
                    jj++;
                }
            }

            while (ii < m)
            {
                FlushEqual();
                FlushInserted();
                del.Append(oldText[ii]);
                ii++;
            }

            while (jj < n)
            {
                FlushEqual();
                FlushDeleted();
                ins.Append(newText[jj]);
                jj++;
            }

            FlushEqual();
            FlushDeleted();
            FlushInserted();

            return result;
        }

        /// <summary>
        /// Подсветить отличия во ВСЕХ строках во вкладке "Текст".
        /// </summary>
        private void RebuildAllInlineHighlights()
        {
            txtSource.SuspendLayout();
            txtTarget.SuspendLayout();
            try
            {
                // Полный сброс подсветки
                ClearHighlight(txtSource);
                ClearHighlight(txtTarget);

                if (_sourceLines == null || _targetLines == null)
                    return;

                int maxLines = Math.Max(_sourceLines.Length, _targetLines.Length);

                for (int lineIndex = 0; lineIndex < maxLines; lineIndex++)
                {
                    string sourceLine = lineIndex < _sourceLines.Length
                        ? _sourceLines[lineIndex]
                        : string.Empty;

                    string targetLine = lineIndex < _targetLines.Length
                        ? _targetLines[lineIndex]
                        : string.Empty;

                    if (string.Equals(sourceLine, targetLine, StringComparison.Ordinal))
                        continue;

                    var segments = BuildInlineDiffSegments(sourceLine, targetLine);

                    if (_lastInlineDiffSimplified)
                        _anyInlineDiffSimplified = true;

                    int srcBase = GetFirstCharIndexSafe(txtSource, lineIndex);
                    int tgtBase = GetFirstCharIndexSafe(txtTarget, lineIndex);

                    int srcPos = srcBase;
                    int tgtPos = tgtBase;

                    bool hasDeleted = false;
                    bool hasInserted = false;

                    foreach (var seg in segments)
                    {
                        switch (seg.Kind)
                        {
                            case InlineSegmentKind.Equal:
                                srcPos += seg.Text.Length;
                                tgtPos += seg.Text.Length;
                                break;

                            case InlineSegmentKind.Deleted:
                                if (seg.Text.Length > 0 && srcPos >= 0)
                                {
                                    hasDeleted = true;
                                    txtSource.Select(srcPos, seg.Text.Length);
                                    txtSource.SelectionBackColor = _diffDeletedBackColor;
                                    txtSource.SelectionColor = _diffForeColor;
                                }
                                srcPos += seg.Text.Length;
                                break;

                            case InlineSegmentKind.Inserted:
                                if (seg.Text.Length > 0 && tgtPos >= 0)
                                {
                                    hasInserted = true;
                                    txtTarget.Select(tgtPos, seg.Text.Length);
                                    txtTarget.SelectionBackColor = _diffInsertedBackColor;
                                    txtTarget.SelectionColor = _diffForeColor;
                                }
                                tgtPos += seg.Text.Length;
                                break;
                        }
                    }

                    // Fallback: если не нашли ни вставок, ни удалений,
                    // а строки при этом отличаются — подсветим строку целиком.
                    if (!hasDeleted && !hasInserted &&
                        !string.Equals(sourceLine, targetLine, StringComparison.Ordinal))
                    {
                        if (srcBase >= 0 && sourceLine.Length > 0)
                        {
                            txtSource.Select(srcBase, sourceLine.Length);
                            txtSource.SelectionBackColor = _diffDeletedBackColor;
                            txtSource.SelectionColor = _diffForeColor;
                        }

                        if (tgtBase >= 0 && targetLine.Length > 0)
                        {
                            txtTarget.Select(tgtBase, targetLine.Length);
                            txtTarget.SelectionBackColor = _diffInsertedBackColor;
                            txtTarget.SelectionColor = _diffForeColor;
                        }
                    }
                }

                // Убираем активное выделение, оставляем только форматирование
                txtSource.SelectionStart = 0;
                txtSource.SelectionLength = 0;

                txtTarget.SelectionStart = 0;
                txtTarget.SelectionLength = 0;
            }
            finally
            {
                txtSource.ResumeLayout();
                txtTarget.ResumeLayout();
            }
        }

        private int GetFirstCharIndexSafe(RichTextBox rtb, int lineIndex)
        {
            if (lineIndex < 0)
                return -1;

            if (lineIndex >= rtb.Lines.Length)
                return rtb.TextLength;

            return rtb.GetFirstCharIndexFromLine(lineIndex);
        }

        private void ClearHighlight(RichTextBox rtb)
        {
            int savedStart = rtb.SelectionStart;
            int savedLength = rtb.SelectionLength;

            rtb.Select(0, rtb.TextLength);
            rtb.SelectionBackColor = rtb.BackColor;
            rtb.SelectionColor = rtb.ForeColor;

            rtb.Select(savedStart, savedLength);
        }

        private void ScrollToLine(RichTextBox rtb, int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= rtb.Lines.Length)
                return;

            int charIndex = rtb.GetFirstCharIndexFromLine(lineIndex);
            if (charIndex < 0)
                return;

            rtb.SelectionStart = charIndex;
            rtb.SelectionLength = 0;
            rtb.ScrollToCaret();
        }

        /// <summary>
        /// Щёлк по строке в гриде: скроллим ОБЕ панели к этой строке.
        /// </summary>
        private void dgvLineDiff_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvLineDiff.CurrentRow == null)
                return;

            int lineIndex = dgvLineDiff.CurrentRow.Index;

            ScrollToLine(txtSource, lineIndex);
            ScrollToLine(txtTarget, lineIndex);
        }

        /// <summary>
        /// Показ формы для конкретной ячейки.
        /// </summary>
        public static void ShowForCell(
            IWin32Window owner,
            string tableName,
            string columnName,
            string keyDescription,
            string sourceText,
            string targetText)
        {
            using (var f = new ValueDiffForm())
            {
                f.SetData(tableName, columnName, keyDescription, sourceText, targetText);
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowDialog(owner);
            }
        }
        private void dgvLineDiff_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            int lineIndex = e.RowIndex;

            // Переключаемся на вкладку "Текст"
            if (tabMain.SelectedTab != tabPageText)
                tabMain.SelectedTab = tabPageText;

            // И сразу скроллим обе панели к нужной строке
            ScrollToLine(txtSource, lineIndex);
            ScrollToLine(txtTarget, lineIndex);
        }

        private void EnableDoubleBuffering(DataGridView dgv)
        {
            var prop = typeof(DataGridView).GetProperty(
                "DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (prop != null)
                prop.SetValue(dgv, true, null);
        }
        private static string NormalizeNewlines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = text.Replace("\r\n", "\n")
                       .Replace("\r", "\n");

            return text.Replace("\n", Environment.NewLine);
        }
    }
}
