using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MdbDiffTool.Core;

namespace MdbDiffTool
{
    public partial class Form1 : Form
    {
        private SplitContainer _splitMain;
        private Panel _tablesActionsPanel;
        private bool _splitMainInitialized;
        private bool _tablesPaneInitialized;
        private bool _browseHintLayoutInitialized;
        private readonly Dictionary<Button, Label> _browseHintLabels = new Dictionary<Button, Label>();

        private string _sourcePath; // источник
        private string _targetPath; // приёмник
        private bool _isBusy;       // флаг фоновой операции

        private string _configPath;
        private ConfigService _configService;
        private CancellationTokenSource _loadCts;
        private AppConfig _config;

        private readonly IDatabaseProvider _dbProvider;
        private readonly TableComparisonService _comparisonService;
        private readonly TablesLoadController _tablesLoadController;
        private TableCompareController _tableCompareController;
        private readonly ConnectionStringService _connectionStringService;
        private List<TableDiffSummary> _lastSummaries = new List<TableDiffSummary>();
        private DiffGridBuilder _diffGridBuilder;
        private DiffApplyService _diffApplyService;
        private ApplyController _applyController;
        private ContextMenuStrip _diffCellContextMenu;
        private ToolStripMenuItem _miCompareCellValue;
        private Font _diffBoldFont;
        private int _lastDiffCellRowIndex = -1;
        private int _lastDiffCellColumnIndex = -1;
        // Фильтрация diff-таблицы и режим "только изменённые столбцы"
        private readonly DiffFilteringService _diffFilteringService = new DiffFilteringService();

        private ToolStripMenuItem _miFilterByThisValue;
        private ToolStripMenuItem _miClearFilterForColumn;
        private ToolStripMenuItem _miClearAllFilters;
        private ToolStripMenuItem _miShowOnlyChangedColumns;
        private ToolStripMenuItem _miFilterColumnContains;


        // Список таблиц: имя + количество различий
        private sealed class TableItem
        {
            public string Name { get; set; }
            public int DiffCount { get; set; }

            public override string ToString()
            {
                return DiffCount > 0 ? Name + " (" + DiffCount + ")" : Name;
            }
        }

        

private string GetSelectedTableNameForUi()
{
    return clbTables.SelectedItem is TableItem ti ? ti.Name : null;
}

private List<string> GetCheckedTableNamesForUi()
{
    var tableNames = new List<string>();

    for (int i = 0; i < clbTables.Items.Count; i++)
    {
        if (!clbTables.GetItemChecked(i))
            continue;

        if (clbTables.Items[i] is TableItem ti)
            tableNames.Add(ti.Name);
    }

    return tableNames;
}

private sealed class RowPairKeyComparer : IComparer<RowPair>
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

                var rowX = x.SourceRow ?? x.TargetRow;
                var rowY = y.SourceRow ?? y.TargetRow;
                if (rowX == null || rowY == null) return 0;

                if (_keyColumns == null)
                    return 0;

                for (int i = 0; i < _keyColumns.Length; i++)
                {
                    var colName = _keyColumns[i];
                    if (!_columns.Contains(colName))
                        continue;

                    var dc = _columns[colName];
                    object v1 = rowX[colName];
                    object v2 = rowY[colName];

                    int cmp = CompareValuesForSort(v1, v2, dc.DataType);
                    if (cmp != 0)
                        return cmp;
                }

                return 0;
            }
        }

        public Form1()
        {
            InitializeComponent();
            UiTheme.ApplyDark(this);
            // Лэйаут: делаем правую панель "Таблицы" регулируемой по ширине.
            // Это нужно выполнить ДО InitUi(), потому что там настраиваются события и подсказки.
            SetupMainSplitLayout();

            // Конфиг должен храниться в профиле пользователя (LocalAppData),
            // иначе в защищённых папках он не сохранится.
            _configPath = AppPaths.ConfigPath;
            _configService = new ConfigService(_configPath);
            _config = _configService.Load();

            // создание универсального провайдера
            _dbProvider = new MultiDatabaseProvider();
            // создание сервиса сравнения таблиц
            _comparisonService = new TableComparisonService(_dbProvider);
            _tablesLoadController = new TablesLoadController(_dbProvider, _comparisonService);
            // сервис применения diff-изменений
            _diffApplyService = new DiffApplyService(_dbProvider);

            _applyController = new ApplyController(_diffApplyService, _dbProvider);

            // билдер строк подключения (вынесено из Form1)
            _connectionStringService = new ConnectionStringService();

            InitUi();
            ApplyConfigToUi();
            AlignStatusWithProgress();
            this.Resize += (s, e) => AlignStatusWithProgress();

            // Восстановление размеров/позиции окна и ширины панели таблиц из конфига.
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            TryRestoreWindowPlacementFromConfig();

            // Разделитель лучше применять после Layout, но до первого отображения.
            // Если не получится (например, из-за ранней инициализации), применим ещё раз в Shown.
            TryRestoreSplitDistanceFromConfig();
            this.Shown += (s, ee) => TryRestoreSplitDistanceFromConfig();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                PersistWindowPlacementToConfig();
                PersistSplitDistanceToConfig();
                SaveConfig();
            }
            catch
            {
                // конфиг не должен мешать закрытию приложения
            }
        }

        private void SetupMainSplitLayout()
        {
            try
            {
                if (_splitMainInitialized)
                    return;

                // Разметка задаётся в Designer (splitMain + groupBox2). Здесь только
                // привязка к split-контейнеру и сохранение/восстановление ширины панели таблиц.
                _splitMain = splitMain;
                if (_splitMain == null)
                {
                    _splitMain = Controls.Find("splitMain", true)
                        .OfType<SplitContainer>()
                        .FirstOrDefault();
                }

                if (_splitMain == null)
                    return;

                this.Shown += (s, e) => ApplyDefaultSplitDistanceOnce();

                _splitMain.SplitterMoved += (s, e) =>
                {
                    _splitMain.Tag = "user";
                    PersistSplitDistanceToConfig();
                };

                _splitMainInitialized = true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка при инициализации лэйаута главного окна.", ex);
            }
        }

private static void MoveToPanel(Control c, Control panel)
        {
            if (c == null || panel == null) return;
            var parent = c.Parent;
            if (parent != null)
                parent.Controls.Remove(c);
            panel.Controls.Add(c);
        }

        private void FitLeftPanelControls()
        {
            try
            {
                if (_splitMain == null) return;
                var p = _splitMain.Panel1;
                int margin = 12;
                int w = Math.Max(200, p.ClientSize.Width - margin * 2);

                if (groupBox1 != null)
                {
                    groupBox1.Left = margin;
                    groupBox1.Width = w;
                }

                if (dgvDiff != null)
                {
                    dgvDiff.Left = margin;
                    dgvDiff.Width = w;
                }

                if (progressBar != null)
                {
                    progressBar.Left = margin;
                    progressBar.Width = Math.Max(200, w - 268); // оставить место под кнопки справа
                }

                // Кнопки справа внизу
                if (btnToggleApplyAll != null)
                {
                    int applyW = btnApplySelected?.Width ?? 0;
                    btnToggleApplyAll.Left = Math.Max(margin, p.ClientSize.Width - btnToggleApplyAll.Width - 12 - applyW - 6);
                }
                if (btnApplySelected != null)
                    btnApplySelected.Left = Math.Max(margin, p.ClientSize.Width - btnApplySelected.Width - 12);
            }
            catch
            {
                // игнорируем
            }
        }

        private void ApplyDefaultSplitDistanceOnce()
        {
            try
            {
                if (_splitMain == null) return;

                // Если в конфиге уже сохранено положение разделителя — используем его.
                if (_config != null && _config.UiSplitMainDistance > 0)
                {
                    // ВАЖНО: имя переменной не должно конфликтовать с "dist" ниже (иначе CS0136).
                    int cfgDist = _config.UiSplitMainDistance;
                    cfgDist = Math.Max(_splitMain.Panel1MinSize, cfgDist);
                    cfgDist = Math.Min(cfgDist, _splitMain.Width - _splitMain.Panel2MinSize);
                    _splitMain.SplitterDistance = cfgDist;
                    _splitMain.Tag = "user";
                    return;
                }

                // Если пользователь уже двигал в этой сессии — не трогаем.
                if (_splitMain.Tag is string s && s == "user") return;

                // Исторически clbTables был ~141px. Делаем +30% по умолчанию.
                int desiredClb = (int)Math.Round(141 * 1.3);
                int actionsW = _tablesActionsPanel?.Width ?? 200;
                int desiredRight = Math.Max(360, desiredClb + actionsW + 30);

                int dist = Math.Max(_splitMain.Panel1MinSize, _splitMain.Width - desiredRight);
                dist = Math.Min(dist, _splitMain.Width - _splitMain.Panel2MinSize);
                _splitMain.SplitterDistance = dist;

                _splitMain.Tag = "default";
            }
            catch
            {
                // игнорируем
            }
        }

        private void PersistSplitDistanceToConfig()
        {
            try
            {
                if (_config == null || _splitMain == null)
                    return;

                // Сохраняем всегда текущее положение разделителя.
                _config.UiSplitMainDistance = _splitMain.SplitterDistance;
            }
            catch
            {
                // игнорируем
            }
        }

        private void TryRestoreSplitDistanceFromConfig()
        {
            try
            {
                if (_config == null || _splitMain == null)
                    return;

                if (_config.UiSplitMainDistance <= 0)
                    return;

                int dist = _config.UiSplitMainDistance;
                dist = Math.Max(_splitMain.Panel1MinSize, dist);
                dist = Math.Min(dist, _splitMain.Width - _splitMain.Panel2MinSize);
                _splitMain.SplitterDistance = dist;
                _splitMain.Tag = "user";
            }
            catch
            {
                // игнорируем
            }
        }

        private void PersistWindowPlacementToConfig()
        {
            if (_config == null)
                return;

            try
            {
                // Minimized не сохраняем как состояние восстановления, иначе при старте окно будет "пропадать".
                var state = this.WindowState;
                if (state == FormWindowState.Minimized)
                    state = FormWindowState.Normal;

                Rectangle r = state == FormWindowState.Normal ? this.Bounds : this.RestoreBounds;

                _config.UiWindowX = r.X;
                _config.UiWindowY = r.Y;
                _config.UiWindowWidth = r.Width;
                _config.UiWindowHeight = r.Height;
                _config.UiWindowState = (int)state;
            }
            catch
            {
                // игнорируем
            }
        }

        private void TryRestoreWindowPlacementFromConfig()
        {
            try
            {
                if (_config == null)
                    return;

                if (_config.UiWindowWidth <= 0 || _config.UiWindowHeight <= 0)
                    return;

                var desired = new Rectangle(
                    _config.UiWindowX,
                    _config.UiWindowY,
                    _config.UiWindowWidth,
                    _config.UiWindowHeight);

                // Проверяем, что окно хотя бы частично попадает на любой экран.
                bool visible = false;
                foreach (var s in Screen.AllScreens)
                {
                    if (s.WorkingArea.IntersectsWith(desired))
                    {
                        visible = true;
                        break;
                    }
                }

                if (!visible)
                    return;

                this.StartPosition = FormStartPosition.Manual;
                this.Bounds = desired;

                var ws = _config.UiWindowState;
                if (ws == (int)FormWindowState.Maximized)
                    this.WindowState = FormWindowState.Maximized;
                else
                    this.WindowState = FormWindowState.Normal;
            }
            catch
            {
                // игнорируем
            }
        }

        private void SetupTablesPaneLayout()
        {
            try
            {
                if (_tablesPaneInitialized)
                    return;

                if (groupBox2 == null || clbTables == null || groupBox3 == null || groupBox4 == null || chkShowAllTables == null || btnExcludeTable == null)
                    return;

                // Правая панель с кнопками/настройками
                _tablesActionsPanel = new Panel
                {
                    Name = "panelTablesActions",
                    Dock = DockStyle.Right,
                    Width = 200
                };

                groupBox2.Controls.Add(_tablesActionsPanel);
                _tablesActionsPanel.BringToFront();

                // Переносим элементы управления в правую панель
                groupBox3.Parent = _tablesActionsPanel;
                groupBox4.Parent = _tablesActionsPanel;
                chkShowAllTables.Parent = _tablesActionsPanel;
                btnExcludeTable.Parent = _tablesActionsPanel;

                // Dock-лэйаут
                groupBox3.Dock = DockStyle.Top;
                groupBox4.Dock = DockStyle.Top;
                groupBox3.Height = 92;
                groupBox4.Height = 92;

                btnExcludeTable.Dock = DockStyle.Bottom;
                chkShowAllTables.Dock = DockStyle.Bottom;

                // Список таблиц занимает всё оставшееся место
                clbTables.Dock = DockStyle.Fill;

                _tablesPaneInitialized = true;
            }
            catch
            {
                // игнорируем
            }
        }

        #region Конфиг

        private void SaveConfig()
        {
            _configService.Save(_config);
        }

        private void ApplyConfigToUi()
        {
            try
            {
                if (_config == null)
                    return;

                // Настройки отображения
                if (toolStripMenuItemShowNullEmptyMarkers != null)
                    toolStripMenuItemShowNullEmptyMarkers.Checked = _config.ShowNullEmptyMarkers;

                if (!string.IsNullOrWhiteSpace(_config.LastSourcePath))
                {
                    _sourcePath = ExtractPlainPath(_config.LastSourcePath);
                    txtSourcePath.Text = _config.LastSourcePath;
                }

                if (!string.IsNullOrWhiteSpace(_config.LastTargetPath))
                {
                    _targetPath = ExtractPlainPath(_config.LastTargetPath);
                    txtTargetPath.Text = _config.LastTargetPath;
                }
            }
            catch
            {
                // игнорируем
            }
        }

        private HashSet<string> GetExcludedTablesSet()
        {
            return _configService.GetExcludedTablesSet(_config);
        }

        #endregion

        #region Инициализация UI

        private void InitUi()
        {
            Text = "Сравнятор 3000";

            dgvDiff.AllowUserToAddRows = false;
            dgvDiff.AllowUserToDeleteRows = false;
            dgvDiff.ReadOnly = false;
            dgvDiff.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvDiff.MultiSelect = true;
            dgvDiff.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dgvDiff.AllowUserToAddRows = false;
            dgvDiff.AllowUserToDeleteRows = false;
            dgvDiff.ReadOnly = false;
            dgvDiff.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvDiff.MultiSelect = true;

            // авторазмер столбцов
            dgvDiff.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            dgvDiff.RowHeadersVisible = false;
            // тёмная схема выделения строк/ячеек
            var selBack = Color.FromArgb(70, 90, 120); // синий/серый
            var selFore = Color.White;

            dgvDiff.DefaultCellStyle.SelectionBackColor = selBack;
            dgvDiff.DefaultCellStyle.SelectionForeColor = selFore;

            dgvDiff.RowsDefaultCellStyle.SelectionBackColor = selBack;
            dgvDiff.RowsDefaultCellStyle.SelectionForeColor = selFore;

            dgvDiff.AlternatingRowsDefaultCellStyle.SelectionBackColor = selBack;
            dgvDiff.AlternatingRowsDefaultCellStyle.SelectionForeColor = selFore;

            dgvDiff.RowHeadersDefaultCellStyle.SelectionBackColor = selBack;
            dgvDiff.RowHeadersDefaultCellStyle.SelectionForeColor = selFore;

            dgvDiff.EnableHeadersVisualStyles = false;
            dgvDiff.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 60, 80);
            dgvDiff.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
            dgvDiff.ShowCellToolTips = true; // по умолчанию true, но явно не помешает

            EnableDoubleBuffering(dgvDiff);

            // базовый шрифт и жирный для подсветки отличий по ячейкам
            var baseFont = dgvDiff.DefaultCellStyle.Font ?? dgvDiff.Font;
            _diffBoldFont = new Font(baseFont, FontStyle.Bold);
            _diffGridBuilder = new DiffGridBuilder(
                dgvDiff,
                lblStatus,
                _diffBoldFont,
                () => _config?.ShowNullEmptyMarkers ?? true);

            

            _tableCompareController = new TableCompareController(_comparisonService, _diffGridBuilder, _diffFilteringService, _config);
// Контекстное меню для ячеек diff-таблицы
            _diffCellContextMenu = new ContextMenuStrip();

            // 1) Сравнение значения (уже было)
            _miCompareCellValue = new ToolStripMenuItem("Сравнить значение...");
            _miCompareCellValue.Click += MiCompareCellValue_Click;
            _diffCellContextMenu.Items.Add(_miCompareCellValue);

            _diffCellContextMenu.Items.Add(new ToolStripSeparator());

            // 2) Фильтрация по колонкам
            _miFilterByThisValue = new ToolStripMenuItem("Фильтровать по этому значению");
            _miFilterByThisValue.Click += MiFilterByThisValue_Click;
            _diffCellContextMenu.Items.Add(_miFilterByThisValue);
            
            _miFilterColumnContains = new ToolStripMenuItem("Фильтровать (содержит)...");
            _miFilterColumnContains.Click += MiFilterColumnContains_Click;
            _diffCellContextMenu.Items.Add(_miFilterColumnContains);

            _miClearFilterForColumn = new ToolStripMenuItem("Снять фильтр по этому столбцу");
            _miClearFilterForColumn.Click += MiClearFilterForColumn_Click;
            _diffCellContextMenu.Items.Add(_miClearFilterForColumn);

            _miClearAllFilters = new ToolStripMenuItem("Снять все фильтры");
            _miClearAllFilters.Click += MiClearAllFilters_Click;
            _diffCellContextMenu.Items.Add(_miClearAllFilters);

            _diffCellContextMenu.Items.Add(new ToolStripSeparator());

            // 3) Только столбцы с отличиями
            _miShowOnlyChangedColumns = new ToolStripMenuItem("Показать только столбцы с отличиями");
            _miShowOnlyChangedColumns.CheckOnClick = true;
            _miShowOnlyChangedColumns.Click += MiShowOnlyChangedColumns_Click;
            _diffCellContextMenu.Items.Add(_miShowOnlyChangedColumns);

            dgvDiff.ContextMenuStrip = _diffCellContextMenu;

            // Обработчик для "поймать" ячейку под ПКМ
            dgvDiff.CellMouseDown += DgvDiff_CellMouseDown;

            clbTables.CheckOnClick = true;
            clbTables.HorizontalScrollbar = true;

            if (progressBar != null)
            {
                progressBar.Visible = false;
                progressBar.Minimum = 0;
                progressBar.Value = 0;
                progressBar.Style = ProgressBarStyle.Blocks;
            }

            btnBrowseSource.Click += BtnBrowseSource_Click;
            btnBrowseTarget.Click += BtnBrowseTarget_Click;

            // ПКМ по кнопкам "Обзор" — выбор папки (CSV-набор), ЛКМ — выбор файла как раньше.
            btnBrowseSource.MouseUp += BtnBrowseSource_MouseUp;
            btnBrowseTarget.MouseUp += BtnBrowseTarget_MouseUp;

            SetupBrowseButtonsHints();
            btnLoadTables.Click += BtnLoadTables_Click;
            btnCompareTable.Click += BtnCompareTable_Click;
            btnApplySelected.Click += BtnApplySelected_Click;
            btnApplyWholeTable.Click += BtnApplyWholeTable_Click;
            btnToggleCheckAll.Click += BtnToggleCheckAll_Click;
            btnToggleApplyAll.Click += BtnToggleApplyAll_Click;
            btnExcludeTable.Click += BtnEditExclusions_Click;
            btnCancelLoad.Click += BtnCancelLoad_Click;
            btnCancelLoad.Enabled = false;
            btnSwap.Click += BtnSwap_Click;
            btnDeleteTable.Click += btnDeleteTable_Click;

            clbTables.SelectedIndexChanged += ClbTables_SelectedIndexChanged;

            if (lblStatus != null)
            {
                lblStatus.BackColor = Color.Transparent;
                lblStatus.AutoSize = false;
                lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            }

            btnSwap.Text = "↕";
            btnSwap.Font = new Font(btnSwap.Font.FontFamily, 16, FontStyle.Bold);
        }

        private void BtnBrowseSource_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isBusy) return;
            if (e.Button != MouseButtons.Right) return;
            ShowFolderPickMenu(isSource: true, sender as Control, e.Location);
        }

        private void BtnBrowseTarget_MouseUp(object sender, MouseEventArgs e)
        {
            if (_isBusy) return;
            if (e.Button != MouseButtons.Right) return;
            ShowFolderPickMenu(isSource: false, sender as Control, e.Location);
        }

        private void SetupBrowseButtonsHints()
        {
            try
            {
                var tip = new ToolTip
                {
                    AutoPopDelay = 15000,
                    InitialDelay = 300,
                    ReshowDelay = 100,
                    ShowAlways = true
                };

                const string hint = "ЛКМ — выбрать файл (Access/SQLite/Excel/Libre/CFG и т.д.)\r\n" +
                                    "ПКМ — выбрать папку (CSV / CONFIG)";

                tip.SetToolTip(btnBrowseSource, hint);
                tip.SetToolTip(btnBrowseTarget, hint);

                // Наглядная подпись над кнопками: "ЛКМ/ПКМ" (закрепляем над кнопкой)
                AddBrowseHintLabelNearButton(btnBrowseSource);
                AddBrowseHintLabelNearButton(btnBrowseTarget);

                EnsureBrowseHintLabelsLayout();
            }
            catch
            {
                // подсказки не критичны
            }
        }

        private void AddBrowseHintLabelNearButton(Button button)
        {
            if (button == null) return;
            if (groupBox1 == null) return;

            if (_browseHintLabels.ContainsKey(button))
                return;

            var lbl = new Label
            {
                AutoSize = true,
                Text = "ЛКМ/ПКМ",
                ForeColor = SystemColors.GrayText
            };

            groupBox1.Controls.Add(lbl);
            lbl.BringToFront();

            _browseHintLabels[button] = lbl;
            PositionBrowseHintLabel(button, lbl);
        }

        private void EnsureBrowseHintLabelsLayout()
        {
            if (_browseHintLayoutInitialized) return;
            _browseHintLayoutInitialized = true;

            // Когда форма/группа меняют размер, кнопки "Обзор" сдвигаются,
            // поэтому подписи надо перерисовывать.
            if (groupBox1 != null)
            {
                groupBox1.Resize += (s, e) => RepositionBrowseHintLabels();
                groupBox1.Layout += (s, e) => RepositionBrowseHintLabels();
            }

            if (btnBrowseSource != null)
                btnBrowseSource.LocationChanged += (s, e) => RepositionBrowseHintLabels();
            if (btnBrowseTarget != null)
                btnBrowseTarget.LocationChanged += (s, e) => RepositionBrowseHintLabels();

            RepositionBrowseHintLabels();
        }

        private void RepositionBrowseHintLabels()
        {
            try
            {
                foreach (var kv in _browseHintLabels)
                    PositionBrowseHintLabel(kv.Key, kv.Value);
            }
            catch
            {
                // не критично
            }
        }

        private static void PositionBrowseHintLabel(Button button, Label lbl)
        {
            if (button == null || lbl == null) return;

            // Центрируем над кнопкой
            int x = button.Left + Math.Max(0, (button.Width - lbl.Width) / 2);
            int y = Math.Max(0, button.Top - lbl.Height - 1);
            lbl.Location = new Point(x, y);
        }

        private void SetBusy(bool isBusy, string statusText = null)
        {
            _isBusy = isBusy;

            // groupBox1 НЕ блокируем, для доступности "Отмена"
            groupBox2.Enabled = !isBusy;
            dgvDiff.Enabled = !isBusy;
            btnApplySelected.Enabled = !isBusy;

            UseWaitCursor = isBusy;

            if (!string.IsNullOrWhiteSpace(statusText) && lblStatus != null)
                lblStatus.Text = statusText;
        }

        #endregion
        private void BtnSwap_Click(object sender, EventArgs e)
        {
            if (_isBusy) return;

            // свап путей баз
            var tmpPath = _sourcePath;
            _sourcePath = _targetPath;
            _targetPath = tmpPath;

            var tmpText = txtSourcePath.Text;
            txtSourcePath.Text = txtTargetPath.Text;
            txtTargetPath.Text = tmpText;

            _config.LastSourcePath = _sourcePath;
            _config.LastTargetPath = _targetPath;
            SaveConfig();

            if (lblStatus != null)
                lblStatus.Text = "Базы источника и приёмника поменялись местами. Перезагрузи таблицы при необходимости.";
        }
        private void MiCompareCellValue_Click(object sender, EventArgs e)
        {
            try
            {
                if (_lastDiffCellRowIndex < 0 || _lastDiffCellColumnIndex < 0)
                    return;

                var row = dgvDiff.Rows[_lastDiffCellRowIndex];
                var column = dgvDiff.Columns[_lastDiffCellColumnIndex];

                // Служебные колонки пропускаем
                if (column.Name == "Apply" || column.Name == "Status")
                    return;

                var ctx = dgvDiff.Tag as DiffContext;
                if (ctx == null)
                    return;

                var pair = row.Tag as RowPair;
                if (pair == null)
                    return;

                string tableName = ctx.TableName ?? "<неизвестно>";
                string columnName = column.Name;

                // Собираем описание ключа строки
                string keyDescription = BuildRowKeyDescription(pair, ctx);

                // Берём значения из SourceRow / TargetRow
                object v1 = pair.SourceRow != null
                    ? pair.SourceRow[columnName]
                    : null;
                object v2 = pair.TargetRow != null
                    ? pair.TargetRow[columnName]
                    : null;

                string sourceText = FormatCellValueForDiff(v1);
                string targetText = FormatCellValueForDiff(v2);

                ValueDiffForm.ShowForCell(
                    this,
                    tableName,
                    columnName,
                    keyDescription,
                    sourceText,
                    targetText);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка при открытии подробного сравнения значения ячейки.", ex);
                MessageBox.Show(
                    this,
                    "Ошибка при открытии сравнения значения:\r\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        private static string FormatCellValueForDiff(object value)
        {
            if (value == null || value == DBNull.Value)
                return "NULL";

            // Если это уже строка — возвращаем как есть
            if (value is string s)
                return s.Length == 0 ? "∅" : s;

            // Для остальных типов — ToString()
            return Convert.ToString(value);
        }

        private string BuildRowKeyDescription(RowPair pair, DiffContext ctx)
        {
            try
            {
                if (ctx?.PrimaryKeyColumns != null && ctx.PrimaryKeyColumns.Length > 0)
                {
                    var parts = new List<string>();

                    foreach (var colName in ctx.PrimaryKeyColumns)
                    {
                        object v = pair.SourceRow != null
                            ? pair.SourceRow[colName]
                            : pair.TargetRow != null
                                ? pair.TargetRow[colName]
                                : null;

                        string text = FormatCellValueForDiff(v);
                        parts.Add($"{colName}={text}");
                    }

                    return string.Join("; ", parts);
                }

                // Если PK нет — возьмём 1–3 первых столбца из diff для ориентира
                if (ctx?.SourceColumns != null && ctx.SourceColumns.Count > 0)
                {
                    var parts = new List<string>();
                    int maxCols = Math.Min(3, ctx.SourceColumns.Count);

                    for (int i = 0; i < maxCols; i++)
                    {
                        var dc = ctx.SourceColumns[i];
                        string colName = dc.ColumnName;

                        object v = pair.SourceRow != null
                            ? pair.SourceRow[colName]
                            : pair.TargetRow != null
                                ? pair.TargetRow[colName]
                                : null;

                        string text = FormatCellValueForDiff(v);
                        parts.Add($"{colName}={text}");
                    }

                    return string.Join("; ", parts);
                }
            }
            catch
            {
                // Игнорируем любые ошибки, вернём запасной вариант
            }

            return "<ключ не определён>";
        }
        /// <summary>
        /// Добавить фильтр "равно" по значению выбранной ячейки.
        /// </summary>
        private void MiFilterByThisValue_Click(object sender, EventArgs e)
        {
            if (_lastDiffCellRowIndex < 0 || _lastDiffCellColumnIndex < 0)
                return;

            var row = dgvDiff.Rows[_lastDiffCellRowIndex];
            var column = dgvDiff.Columns[_lastDiffCellColumnIndex];

            if (column == null)
                return;

            string colName = column.Name;

            // служебные колонки не фильтруем
            if (DiffFilteringService.IsServiceColumn(colName))
                return;

            string valueText = Convert.ToString(row.Cells[colName].Value) ?? string.Empty;

            _diffFilteringService.SetEquals(colName, valueText);
            _diffFilteringService.Apply(dgvDiff);
        }

        private void MiClearFilterForColumn_Click(object sender, EventArgs e)
        {
            if (_lastDiffCellColumnIndex < 0)
                return;

            var column = dgvDiff.Columns[_lastDiffCellColumnIndex];
            if (column == null)
                return;

            string colName = column.Name;

            _diffFilteringService.Remove(colName);
            _diffFilteringService.Apply(dgvDiff);
        }

        private void MiClearAllFilters_Click(object sender, EventArgs e)
        {
            if (!_diffFilteringService.HasAnyFilters)
                return;

            _diffFilteringService.ClearAll();
            _diffFilteringService.Apply(dgvDiff);
        }

        private void MiShowOnlyChangedColumns_Click(object sender, EventArgs e)
        {
            // CheckOnClick уже переключил флаг Checked
            _diffFilteringService.SetShowOnlyChangedColumns(_miShowOnlyChangedColumns.Checked);
            _diffFilteringService.Apply(dgvDiff);
        }
        private void MiFilterColumnContains_Click(object sender, EventArgs e)
        {
            if (_lastDiffCellRowIndex < 0 || _lastDiffCellColumnIndex < 0)
                return;

            var row = dgvDiff.Rows[_lastDiffCellRowIndex];
            var column = dgvDiff.Columns[_lastDiffCellColumnIndex];
            if (column == null)
                return;

            string colName = column.Name;

            // служебные колонки не фильтруем
            if (DiffFilteringService.IsServiceColumn(colName))
                return;

            string currentValue = Convert.ToString(row.Cells[colName].Value) ?? string.Empty;

            // спрашиваем подстроку
            string prompt = $"Введите подстроку для фильтрации столбца \"{column.HeaderText}\" (СОДЕРЖИТ):";
            //string input = Interaction.InputBox(prompt, "Фильтр (содержит)", currentValue);
            string input = ShowInputBox("Фильтр (содержит)", prompt, currentValue);

            _diffFilteringService.SetContains(colName, input);
            _diffFilteringService.Apply(dgvDiff);
        }

        private void DgvDiff_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Игнорируем заголовки
                if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
                {
                    _lastDiffCellRowIndex = e.RowIndex;
                    _lastDiffCellColumnIndex = e.ColumnIndex;

                    // Выделим ячейку, чтобы визуально было понятно, с чем работаем
                    dgvDiff.ClearSelection();
                    dgvDiff.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected = true;
                }
                else
                {
                    _lastDiffCellRowIndex = -1;
                    _lastDiffCellColumnIndex = -1;
                }
            }
        }
        /// <summary>
        /// Массовое включение/выключение флага Apply по всем строкам diff.
        /// Если есть хотя бы одна неотмеченная — включаем всем.
        /// Иначе — снимаем у всех.
        /// </summary>
        private void BtnToggleApplyAll_Click(object sender, EventArgs e)
        {
            if (_isBusy) return;
            if (dgvDiff == null || dgvDiff.Rows.Count == 0) return;

            // Если нет колонки Apply – делать нечего
            var applyColumn = dgvDiff.Columns["Apply"];
            if (applyColumn == null)
                return;

            // Проверяем, есть ли хотя бы одна неотмеченная строка,
            // у которой Apply не ReadOnly
            bool hasUnchecked = false;

            foreach (DataGridViewRow row in dgvDiff.Rows)
            {
                if (row.IsNewRow) continue;

                var cell = row.Cells["Apply"];
                if (cell == null || cell.ReadOnly) continue;

                bool val = false;
                object raw = cell.Value;

                if (raw is bool b) val = b;
                else if (raw != null && bool.TryParse(Convert.ToString(raw), out var parsed))
                    val = parsed;

                if (!val)
                {
                    hasUnchecked = true;
                    break;
                }
            }

            bool newValue = hasUnchecked; // если были неотмеченные — включаем всем, иначе снимаем всем

            foreach (DataGridViewRow row in dgvDiff.Rows)
            {
                if (row.IsNewRow) continue;

                var cell = row.Cells["Apply"];
                if (cell == null || cell.ReadOnly) continue;

                cell.Value = newValue;
            }
        }


        #region Выбор файлов

        private void BtnBrowseSource_Click(object sender, EventArgs e)
        {
            if (_isBusy) return;

            const int FilterIndexPostgres = 5;
            const int FilterIndexCfg = 6;
            const int FilterIndexCsvFolder = 7;
            const int FiltersCount = 8;

            // Важно: ЛКМ всегда открывает выбор ФАЙЛА (как раньше).
            // Для выбора папки CSV используйте:
            //  - ПКМ по кнопке "Обзор" (быстро), либо
            //  - пункт фильтра "CSV (папка...)" внутри диалога выбора файла.

            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter =
                    "Access (*.mdb;*.accdb)|*.mdb;*.accdb|" +
                    "SQLite (*.sqlite;*.db;*.db3)|*.sqlite;*.db;*.db3|" +
                    "Excel (*.xlsx;*.xlsm;*.xlam;*.xls;*.xla)|*.xlsx;*.xlsm;*.xlam;*.xls;*.xla|" +
                    "LibreOffice Calc (*.ods)|*.ods|" +
                    "PostgreSQL (строка подключения вручную)|*.*|" +
                    "Конфиг Emicon (*.cfg)|*.cfg|" +
                    "CSV (папка, набор файлов *.csv)|*.*|" +
                    "Все файлы (*.*)|*.*";

                // Важно: пользователи часто работают с одним типом файлов.
                // Запоминаем последнюю выбранную строку фильтра.
                int idx = _config?.LastSourceBrowseFilterIndex ?? 1;
                if (idx < 1) idx = 1;
                // миграция со старого конфига: раньше "Все файлы" было 7,
                // теперь 7 — "CSV (папка)", а "Все файлы" стало 8.
                if (idx == FilterIndexCsvFolder && !string.IsNullOrWhiteSpace(_sourcePath) && File.Exists(_sourcePath))
                    idx = FiltersCount;
                if (idx > FiltersCount) idx = FiltersCount;
                ofd.FilterIndex = idx;

                try
                {
                    if (!string.IsNullOrWhiteSpace(_sourcePath))
                    {
                        if (Directory.Exists(_sourcePath))
                            ofd.InitialDirectory = _sourcePath;
                        else
                        {
                            var dir = Path.GetDirectoryName(_sourcePath);
                            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                                ofd.InitialDirectory = dir;
                        }
                    }
                }
                catch
                {
                    // игнорируем
                }

                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    // сохраняем выбор фильтра
                    _config.LastSourceBrowseFilterIndex = ofd.FilterIndex;

                    // Если пользователь выбрал пункт "PostgreSQL" (5-я строка) —
                    // файл нам не нужен, строка подключения вводится вручную.
                    if (ofd.FilterIndex == FilterIndexPostgres)
                    {
                        SaveConfig();
                        MessageBox.Show(
                            this,
                            "Для PostgreSQL файл не выбирается.\r\n" +
                            "Введите строку подключения вручную в поле \"Источник\". Можно в двух форматах:\r\n\r\n" +
                            "1) Классический Npgsql: Host=server;Port=5432;Database=имя;Username=логин;Password=пароль;\r\n" +
                            "2) URI: postgresql://логин:пароль@server:5432/имя?search_path=myschema\r\n",
                            "PostgreSQL",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    if (ofd.FilterIndex == FilterIndexCsvFolder)
                    {
                        SaveConfig();
                        PickCsvFolder(isSource: true);
                        return;
                    }

                    _sourcePath = ofd.FileName;
                    txtSourcePath.Text = _sourcePath;

                    _config.LastSourcePath = _sourcePath;
                    SaveConfig();
                }
            }
        }

        private void BtnBrowseTarget_Click(object sender, EventArgs e)
        {
            if (_isBusy) return;

            const int FilterIndexPostgres = 5;
            const int FilterIndexCfg = 6;
            const int FilterIndexCsvFolder = 7;
            const int FiltersCount = 8;

            // Важно: ЛКМ всегда открывает выбор ФАЙЛА (как раньше).
            // Для выбора папки CSV используйте:
            //  - ПКМ по кнопке "Обзор" (быстро), либо
            //  - пункт фильтра "CSV (папка...)" внутри диалога выбора файла.

            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter =
                    "Access (*.mdb;*.accdb)|*.mdb;*.accdb|" +
                    "SQLite (*.sqlite;*.db;*.db3)|*.sqlite;*.db;*.db3|" +
                    "Excel (*.xlsx;*.xlsm;*.xlam;*.xls;*.xla)|*.xlsx;*.xlsm;*.xlam;*.xls;*.xla|" +
                    "LibreOffice Calc (*.ods)|*.ods|" +
                    "PostgreSQL (строка подключения вручную)|*.*|" +
                    "Конфиг Emicon (*.cfg)|*.cfg|" +
                    "CSV (папка, набор файлов *.csv)|*.*|" +
                    "Все файлы (*.*)|*.*";

                int idx = _config?.LastTargetBrowseFilterIndex ?? 1;
                if (idx < 1) idx = 1;
                if (idx == FilterIndexCsvFolder && !string.IsNullOrWhiteSpace(_targetPath) && File.Exists(_targetPath))
                    idx = FiltersCount;
                if (idx > FiltersCount) idx = FiltersCount;
                ofd.FilterIndex = idx;

                try
                {
                    if (!string.IsNullOrWhiteSpace(_targetPath))
                    {
                        if (Directory.Exists(_targetPath))
                            ofd.InitialDirectory = _targetPath;
                        else
                        {
                            var dir = Path.GetDirectoryName(_targetPath);
                            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                                ofd.InitialDirectory = dir;
                        }
                    }
                }
                catch
                {
                    // игнорируем
                }

                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _config.LastTargetBrowseFilterIndex = ofd.FilterIndex;

                    if (ofd.FilterIndex == FilterIndexPostgres)
                    {
                        SaveConfig();
                        MessageBox.Show(
                            this,
                            "Для PostgreSQL файл не выбирается.\r\n" +
                            "Введите строку подключения вручную в поле \"Приёмник\". Можно в двух форматах:\r\n\r\n" +
                            "1) Классический Npgsql: Host=server;Port=5432;Database=имя;Username=логин;Password=пароль;\r\n" +
                            "2) URI: postgresql://логин:пароль@server:5432/имя?search_path=myschema\r\n",
                            "PostgreSQL",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    if (ofd.FilterIndex == FilterIndexCsvFolder)
                    {
                        SaveConfig();
                        PickCsvFolder(isSource: false);
                        return;
                    }

                    _targetPath = ofd.FileName;
                    txtTargetPath.Text = _targetPath;

                    _config.LastTargetPath = _targetPath;
                    SaveConfig();
                }
            }
        }

        private string GetSourceConnectionString()
        {
            var text = txtSourcePath.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException(
                    "Не выбран путь к базе или строка подключения (Источник).");

            // синхронизация поля, чтобы конфиг и логи видели актуальное значение
            _sourcePath = ExtractPlainPath(text);

            var cs = _connectionStringService.BuildFromInput(text);
            cs = AppendCsvFolderOptionsIfNeeded(cs);
            cs = AppendXmlConfigFolderOptionsIfNeeded(cs);
            return cs;
        }

        private string GetTargetConnectionString()
        {
            var text = txtTargetPath.Text?.Trim();

            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException(
                    "Не выбран путь к базе или строка подключения (Приёмник).");

            _targetPath = ExtractPlainPath(text);

            var cs = _connectionStringService.BuildFromInput(text);
            cs = AppendCsvFolderOptionsIfNeeded(cs);
            cs = AppendXmlConfigFolderOptionsIfNeeded(cs);
            return cs;
        }

        private string AppendCsvFolderOptionsIfNeeded(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return connectionString;

            var cs = connectionString;
            if (!cs.StartsWith("CsvFolder=", StringComparison.OrdinalIgnoreCase))
                return cs;

            // Если Recursive уже задан вручную — не трогаем.
            if (cs.IndexOf("Recursive=", StringComparison.OrdinalIgnoreCase) >= 0)
                return cs;

            bool recursive = _config != null && _config.CsvFolderRecursive;

            if (!cs.EndsWith(";"))
                cs += ";";

            cs += recursive ? "Recursive=1;" : "Recursive=0;";
            return cs;
        }

        
        private string AppendXmlConfigFolderOptionsIfNeeded(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return connectionString;

            var cs = connectionString;
            if (!cs.StartsWith("XmlConfigFolder=", StringComparison.OrdinalIgnoreCase))
                return cs;

            // Если Recursive уже задан вручную — не трогаем.
            if (cs.IndexOf("Recursive=", StringComparison.OrdinalIgnoreCase) >= 0)
                return cs;

            bool recursive = _config != null && _config.XmlConfigFolderRecursive;

            if (!cs.EndsWith(";"))
                cs += ";";

            cs += recursive ? "Recursive=1;" : "Recursive=0;";
            return cs;
        }

        private static string ExtractPlainPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            var s = value.Trim();

            // CsvFolder=...;
            if (s.StartsWith("CsvFolder=", StringComparison.OrdinalIgnoreCase))
                return ExtractBetweenPrefixAndSemicolon(s, "CsvFolder=");

            // XmlConfigFolder=...;
            if (s.StartsWith("XmlConfigFolder=", StringComparison.OrdinalIgnoreCase))
                return ExtractBetweenPrefixAndSemicolon(s, "XmlConfigFolder=");

            // ExcelFile=...;
            if (s.StartsWith("ExcelFile=", StringComparison.OrdinalIgnoreCase))
                return ExtractBetweenPrefixAndSemicolon(s, "ExcelFile=");

            // CfgXmlFile=...;
            if (s.StartsWith("CfgXmlFile=", StringComparison.OrdinalIgnoreCase))
                return ExtractBetweenPrefixAndSemicolon(s, "CfgXmlFile=");

            return s;
        }

        private static string ExtractBetweenPrefixAndSemicolon(string s, string prefix)
        {
            try
            {
                var rest = s.Substring(prefix.Length);
                int semi = rest.IndexOf(';');
                var val = semi >= 0 ? rest.Substring(0, semi) : rest;
                return val.Trim().Trim('"');
            }
            catch
            {
                return s;
            }
        }

        private void ShowFolderPickMenu(bool isSource, Control anchor, System.Drawing.Point location)
        {
            try
            {
                if (anchor == null)
                    anchor = this;

                var menu = new ContextMenuStrip();

                var miCsv = new ToolStripMenuItem("Папка CSV (*.csv)");
                miCsv.Click += (s, e) => PickCsvFolder(isSource);

                var miCfg = new ToolStripMenuItem("Папка CONFIG (*.config)");
                miCfg.Click += (s, e) => PickXmlConfigFolder(isSource);

                menu.Items.Add(miCsv);
                menu.Items.Add(miCfg);

                // показываем рядом с курсором/кнопкой
                var p = location;
                if (p.X <= 0 && p.Y <= 0)
                    p = anchor.PointToClient(Cursor.Position);

                menu.Show(anchor, p);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Не удалось показать меню выбора папки.", ex);
            }
        }

        private void PickXmlConfigFolder(bool isSource)
        {
            try
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = isSource
                        ? "Выберите папку с XML .config файлами (Источник)"
                        : "Выберите папку с XML .config файлами (Приёмник)";

                    var curText = isSource ? txtSourcePath.Text : txtTargetPath.Text;
                    var cur = ExtractPlainPath(curText);

                    if (!string.IsNullOrWhiteSpace(cur) && Directory.Exists(cur))
                        dlg.SelectedPath = cur;
                    else
                    {
                        var dir = string.IsNullOrWhiteSpace(cur) ? null : Path.GetDirectoryName(cur);
                        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                            dlg.SelectedPath = dir;
                    }

                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    var folder = dlg.SelectedPath;
                    if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                        return;

                    var csText = "XmlConfigFolder=" + folder + ";";

                    if (isSource)
                    {
                        _sourcePath = folder;
                        txtSourcePath.Text = csText;
                        _config.LastSourcePath = csText;
                        // фильтр оставляем как был (не влияет на выбор папок)
                    }
                    else
                    {
                        _targetPath = folder;
                        txtTargetPath.Text = csText;
                        _config.LastTargetPath = csText;
                    }

                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Не удалось выбрать папку с XML .config файлами.", ex);
                MessageBox.Show(this,
                    "Ошибка выбора папки с XML .config файлами:\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
private void PickCsvFolder(bool isSource)
        {
            try
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = isSource
                        ? "Выберите папку с CSV-файлами (Источник)"
                        : "Выберите папку с CSV-файлами (Приёмник)";

                    var cur = isSource ? _sourcePath : _targetPath;
                    if (!string.IsNullOrWhiteSpace(cur) && Directory.Exists(cur))
                        dlg.SelectedPath = cur;
                    else
                    {
                        var dir = string.IsNullOrWhiteSpace(cur) ? null : Path.GetDirectoryName(cur);
                        if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                            dlg.SelectedPath = dir;
                    }

                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    var folder = dlg.SelectedPath;
                    if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                        return;

                    if (isSource)
                    {
                        _sourcePath = folder;
                        txtSourcePath.Text = folder;
                        _config.LastSourcePath = folder;
                        _config.LastSourceBrowseFilterIndex = 7;
                    }
                    else
                    {
                        _targetPath = folder;
                        txtTargetPath.Text = folder;
                        _config.LastTargetPath = folder;
                        _config.LastTargetBrowseFilterIndex = 7;
                    }

                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Не удалось выбрать папку с CSV-файлами.", ex);
                MessageBox.Show(
                    this,
                    "Не удалось выбрать папку с CSV-файлами.\r\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        #endregion

        #region Загрузка таблиц + сравнение всех (фон)

        private async void BtnLoadTables_Click(object sender, EventArgs e)
        {
            if (_isBusy) return;

            // сохраняем текущее подключение в конфиг
            var srcText = txtSourcePath.Text?.Trim();
            var tgtText = txtTargetPath.Text?.Trim();

            _sourcePath = srcText;
            _targetPath = tgtText;

            _config.LastSourcePath = srcText;
            _config.LastTargetPath = tgtText;
            SaveConfig();

            var sw = Stopwatch.StartNew();
            bool ok = false;

            try
            {
                var srcConnStr = GetSourceConnectionString();
                var tgtConnStr = GetTargetConnectionString();

                // создаём токен отмены для этой операции
                _loadCts?.Cancel();
                _loadCts = new CancellationTokenSource();
                var token = _loadCts.Token;

                SetBusy(true, "Получение списка таблиц...");
                if (progressBar != null)
                {
                    progressBar.Visible = true;
                    progressBar.Style = ProgressBarStyle.Blocks;
                    progressBar.Value = 0;
                }
                btnCancelLoad.Enabled = true;
                var excluded = GetExcludedTablesSet();

                var uiProgress = new Progress<TablesDiffProgress>(p =>
                {
                    // Этот коллбек выполняется в UI-потоке, так как Progress<T> захватывает SynchronizationContext
                    if (progressBar != null)
                    {
                        if (p.Total > 0)
                            progressBar.Maximum = p.Total;

                        var v = Math.Min(progressBar.Maximum, Math.Max(0, p.Processed));
                        progressBar.Value = v;
                    }

                    if (lblStatus != null)
                    {
                        lblStatus.Text = $"Сравнение таблиц... {p.Processed} / {p.Total}";
                    }
                });

                var loadResult = await _tablesLoadController.LoadAndCompareAsync(
                    srcConnStr,
                    tgtConnStr,
                    excluded,
                    _config,
                    token,
                    uiProgress);

                if (loadResult.CommonTablesCount == 0)
                {
                    clbTables.Items.Clear();
                    _lastSummaries = new List<TableDiffSummary>();

                    if (lblStatus != null)
                        lblStatus.Text = "Общих таблиц не найдено (с учётом исключений).";

                    ok = true;
                    return;
                }

                var summaries = loadResult.Summaries;

                // сохраняем сводки для дальнейшей фильтрации

                _lastSummaries = summaries;

                // обновляем список таблиц в соответствии с чекбоксом
                RefreshTablesList();

                // считаем количество таблиц с отличиями независимо от фильтра
                int tablesWithDiff = summaries.Count(s => s.TotalDiffCount > 0);

                if (lblStatus != null)
                {
                    lblStatus.Text = $"Таблиц с отличиями: {tablesWithDiff} (из {loadResult.CommonTablesCount} общих)";
                }

                if (tablesWithDiff == 0)
                {
                    _diffGridBuilder.BuildEmptyGrid("—");
                }

                ok = true;
                AppLogger.Info($"Сравнение завершено. Общих таблиц: {loadResult.CommonTablesCount}, " +
                               $"с отличиями: {tablesWithDiff}. Время: {sw.Elapsed.TotalSeconds:F1} с.");


            }
            catch (OperationCanceledException)
            {
                if (lblStatus != null)
                    lblStatus.Text = "Операция отменена пользователем.";
                AppLogger.Info("Пользователь отменил загрузку/сравнение таблиц.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка при загрузке таблиц или сравнении.", ex);
                MessageBox.Show(this, "Ошибка при загрузке/сравнении таблиц:\n" + ex.Message,
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                sw.Stop();

                btnCancelLoad.Enabled = false;
                if (progressBar != null)
                {
                    progressBar.Visible = false;
                    progressBar.Style = ProgressBarStyle.Blocks;
                }
                _loadCts = null;
                SetBusy(false, null);

                if (ok && lblStatus != null && !string.IsNullOrWhiteSpace(lblStatus.Text))
                {
                    lblStatus.Text += $". Время: {sw.Elapsed.TotalSeconds:F1} с.";
                }
            }
        }
        private void ChkShowAllTables_CheckedChanged(object sender, EventArgs e)
        {
            // переотображаем список на основе сохранённых сводок
            RefreshTablesList();
        }

        private void RefreshTablesList()
        {
            if (_lastSummaries == null)
                return;

            // запоминаем выбранную таблицу, чтобы попытаться её восстановить
            string selectedTable = null;
            if (clbTables.SelectedItem is TableItem selectedItem)
                selectedTable = selectedItem.Name;

            bool showAll = chkShowAllTables != null && chkShowAllTables.Checked;

            IEnumerable<TableDiffSummary> filtered = _lastSummaries;

            if (!showAll)
            {
                filtered = filtered.Where(s => s.TotalDiffCount > 0);
            }

            var items = filtered
                .OrderBy(s => s.TableName, StringComparer.OrdinalIgnoreCase)
                .Select(s => new TableItem
                {
                    Name = s.TableName,
                    DiffCount = s.TotalDiffCount
                })
                .ToList();

            clbTables.BeginUpdate();
            try
            {
                clbTables.Items.Clear();
                foreach (var item in items)
                {
                    clbTables.Items.Add(item, true);
                }
            }
            finally
            {
                clbTables.EndUpdate();
            }

            UpdateTablesHorizontalExtent();

            if (clbTables.Items.Count == 0)
                return;

            // пробуем восстановить выбор
            if (!string.IsNullOrEmpty(selectedTable))
            {
                for (int i = 0; i < clbTables.Items.Count; i++)
                {
                    if (clbTables.Items[i] is TableItem ti &&
                        string.Equals(ti.Name, selectedTable, StringComparison.OrdinalIgnoreCase))
                    {
                        clbTables.SelectedIndex = i;
                        return;
                    }
                }
            }

            // иначе выбираем первую таблицу
            if (clbTables.SelectedIndex < 0 && clbTables.Items.Count > 0)
                clbTables.SelectedIndex = 0;
        }

        private int GetDiffCountForTable(string srcConnStr, string tgtConnStr, string tableName)
        {
            return _comparisonService.GetDiffCountForTable(
                srcConnStr,
                tgtConnStr,
                tableName,
                _config);
        }

        #endregion

        #region Сравнение одной таблицы (фон)

        private async void ClbTables_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isBusy) return;

            var item = clbTables.SelectedItem as TableItem;
            if (item != null)
            {
               // await CompareTableAsync(item.Name);
            }
        }

        private async void BtnCompareTable_Click(object sender, EventArgs e)
        {
            if (_isBusy) return;

            var item = clbTables.SelectedItem as TableItem;
            if (item == null)
            {
                MessageBox.Show(this, "Выбери таблицу в списке.", "Нет таблицы",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var tableName = item.Name;

            // проверка PK и пользовательского ключа
            bool hasPk = false;
            bool hasCustomKey = false;

            // пробуем узнать PK
            try
            {
                var srcConnStr = GetSourceConnectionString();
                var pkColumns = _dbProvider.GetPrimaryKeyColumns(srcConnStr, tableName);
                hasPk = pkColumns != null && pkColumns.Length > 0;
            }
            catch
            {
                hasPk = false;
                AppLogger.Info("Не удалось получить строку подключения/PK. PK не считан. CompareTableAsync сам отработл.");
            }

            // есть ли пользовательский ключ в конфиге
            if (_config?.CustomKeys != null)
            {
                var cfg = _config.CustomKeys
                    .FirstOrDefault(k => string.Equals(k.TableName, tableName, StringComparison.OrdinalIgnoreCase));

                if (cfg != null && cfg.Columns != null && cfg.Columns.Count > 0)
                    hasCustomKey = true;
            }

            // если ни PK, ни пользовательского ключа нет — выводим предупреждение
            if (!hasPk && !hasCustomKey)
            {
                var msg =
                    "Внимание!\n\n" +
                    "Таблица [" + tableName + "] не имеет первичного ключа, " +
                    "и для неё не настроен пользовательский ключ.\n\n" +
                    "Сравнение будет выполняться по всем столбцам, и строки могут " +
                    "попадать в режим \"Только в источнике\" / \"Только в приёмнике\".\n\n" +
                    "Для более корректного сопоставления строк рекомендуется настроить " +
                    "пользовательский ключ:\n" +
                    "ПКМ по нужной таблице → \"Настроить ключ...\".\n\n" +
                    "Продолжить сравнение без ключа?";

                var dr = MessageBox.Show(this, msg,
                    "Таблица без ключа",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (dr == DialogResult.No)
                {
                    // пользователь решил сначала настроить ключ
                    return;
                }
            }

            // запускаем реальное сравнение
            await CompareTableAsync(tableName);
        }


        private async Task CompareTableAsync(string tableName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtSourcePath.Text) || string.IsNullOrWhiteSpace(txtTargetPath.Text))
                {
                    MessageBox.Show(this,
                        "Сначала выбери обе базы (источник и приёмник).",
                        "Не выбраны базы",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var srcConnStr = GetSourceConnectionString();
                var tgtConnStr = GetTargetConnectionString();

                if (_tableCompareController == null)
                    _tableCompareController = new TableCompareController(_comparisonService, _diffGridBuilder, _diffFilteringService, _config);

                await _tableCompareController.CompareTableAsync(
                    this,
                    tableName,
                    srcConnStr,
                    tgtConnStr,
                    dgvDiff,
                    progressBar,
                    lblStatus,
                    SetBusy);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Ошибка подготовки сравнения таблицы '{tableName}'. {ex}");
                MessageBox.Show(this,
                    "Ошибка подготовки сравнения таблицы '" + tableName + "'.\r\n\r\n" + ex.Message,
                    "Ошибка сравнения",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static int CompareValuesForSort(object v1, object v2, Type type)
        {
            bool isNull1 = (v1 == null || v1 == DBNull.Value);
            bool isNull2 = (v2 == null || v2 == DBNull.Value);

            if (isNull1 && isNull2)
                return 0;
            if (isNull1)
                return -1;
            if (isNull2)
                return 1;

            // Для чисел, дат и т.п. — нормальное сравнение через IComparable
            var c1 = v1 as IComparable;
            var c2 = v2 as IComparable;
            if (c1 != null && c2 != null)
            {
                try
                {
                    return c1.CompareTo(c2);
                }
                catch
                {
                    AppLogger.Info("Упали в строковое сравнение.");
                }
            }

            // Фолбэк — строковое сравнение
            string s1 = Convert.ToString(v1);
            string s2 = Convert.ToString(v2);
            return string.Compare(s1, s2, StringComparison.Ordinal);
        }

        #endregion

        #region Применение изменений

        private async void BtnApplySelected_Click(object sender, EventArgs e)
{
    if (_isBusy) return;

    await _applyController.ApplySelectedDiffRowsAsync(
        this,
        dgvDiff,
        GetTargetConnectionString,
        GetSelectedTableNameForUi,
        CompareTableAsync,
        SetBusy);
}


        private async void BtnApplyWholeTable_Click(object sender, EventArgs e)
{
    if (_isBusy) return;

    await _applyController.ReplaceCheckedTablesAsync(
        this,
        GetSourceConnectionString,
        GetTargetConnectionString,
        GetCheckedTableNamesForUi,
        progressBar,
        lblStatus,
        SetBusy,
        OnTablesReplacedSuccessfully);
}

        /// <summary>
        /// После успешной перезаливки таблиц обновляем сводку и список "изменённых" таблиц.
        /// </summary>
        private void OnTablesReplacedSuccessfully(IReadOnlyList<string> tableNames)
        {
            if (tableNames == null || tableNames.Count == 0)
                return;

            // 1) Обновляем сводку: после успешной перезаливки таблицы становятся одинаковыми
            if (_lastSummaries != null)
            {
                var set = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);
                foreach (var s in _lastSummaries)
                {
                    if (s == null) continue;
                    if (!set.Contains(s.TableName)) continue;

                    s.OnlyInSourceCount = 0;
                    s.OnlyInTargetCount = 0;
                    s.DifferentCount = 0;
                    s.TotalDiffCount = 0;
                }
            }

            // 2) Если сейчас в гриде показана перезалитая таблица — очищаем diff, чтобы не вводить в заблуждение
            var ctx = dgvDiff?.Tag as DiffContext;
            if (ctx != null)
            {
                var currentTable = ctx.TableName;
                if (!string.IsNullOrWhiteSpace(currentTable) &&
                    tableNames.Any(t => string.Equals(t, currentTable, StringComparison.OrdinalIgnoreCase)))
                {
                    dgvDiff.Columns.Clear();
                    dgvDiff.Rows.Clear();
                    dgvDiff.Tag = null;

                    if (lblStatus != null)
                        lblStatus.Text = "Таблица [" + currentTable + "] перезалита. Различий больше нет.";
                }
            }

            // 3) Обновляем список таблиц (если чекбокс "показывать все" выключен — перезалитые исчезнут)
            RefreshTablesList();
        }


        /// <summary>
        /// Удаляет таблицу из указанной базы данных (если возможно).
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе.</param>
        /// <param name="tableName">Имя таблицы для удаления.</param>
        /// <param name="dbLabel">Текст для логов/сообщений.</param>
        private void DeleteTableFromDatabase(string connectionString, string tableName, string dbLabel)
        {
            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(tableName))
                return;

            try
            {
                _dbProvider.DropTable(connectionString, tableName);

                AppLogger.Info(
                    $"Таблица '{tableName}' удалена из {dbLabel}.");
            }
            catch (Exception ex)
            {
                AppLogger.Error(
                    $"Ошибка при удалении таблицы '{tableName}' из {dbLabel}.", ex);

                MessageBox.Show(
                    $"Ошибка при удалении таблицы '{tableName}' из {dbLabel}." +
                    Environment.NewLine + ex.Message,
                    "Ошибка удаления таблицы",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        private void btnDeleteTable_Click(object sender, EventArgs e)
        {
            // Не даём запускать удаление во время сравнения
            if (_isBusy)
            {
                MessageBox.Show(
                    "Сейчас выполняется операция сравнения. " +
                    "Дождитесь её завершения перед следующим удалением таблицы.",
                    "Операция недоступна",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!(clbTables.SelectedItem is TableItem selected) ||
                string.IsNullOrWhiteSpace(selected.Name))
            {
                MessageBox.Show(
                    "Не выбрана таблица для удаления.",
                    "Удаление таблицы",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string tableName = selected.Name;
            string srcPath = txtSourcePath.Text;
            string tgtPath = txtTargetPath.Text;

            if (string.IsNullOrWhiteSpace(srcPath) || string.IsNullOrWhiteSpace(tgtPath))
            {
                MessageBox.Show(
                    "Не указаны пути к базе-источнику и/или базе-приёмнику.",
                    "Удаление таблицы",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string message =
                $"Вы действительно хотите удалить таблицу '{tableName}'?" + Environment.NewLine +
                Environment.NewLine +
                "Из базы-источника:" + Environment.NewLine +
                srcPath + Environment.NewLine +
                Environment.NewLine +
                "И из базы-приёмника:" + Environment.NewLine +
                tgtPath + Environment.NewLine +
                Environment.NewLine +
                "Операция необратима.";

            var result = MessageBox.Show(
                message,
                "Подтверждение удаления таблицы",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
                return;

            try
            {
                AppLogger.Info(
                    $"Пользователь подтвердил удаление таблицы '{tableName}' " +
                    "из базы-источника и базы-приёмника.");

                string srcConnStr = GetSourceConnectionString();
                string tgtConnStr = GetTargetConnectionString();

                // Пытаемся удалить таблицу в обеих БД.
                DeleteTableFromDatabase(srcConnStr, tableName, "базы-источника");
                DeleteTableFromDatabase(tgtConnStr, tableName, "базы-приёмника");

                // После удаления полностью обновляем список таблиц и диффы.
                btnLoadTables.PerformClick();
            }
            catch (Exception ex)
            {
                AppLogger.Error(
                    $"Неожиданная ошибка при удалении таблицы '{tableName}' из обеих баз.", ex);

                MessageBox.Show(
                    $"Произошла ошибка при удалении таблицы '{tableName}'." +
                    Environment.NewLine + ex.Message,
                    "Ошибка удаления таблицы",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        

        #endregion

        private void BtnEditExclusions_Click(object sender, EventArgs e)
        {
            if (_isBusy) return;

            // старый список исключений
            var existing = _config.ExcludedTables != null
                ? new List<string>(_config.ExcludedTables)
                : new List<string>();

            string currentTableName = null;
            if (clbTables.SelectedItem is TableItem selectedItem)
                currentTableName = selectedItem.Name;

            using (var dlg = new ExcludedTablesForm(existing, currentTableName))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                // новый список
                _config.ExcludedTables = dlg.ResultTables ?? new List<string>();
                SaveConfig();

                var beforeSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
                var afterSet = GetExcludedTablesSet();

                // что только что добавили в исключения
                var newlyExcluded = beforeSet.Count == 0
                    ? afterSet
                    : new HashSet<string>(afterSet.Except(beforeSet, StringComparer.OrdinalIgnoreCase),
                                          StringComparer.OrdinalIgnoreCase);

                // что только что Убрали из исключений
                var newlyIncluded = afterSet.Count == 0
                    ? beforeSet
                    : new HashSet<string>(beforeSet.Except(afterSet, StringComparer.OrdinalIgnoreCase),
                                          StringComparer.OrdinalIgnoreCase);

                int prevIndex = clbTables.SelectedIndex;
                string prevName = (clbTables.SelectedItem as TableItem)?.Name;

                clbTables.BeginUpdate();

                // 1) убираем только что добавленные в исключения из списка
                if (newlyExcluded.Count > 0)
                {
                    for (int i = clbTables.Items.Count - 1; i >= 0; i--)
                    {
                        if (clbTables.Items[i] is TableItem ti &&
                            newlyExcluded.Contains(ti.Name))
                        {
                            clbTables.Items.RemoveAt(i);
                        }
                    }
                }

                // 2) добавляем обратно только что УДАЛЁННЫЕ из исключений
                bool canRecalc = !string.IsNullOrWhiteSpace(_sourcePath) &&
                                 !string.IsNullOrWhiteSpace(_targetPath);

                if (canRecalc && newlyIncluded.Count > 0)
                {
                    var srcConnStr = GetSourceConnectionString();
                    var tgtConnStr = GetTargetConnectionString();

                    foreach (var name in newlyIncluded)
                    {
                        // уже есть в списке — не добавляем
                        bool already = false;
                        for (int i = 0; i < clbTables.Items.Count; i++)
                        {
                            if (clbTables.Items[i] is TableItem ti &&
                                string.Equals(ti.Name, name, StringComparison.OrdinalIgnoreCase))
                            {
                                already = true;
                                break;
                            }
                        }
                        if (already)
                            continue;

                        try
                        {
                            int diffCount = GetDiffCountForTable(srcConnStr, tgtConnStr, name);
                            if (diffCount > 0)
                            {
                                clbTables.Items.Add(new TableItem
                                {
                                    Name = name,
                                    DiffCount = diffCount
                                }, true);
                            }
                        }
                        catch
                        {
                            AppLogger.Info("С таблицей что-то не так.");
                        }
                    }
                }

                clbTables.EndUpdate();
                UpdateTablesHorizontalExtent();

                if (clbTables.Items.Count == 0)
                {
                    _diffGridBuilder.BuildEmptyGrid("—");
                    if (lblStatus != null)
                        lblStatus.Text = "Все таблицы из списка оказались исключёнными.";
                    return;
                }

                // восстанавливаем/обновляем выбор
                int newIndex = -1;
                if (!string.IsNullOrEmpty(prevName))
                {
                    for (int i = 0; i < clbTables.Items.Count; i++)
                    {
                        if (clbTables.Items[i] is TableItem ti &&
                            string.Equals(ti.Name, prevName, StringComparison.OrdinalIgnoreCase))
                        {
                            newIndex = i;
                            break;
                        }
                    }
                }

                if (newIndex >= 0)
                    clbTables.SelectedIndex = newIndex;
                else if (clbTables.SelectedIndex < 0 && clbTables.Items.Count > 0)
                    clbTables.SelectedIndex = 0;
            }
        }

        private static string ShowInputBox(string title, string prompt, string defaultValue)
        {
            using (var form = new Form())
            using (var lbl = new Label())
            using (var txt = new TextBox())
            using (var btnOk = new Button())
            using (var btnCancel = new Button())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(480, 140);

                // Заголовок
                lbl.AutoSize = false;
                lbl.Text = prompt;
                lbl.SetBounds(9, 9, 462, 40);

                // Поле ввода
                txt.Text = defaultValue ?? string.Empty;
                txt.SetBounds(12, 55, 456, 20);
                txt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                txt.BorderStyle = BorderStyle.FixedSingle;

                // Кнопки
                btnOk.Text = "OK";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.SetBounds(292, 90, 80, 25);

                btnCancel.Text = "Отмена";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.SetBounds(388, 90, 80, 25);

                form.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                UiTheme.ApplyDark(form);

                // более интегрированный вид:
                 txt.BackColor = form.BackColor;
                 txt.ForeColor = form.ForeColor;

                // Чтобы CenterParent реально работал, дадим владельца, если есть
                Form owner = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;

                return form.ShowDialog(owner) == DialogResult.OK
                    ? txt.Text
                    : string.Empty;
            }
        }

        private void UpdateTablesHorizontalExtent()
        {
            int maxWidth = 0;

            foreach (var item in clbTables.Items)
            {
                string text = item?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                    continue;

                var size = TextRenderer.MeasureText(text, clbTables.Font);
                if (size.Width > maxWidth)
                    maxWidth = size.Width;
            }

            // + запас под скролл и отступы
            maxWidth += SystemInformation.VerticalScrollBarWidth + 8;

            clbTables.HorizontalExtent = maxWidth;
        }
        private void BtnCancelLoad_Click(object sender, EventArgs e)
        {
            if (_loadCts != null && !_loadCts.IsCancellationRequested)
            {
                _loadCts.Cancel();
            }
        }

        private void EnableDoubleBuffering(DataGridView dgv)
        {
            var prop = typeof(DataGridView).GetProperty(
                "DoubleBuffered",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (prop != null)
                prop.SetValue(dgv, true, null);
        }
        private void AlignStatusWithProgress()
        {
            if (lblStatus == null || progressBar == null)
                return;

            // та же ширина, что у progressBar
            lblStatus.Left = progressBar.Left;
            lblStatus.Width = progressBar.Width;

            // лейбл чуть выше полосы
            lblStatus.Top = progressBar.Top - lblStatus.Height - 2;
        }

        private void ClbTables_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= clbTables.Items.Count)
                return;

            var g = e.Graphics;
            var bounds = e.Bounds;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool isChecked = clbTables.GetItemChecked(e.Index);

            // Базовые цвета
            Color back = clbTables.BackColor;
            Color fore = clbTables.ForeColor;

            if (selected)
            {
                // тёмный цвет выделения
                back = Color.FromArgb(60, 70, 90);
                fore = Color.White;
            }

            // фон
            using (var b = new SolidBrush(back))
                g.FillRectangle(b, bounds);

            // рисуем чекбокс
            int checkSize = 14;
            var checkRect = new Rectangle(
                bounds.X + 2,
                bounds.Y + (bounds.Height - checkSize) / 2,
                checkSize,
                checkSize);

            ControlPaint.DrawCheckBox(
                g,
                checkRect,
                isChecked ? ButtonState.Checked : ButtonState.Normal);

            // текст
            string text = clbTables.Items[e.Index].ToString();
            var textRect = new Rectangle(
                checkRect.Right + 4,
                bounds.Y,
                bounds.Width - (checkRect.Right + 4),
                bounds.Height);

            TextRenderer.DrawText(
                g,
                text,
                e.Font,
                textRect,
                fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

            // рамка фокуса
            e.DrawFocusRectangle();
        }
        private void ToolStripMenuItemParallel_Click(object sender, EventArgs e)
        {
            using (var dlg = new SettingsForm(_config))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // _config уже изменён внутри диалога
                    SaveConfig();
                    AppLogger.Info($"Изменена настройка MaxParallelTables: {_config.MaxParallelTables}.");
                }
            }
        }

        private void ToolStripMenuItemShowNullEmptyMarkers_Click(object sender, EventArgs e)
        {
            try
            {
                if (_config == null)
                    return;

                _config.ShowNullEmptyMarkers = toolStripMenuItemShowNullEmptyMarkers.Checked;
                SaveConfig();
                AppLogger.Info($"Параметр ShowNullEmptyMarkers изменён: {_config.ShowNullEmptyMarkers}.");

                // Перерисовать текущую таблицу (без повторного сравнения), чтобы маркеры применились сразу.
                var ctx = dgvDiff?.Tag as DiffContext;
                if (ctx != null)
                {
                    _diffGridBuilder.ShowDiff(ctx.TableName, ctx.Pairs, ctx.SourceColumns, ctx.PrimaryKeyColumns);
                    _diffFilteringService.Apply(dgvDiff);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка при переключении отображения NULL/∅.", ex);
            }
        }

        private void ToolStripMenuItemOpenLogs_Click(object sender, EventArgs e)
        {
            try
            {
                var dir = AppLogger.GetLogDirectory();
                if (string.IsNullOrWhiteSpace(dir))
                    dir = AppPaths.LogsDirectory;

                Directory.CreateDirectory(dir);

                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Не удалось открыть папку с логами.", ex);
                MessageBox.Show(
                    this,
                    "Не удалось открыть папку с логами.\r\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }



        private void ToolStripMenuItemSetLogs_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Выберите папку для логов";

                    var cur = _config?.LogsDirectory;
                    if (string.IsNullOrWhiteSpace(cur))
                        cur = AppLogger.GetLogDirectory();
                    if (string.IsNullOrWhiteSpace(cur))
                        cur = AppPaths.LogsDirectory;

                    dlg.SelectedPath = cur;

                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    var dir = dlg.SelectedPath;
                    if (string.IsNullOrWhiteSpace(dir))
                        return;

                    Directory.CreateDirectory(dir);

                    if (_config != null)
                        _config.LogsDirectory = dir;

                    SaveConfig();

                    AppLogger.Configure(dir);
                    AppLogger.Info($"Изменён путь логов: '{dir}'.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Не удалось задать папку с логами.", ex);
                MessageBox.Show(
                    this,
                    "Не удалось задать папку с логами.\r\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ToolStripMenuItemSetConfigFolder_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Выберите папку для конфигурации";

                    var curDir = Path.GetDirectoryName(_configPath);
                    if (string.IsNullOrWhiteSpace(curDir))
                        curDir = AppPaths.ConfigDirectory;

                    dlg.SelectedPath = curDir;

                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    var newDir = dlg.SelectedPath;
                    if (string.IsNullOrWhiteSpace(newDir))
                        return;

                    Directory.CreateDirectory(newDir);

                    // Сохраняем текущий конфиг в новую папку.
                    var newConfigPath = Path.Combine(newDir, "MdbDiffTool.config.xml");
                    var newService = new ConfigService(newConfigPath);
                    newService.Save(_config);

                    // Сохраняем переопределение папки (для перезапуска).
                    AppPaths.SetConfigDirectory(newDir);

                    // Переключаемся на новый путь/сервис без перезапуска.
                    _configPath = newConfigPath;
                    _configService = new ConfigService(_configPath);
                    _config = _configService.Load();

                    AppLogger.Info($"Изменена папка конфигурации: '{newDir}'.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Не удалось задать папку с конфигурацией.", ex);
                MessageBox.Show(
                    this,
                    "Не удалось задать папку с конфигурацией.\r\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ToolStripMenuItemOpenConfigFolder_Click(object sender, EventArgs e)
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (string.IsNullOrWhiteSpace(dir))
                    dir = AppPaths.ConfigDirectory;

                Directory.CreateDirectory(dir);

                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Не удалось открыть папку с конфигурацией.", ex);
                MessageBox.Show(
                    this,
                    "Не удалось открыть папку с конфигурацией.\r\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ClbTables_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            int index = clbTables.IndexFromPoint(e.Location);
            if (index >= 0 && index < clbTables.Items.Count)
            {
                clbTables.SelectedIndex = index;
            }
        }

        private void MiSetupKey_Click(object sender, EventArgs e)
        {
            if (clbTables.SelectedItem is not TableItem item)
                return;

            var tableName = item.Name;

            // открываем диалог настройки ключа
            SetupCustomKeyForTable(tableName);
        }
        private void SetupCustomKeyForTable(string tableName)
        {
            // 0. проверяем, есть ли реальный PK в базе
            try
            {
                var srcConnStr = GetSourceConnectionString();
                var pkColumns = _dbProvider.GetPrimaryKeyColumns(srcConnStr, tableName);

                // Для Excel провайдер возвращает псевдо-PK "Row" (номер строки).
                // Это не "реальный PK базы данных", поэтому пользовательский ключ для Excel должен быть разрешён.
                var isExcel = srcConnStr != null &&
                              srcConnStr.TrimStart().StartsWith("ExcelFile=", StringComparison.OrdinalIgnoreCase);

                var hasRealPk =
                    pkColumns != null &&
                    pkColumns.Length > 0 &&
                    !(isExcel && pkColumns.Length == 1 &&
                      string.Equals(pkColumns[0], "Row", StringComparison.OrdinalIgnoreCase));

                if (hasRealPk)
                {
                    var pkText = string.Join(", ", pkColumns);

                    AppLogger.Info(
                        $"Пользователь запросил настройку пользовательского ключа для таблицы '{tableName}', " +
                        $"но у таблицы уже есть первичный ключ ({pkText}). Пользовательский ключ будет проигнорирован.");

                    var msg =
                        "Внимание!\n\n" +
                        "Таблица [" + tableName + "] уже имеет первичный ключ в базе данных.\n\n" +
                        "Приложение всегда использует именно первичный ключ таблицы для сравнения, " +
                        "поэтому настройка пользовательского ключа для этой таблицы не требуется " +
                        "и будет игнорироваться.\n\n" +
                        "Если нужно поменять ключ — это нужно сделать в самой базе (в конструкторе таблиц).";

                    MessageBox.Show(this, msg,
                        "Пользовательский ключ не нужен",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    return;
                }
// 1. если PK нет — читаем структуру таблицы
                var schemaTable = _dbProvider.LoadTable(srcConnStr, tableName);

                var allColumns = schemaTable.Columns.Cast<DataColumn>()
                                                    .Select(c => c.ColumnName)
                                                    .ToList();

                // 2. текущая настройка (если есть)
                var current = _config?.CustomKeys?
                    .FirstOrDefault(k => string.Equals(k.TableName, tableName, StringComparison.OrdinalIgnoreCase));

                var selectedCols = current?.Columns ?? new List<string>();

                using (var dlg = new CustomKeyForm(tableName, allColumns, selectedCols))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK)
                        return;

                    // 3. сохраняем в конфиг
                    if (_config.CustomKeys == null)
                        _config.CustomKeys = new List<CustomKeyConfig>();

                    _config.CustomKeys.RemoveAll(k =>
                        string.Equals(k.TableName, tableName, StringComparison.OrdinalIgnoreCase));

                    if (dlg.SelectedColumns != null && dlg.SelectedColumns.Count > 0)
                    {
                        _config.CustomKeys.Add(new CustomKeyConfig
                        {
                            TableName = tableName,
                            Columns = dlg.SelectedColumns
                        });
                    }

                    SaveConfig();

                    // Пишем в лог: какой ключ выставлен пользователем.
                    // Важно: не выводим слишком длинные списки колонок.
                    var colsText = (dlg.SelectedColumns == null || dlg.SelectedColumns.Count == 0)
                        ? "<пусто>"
                        : (dlg.SelectedColumns.Count <= 6
                            ? string.Join(", ", dlg.SelectedColumns)
                            : dlg.SelectedColumns.Count + " колонок");

                    AppLogger.Info($"Пользовательский ключ для таблицы '{tableName}' обновлён: {colsText}.");

                    if (lblStatus != null)
                        lblStatus.Text = "Ключ для таблицы [" + tableName + "] обновлён.";
}
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка при настройке пользовательского ключа для таблицы [" + tableName + "]:\n", ex);
                MessageBox.Show(this,
                    "Ошибка при настройке пользовательского ключа для таблицы [" + tableName + "]:\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        /// <summary>
        /// Меню "О программе".
        /// Показывает имя разработчика, версию и дату сборки EXE.
        /// </summary>
        private void ToolStripMenuItemAbout_Click(object sender, EventArgs e)
        {
            try
            {
                // Заголовок окна
                string productName = this.Text; // "Сравнятор 3000"

                // Версия берётся из свойств сборки / проекта
                string productVersion = Application.ProductVersion;

                // Дата сборки — по времени изменения exe
                string exePath = Application.ExecutablePath;
                string buildDateText;
                try
                {
                    var buildDate = File.GetLastWriteTime(exePath);
                    buildDateText = buildDate.ToString("dd.MM.yyyy HH:mm");
                }
                catch
                {
                    buildDateText = "неизвестна";
                    AppLogger.Info("Дата билда неизвестна.");
                }

                const string developerName = "Ниязов А.Г.";

                string message =
                    "Дата сборки: " + buildDateText + Environment.NewLine +
                    "Верховный программист: " + developerName;

                MessageBox.Show(
                    this,
                    message,
                    "О программе",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка при выводе информации о программе.", ex);
                MessageBox.Show(
                    this,
                    "Ошибка при выводе информации о программе:" +
                    Environment.NewLine + ex.Message,
                    "О программе",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void BtnOpenSource_Click(object sender, EventArgs e)
        {
            OpenDatabaseExternal(_sourcePath);
        }

        private void BtnOpenTarget_Click(object sender, EventArgs e)
        {
            OpenDatabaseExternal(_targetPath);
        }

        /// <summary>
        /// Открыть базу/файл во внешней программе по пути или строке подключения.
        /// </summary>
        private void OpenDatabaseExternal(string pathOrConnection)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pathOrConnection))
                {
                    MessageBox.Show(this,
                        "Путь к базе или строка подключения не задан(а).",
                        "Открытие базы",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // 1. Если это существующий файл — открываем через ассоциацию ОС
                if (System.IO.File.Exists(pathOrConnection))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = pathOrConnection,
                        UseShellExecute = true
                    });
                    return;
                }

                // 2. Если это строка подключения PostgreSQL
                var trimmed = pathOrConnection.TrimStart();
                if (trimmed.StartsWith("Host=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("Server=", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this,
                        "Это строка подключения PostgreSQL.\r\n" +
                        "Откройте базу через pgAdmin, DBeaver или другой SQL-клиент.",
                        "Открытие базы",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                // 3. На всякий случай: попытка отдать строку ОС (если это, например, путь или URL)
                Process.Start(new ProcessStartInfo
                {
                    FileName = pathOrConnection,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error("Не удалось открыть базу.", ex);
                MessageBox.Show(this,
                    "Не удалось открыть базу:\r\n" + ex.Message,
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        #region Выделить всё / Снять всё

        private void BtnToggleCheckAll_Click(object sender, EventArgs e)
        {
            if (_isBusy) return;

            bool hasUnchecked = false;
            for (int i = 0; i < clbTables.Items.Count; i++)
            {
                if (!clbTables.GetItemChecked(i))
                {
                    hasUnchecked = true;
                    break;
                }
            }

            for (int i = 0; i < clbTables.Items.Count; i++)
            {
                clbTables.SetItemChecked(i, hasUnchecked);
            }
        }

        #endregion
    }
}
