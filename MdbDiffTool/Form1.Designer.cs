namespace MdbDiffTool
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Код, автоматически созданный конструктором форм Windows

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnCancelLoad = new System.Windows.Forms.Button();
            this.btnSwap = new System.Windows.Forms.Button();
            this.btnLoadTables = new System.Windows.Forms.Button();
            this.btnBrowseTarget = new System.Windows.Forms.Button();
            this.txtTargetPath = new System.Windows.Forms.TextBox();
            this.lblTarget = new System.Windows.Forms.Label();
            this.btnBrowseSource = new System.Windows.Forms.Button();
            this.txtSourcePath = new System.Windows.Forms.TextBox();
            this.lblSource = new System.Windows.Forms.Label();
            this.btnOpenTarget = new System.Windows.Forms.Button();
            this.btnOpenSource = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.tlpChecked = new System.Windows.Forms.TableLayoutPanel();
            this.btnToggleCheckAll = new System.Windows.Forms.Button();
            this.btnApplyWholeTable = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.tlpSelectedRow = new System.Windows.Forms.TableLayoutPanel();
            this.btnCompareTable = new System.Windows.Forms.Button();
            this.btnDeleteTable = new System.Windows.Forms.Button();
            this.chkShowAllTables = new System.Windows.Forms.CheckBox();
            this.btnExcludeTable = new System.Windows.Forms.Button();
            this.clbTables = new System.Windows.Forms.CheckedListBox();
            this.contextMenuTables = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miSetupKey = new System.Windows.Forms.ToolStripMenuItem();
            this.dgvDiff = new System.Windows.Forms.DataGridView();
            this.btnApplySelected = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.toolStripMenuItemSettings = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemParallel = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemShowNullEmptyMarkers = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorSettings = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemOpenLogs = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemSetLogs = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorSettings2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemOpenConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemSetConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.btnToggleApplyAll = new System.Windows.Forms.Button();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.tlpChecked.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.tlpSelectedRow.SuspendLayout();
            this.contextMenuTables.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDiff)).BeginInit();
            this.menuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.btnCancelLoad);
            this.groupBox1.Controls.Add(this.btnSwap);
            this.groupBox1.Controls.Add(this.btnLoadTables);
            this.groupBox1.Controls.Add(this.btnBrowseTarget);
            this.groupBox1.Controls.Add(this.txtTargetPath);
            this.groupBox1.Controls.Add(this.lblTarget);
            this.groupBox1.Controls.Add(this.btnBrowseSource);
            this.groupBox1.Controls.Add(this.txtSourcePath);
            this.groupBox1.Controls.Add(this.lblSource);
            this.groupBox1.Controls.Add(this.btnOpenTarget);
            this.groupBox1.Controls.Add(this.btnOpenSource);
            this.groupBox1.Location = new System.Drawing.Point(12, 3);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(830, 138);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "БД";
            // 
            // btnCancelLoad
            // 
            this.btnCancelLoad.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancelLoad.Location = new System.Drawing.Point(707, 103);
            this.btnCancelLoad.Name = "btnCancelLoad";
            this.btnCancelLoad.Size = new System.Drawing.Size(117, 23);
            this.btnCancelLoad.TabIndex = 8;
            this.btnCancelLoad.Text = "Отмена";
            this.btnCancelLoad.UseVisualStyleBackColor = true;
            // 
            // btnSwap
            // 
            this.btnSwap.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSwap.Location = new System.Drawing.Point(787, 29);
            this.btnSwap.Name = "btnSwap";
            this.btnSwap.Size = new System.Drawing.Size(37, 61);
            this.btnSwap.TabIndex = 7;
            this.btnSwap.UseVisualStyleBackColor = true;
            // 
            // btnLoadTables
            // 
            this.btnLoadTables.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLoadTables.Location = new System.Drawing.Point(9, 103);
            this.btnLoadTables.Name = "btnLoadTables";
            this.btnLoadTables.Size = new System.Drawing.Size(697, 23);
            this.btnLoadTables.TabIndex = 6;
            this.btnLoadTables.Text = "Загрузить (в списке будут только различные и не исключённые)";
            this.btnLoadTables.UseVisualStyleBackColor = true;
            // 
            // btnBrowseTarget
            // 
            this.btnBrowseTarget.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseTarget.Location = new System.Drawing.Point(707, 68);
            this.btnBrowseTarget.Name = "btnBrowseTarget";
            this.btnBrowseTarget.Size = new System.Drawing.Size(78, 22);
            this.btnBrowseTarget.TabIndex = 5;
            this.btnBrowseTarget.Text = "Обзор";
            this.btnBrowseTarget.UseVisualStyleBackColor = true;
            // 
            // txtTargetPath
            // 
            this.txtTargetPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTargetPath.Location = new System.Drawing.Point(70, 69);
            this.txtTargetPath.Name = "txtTargetPath";
            this.txtTargetPath.Size = new System.Drawing.Size(563, 20);
            this.txtTargetPath.TabIndex = 4;
            // 
            // lblTarget
            // 
            this.lblTarget.AutoSize = true;
            this.lblTarget.Location = new System.Drawing.Point(6, 73);
            this.lblTarget.Name = "lblTarget";
            this.lblTarget.Size = new System.Drawing.Size(62, 13);
            this.lblTarget.TabIndex = 3;
            this.lblTarget.Text = "Приёмник:";
            // 
            // btnBrowseSource
            // 
            this.btnBrowseSource.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseSource.Location = new System.Drawing.Point(707, 29);
            this.btnBrowseSource.Name = "btnBrowseSource";
            this.btnBrowseSource.Size = new System.Drawing.Size(78, 22);
            this.btnBrowseSource.TabIndex = 2;
            this.btnBrowseSource.Text = "Обзор";
            this.btnBrowseSource.UseVisualStyleBackColor = true;
            // 
            // txtSourcePath
            // 
            this.txtSourcePath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSourcePath.Location = new System.Drawing.Point(70, 30);
            this.txtSourcePath.Name = "txtSourcePath";
            this.txtSourcePath.Size = new System.Drawing.Size(563, 20);
            this.txtSourcePath.TabIndex = 1;
            // 
            // lblSource
            // 
            this.lblSource.AutoSize = true;
            this.lblSource.Location = new System.Drawing.Point(6, 33);
            this.lblSource.Name = "lblSource";
            this.lblSource.Size = new System.Drawing.Size(58, 13);
            this.lblSource.TabIndex = 0;
            this.lblSource.Text = "Источник:";
            // 
            // btnOpenTarget
            // 
            this.btnOpenTarget.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenTarget.Location = new System.Drawing.Point(636, 68);
            this.btnOpenTarget.Name = "btnOpenTarget";
            this.btnOpenTarget.Size = new System.Drawing.Size(70, 22);
            this.btnOpenTarget.TabIndex = 6;
            this.btnOpenTarget.Text = "Открыть";
            this.btnOpenTarget.UseVisualStyleBackColor = true;
            this.btnOpenTarget.Click += new System.EventHandler(this.BtnOpenTarget_Click);
            // 
            // btnOpenSource
            // 
            this.btnOpenSource.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenSource.Location = new System.Drawing.Point(636, 29);
            this.btnOpenSource.Name = "btnOpenSource";
            this.btnOpenSource.Size = new System.Drawing.Size(70, 22);
            this.btnOpenSource.TabIndex = 5;
            this.btnOpenSource.Text = "Открыть";
            this.btnOpenSource.UseVisualStyleBackColor = true;
            this.btnOpenSource.Click += new System.EventHandler(this.BtnOpenSource_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.groupBox4);
            this.groupBox2.Controls.Add(this.groupBox3);
            this.groupBox2.Controls.Add(this.chkShowAllTables);
            this.groupBox2.Controls.Add(this.btnExcludeTable);
            this.groupBox2.Controls.Add(this.clbTables);
            this.groupBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBox2.Location = new System.Drawing.Point(0, 0);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.groupBox2.Size = new System.Drawing.Size(352, 784);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Таблицы";
            // 
            // groupBox4
            // 
            this.groupBox4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox4.Controls.Add(this.tlpChecked);
            this.groupBox4.Location = new System.Drawing.Point(6, 90);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.groupBox4.Size = new System.Drawing.Size(327, 52);
            this.groupBox4.TabIndex = 8;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "По галочкам";
            // 
            // tlpChecked
            // 
            this.tlpChecked.ColumnCount = 2;
            this.tlpChecked.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpChecked.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpChecked.Controls.Add(this.btnToggleCheckAll, 0, 0);
            this.tlpChecked.Controls.Add(this.btnApplyWholeTable, 1, 0);
            this.tlpChecked.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpChecked.Location = new System.Drawing.Point(3, 16);
            this.tlpChecked.Margin = new System.Windows.Forms.Padding(0);
            this.tlpChecked.Name = "tlpChecked";
            this.tlpChecked.RowCount = 1;
            this.tlpChecked.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpChecked.Size = new System.Drawing.Size(321, 33);
            this.tlpChecked.TabIndex = 0;
            // 
            // btnToggleCheckAll
            // 
            this.btnToggleCheckAll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnToggleCheckAll.Location = new System.Drawing.Point(3, 3);
            this.btnToggleCheckAll.Name = "btnToggleCheckAll";
            this.btnToggleCheckAll.Size = new System.Drawing.Size(154, 27);
            this.btnToggleCheckAll.TabIndex = 3;
            this.btnToggleCheckAll.Text = "Выделить/Снять всё";
            this.btnToggleCheckAll.UseVisualStyleBackColor = true;
            // 
            // btnApplyWholeTable
            // 
            this.btnApplyWholeTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnApplyWholeTable.Location = new System.Drawing.Point(163, 3);
            this.btnApplyWholeTable.Name = "btnApplyWholeTable";
            this.btnApplyWholeTable.Size = new System.Drawing.Size(155, 27);
            this.btnApplyWholeTable.TabIndex = 2;
            this.btnApplyWholeTable.Text = "Залить целиком";
            this.btnApplyWholeTable.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox3.Controls.Add(this.tlpSelectedRow);
            this.groupBox3.Location = new System.Drawing.Point(6, 26);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.groupBox3.Size = new System.Drawing.Size(327, 54);
            this.groupBox3.TabIndex = 7;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "По выделенной строке";
            // 
            // tlpSelectedRow
            // 
            this.tlpSelectedRow.ColumnCount = 2;
            this.tlpSelectedRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpSelectedRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpSelectedRow.Controls.Add(this.btnCompareTable, 0, 0);
            this.tlpSelectedRow.Controls.Add(this.btnDeleteTable, 1, 0);
            this.tlpSelectedRow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpSelectedRow.Location = new System.Drawing.Point(3, 16);
            this.tlpSelectedRow.Margin = new System.Windows.Forms.Padding(0);
            this.tlpSelectedRow.Name = "tlpSelectedRow";
            this.tlpSelectedRow.RowCount = 1;
            this.tlpSelectedRow.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpSelectedRow.Size = new System.Drawing.Size(321, 35);
            this.tlpSelectedRow.TabIndex = 0;
            // 
            // btnCompareTable
            // 
            this.btnCompareTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnCompareTable.Location = new System.Drawing.Point(3, 3);
            this.btnCompareTable.Name = "btnCompareTable";
            this.btnCompareTable.Size = new System.Drawing.Size(154, 29);
            this.btnCompareTable.TabIndex = 1;
            this.btnCompareTable.Text = "Сравнить";
            this.btnCompareTable.UseVisualStyleBackColor = true;
            // 
            // btnDeleteTable
            // 
            this.btnDeleteTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnDeleteTable.Location = new System.Drawing.Point(163, 3);
            this.btnDeleteTable.Name = "btnDeleteTable";
            this.btnDeleteTable.Size = new System.Drawing.Size(155, 29);
            this.btnDeleteTable.TabIndex = 6;
            this.btnDeleteTable.Text = "Удалить";
            this.btnDeleteTable.UseVisualStyleBackColor = true;
            this.btnDeleteTable.Click += new System.EventHandler(this.btnDeleteTable_Click);
            // 
            // chkShowAllTables
            // 
            this.chkShowAllTables.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.chkShowAllTables.AutoSize = true;
            this.chkShowAllTables.Location = new System.Drawing.Point(6, 755);
            this.chkShowAllTables.Name = "chkShowAllTables";
            this.chkShowAllTables.Size = new System.Drawing.Size(110, 17);
            this.chkShowAllTables.TabIndex = 5;
            this.chkShowAllTables.Text = "Показывать все";
            this.chkShowAllTables.UseVisualStyleBackColor = true;
            this.chkShowAllTables.CheckedChanged += new System.EventHandler(this.ChkShowAllTables_CheckedChanged);
            // 
            // btnExcludeTable
            // 
            this.btnExcludeTable.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExcludeTable.Location = new System.Drawing.Point(203, 748);
            this.btnExcludeTable.Name = "btnExcludeTable";
            this.btnExcludeTable.Size = new System.Drawing.Size(130, 28);
            this.btnExcludeTable.TabIndex = 4;
            this.btnExcludeTable.Text = "Исключение таблиц";
            this.btnExcludeTable.UseVisualStyleBackColor = true;
            // 
            // clbTables
            // 
            this.clbTables.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.clbTables.ContextMenuStrip = this.contextMenuTables;
            this.clbTables.FormattingEnabled = true;
            this.clbTables.Location = new System.Drawing.Point(6, 147);
            this.clbTables.Name = "clbTables";
            this.clbTables.Size = new System.Drawing.Size(327, 604);
            this.clbTables.TabIndex = 0;
            this.clbTables.MouseDown += new System.Windows.Forms.MouseEventHandler(this.ClbTables_MouseDown);
            // 
            // contextMenuTables
            // 
            this.contextMenuTables.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miSetupKey});
            this.contextMenuTables.Name = "contextMenuTables";
            this.contextMenuTables.Size = new System.Drawing.Size(175, 26);
            // 
            // miSetupKey
            // 
            this.miSetupKey.Name = "miSetupKey";
            this.miSetupKey.Size = new System.Drawing.Size(174, 22);
            this.miSetupKey.Text = "Настроить ключ...";
            this.miSetupKey.Click += new System.EventHandler(this.MiSetupKey_Click);
            // 
            // dgvDiff
            // 
            this.dgvDiff.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvDiff.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvDiff.Location = new System.Drawing.Point(12, 147);
            this.dgvDiff.Name = "dgvDiff";
            this.dgvDiff.Size = new System.Drawing.Size(830, 589);
            this.dgvDiff.TabIndex = 2;
            // 
            // btnApplySelected
            // 
            this.btnApplySelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnApplySelected.Location = new System.Drawing.Point(712, 748);
            this.btnApplySelected.Name = "btnApplySelected";
            this.btnApplySelected.Size = new System.Drawing.Size(130, 28);
            this.btnApplySelected.TabIndex = 3;
            this.btnApplySelected.Text = "Записать выбранное";
            this.btnApplySelected.UseVisualStyleBackColor = true;
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(16, 756);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(126, 13);
            this.lblStatus.TabIndex = 4;
            this.lblStatus.Text = "для подсказок/статуса";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(12, 751);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(562, 23);
            this.progressBar.TabIndex = 5;
            this.progressBar.Visible = false;
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemSettings,
            this.toolStripMenuItemAbout});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1214, 24);
            this.menuStrip1.TabIndex = 6;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // toolStripMenuItemSettings
            // 
            this.toolStripMenuItemSettings.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemParallel,
            this.toolStripMenuItemShowNullEmptyMarkers,
            this.toolStripSeparatorSettings,
            this.toolStripMenuItemOpenLogs,
            this.toolStripMenuItemSetLogs,
            this.toolStripSeparatorSettings2,
            this.toolStripMenuItemOpenConfig,
            this.toolStripMenuItemSetConfig});
            this.toolStripMenuItemSettings.Name = "toolStripMenuItemSettings";
            this.toolStripMenuItemSettings.Size = new System.Drawing.Size(79, 20);
            this.toolStripMenuItemSettings.Text = "Настройки";
            // 
            // toolStripMenuItemParallel
            // 
            this.toolStripMenuItemParallel.Name = "toolStripMenuItemParallel";
            this.toolStripMenuItemParallel.Size = new System.Drawing.Size(255, 22);
            this.toolStripMenuItemParallel.Text = "Параметры сравнения…";
            this.toolStripMenuItemParallel.Click += new System.EventHandler(this.ToolStripMenuItemParallel_Click);
            // 
            // toolStripMenuItemShowNullEmptyMarkers
            // 
            this.toolStripMenuItemShowNullEmptyMarkers.Checked = true;
            this.toolStripMenuItemShowNullEmptyMarkers.CheckOnClick = true;
            this.toolStripMenuItemShowNullEmptyMarkers.CheckState = System.Windows.Forms.CheckState.Checked;
            this.toolStripMenuItemShowNullEmptyMarkers.Name = "toolStripMenuItemShowNullEmptyMarkers";
            this.toolStripMenuItemShowNullEmptyMarkers.Size = new System.Drawing.Size(255, 22);
            this.toolStripMenuItemShowNullEmptyMarkers.Text = "Показывать NULL/∅";
            this.toolStripMenuItemShowNullEmptyMarkers.Click += new System.EventHandler(this.ToolStripMenuItemShowNullEmptyMarkers_Click);
            // 
            // toolStripSeparatorSettings
            // 
            this.toolStripSeparatorSettings.Name = "toolStripSeparatorSettings";
            this.toolStripSeparatorSettings.Size = new System.Drawing.Size(252, 6);
            // 
            // toolStripMenuItemOpenLogs
            // 
            this.toolStripMenuItemOpenLogs.Name = "toolStripMenuItemOpenLogs";
            this.toolStripMenuItemOpenLogs.Size = new System.Drawing.Size(255, 22);
            this.toolStripMenuItemOpenLogs.Text = "Открыть папку с логами";
            this.toolStripMenuItemOpenLogs.Click += new System.EventHandler(this.ToolStripMenuItemOpenLogs_Click);
            // 
            // toolStripMenuItemSetLogs
            // 
            this.toolStripMenuItemSetLogs.Name = "toolStripMenuItemSetLogs";
            this.toolStripMenuItemSetLogs.Size = new System.Drawing.Size(255, 22);
            this.toolStripMenuItemSetLogs.Text = "Задать папку с логами…";
            this.toolStripMenuItemSetLogs.Click += new System.EventHandler(this.ToolStripMenuItemSetLogs_Click);
            // 
            // toolStripSeparatorSettings2
            // 
            this.toolStripSeparatorSettings2.Name = "toolStripSeparatorSettings2";
            this.toolStripSeparatorSettings2.Size = new System.Drawing.Size(252, 6);
            // 
            // toolStripMenuItemOpenConfig
            // 
            this.toolStripMenuItemOpenConfig.Name = "toolStripMenuItemOpenConfig";
            this.toolStripMenuItemOpenConfig.Size = new System.Drawing.Size(255, 22);
            this.toolStripMenuItemOpenConfig.Text = "Открыть папку с конфигурацией";
            this.toolStripMenuItemOpenConfig.Click += new System.EventHandler(this.ToolStripMenuItemOpenConfigFolder_Click);
            // 
            // toolStripMenuItemSetConfig
            // 
            this.toolStripMenuItemSetConfig.Name = "toolStripMenuItemSetConfig";
            this.toolStripMenuItemSetConfig.Size = new System.Drawing.Size(255, 22);
            this.toolStripMenuItemSetConfig.Text = "Задать папку с конфигурацией…";
            this.toolStripMenuItemSetConfig.Click += new System.EventHandler(this.ToolStripMenuItemSetConfigFolder_Click);
            // 
            // toolStripMenuItemAbout
            // 
            this.toolStripMenuItemAbout.Name = "toolStripMenuItemAbout";
            this.toolStripMenuItemAbout.Size = new System.Drawing.Size(94, 20);
            this.toolStripMenuItemAbout.Text = "О программе";
            this.toolStripMenuItemAbout.Click += new System.EventHandler(this.ToolStripMenuItemAbout_Click);
            // 
            // btnToggleApplyAll
            // 
            this.btnToggleApplyAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnToggleApplyAll.Location = new System.Drawing.Point(580, 748);
            this.btnToggleApplyAll.Name = "btnToggleApplyAll";
            this.btnToggleApplyAll.Size = new System.Drawing.Size(130, 28);
            this.btnToggleApplyAll.TabIndex = 6;
            this.btnToggleApplyAll.Text = "Отметить / снять всё";
            this.btnToggleApplyAll.UseVisualStyleBackColor = true;
            // 
            // splitMain
            // 
            this.splitMain.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 24);
            this.splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.groupBox1);
            this.splitMain.Panel1.Controls.Add(this.dgvDiff);
            this.splitMain.Panel1.Controls.Add(this.progressBar);
            this.splitMain.Panel1.Controls.Add(this.lblStatus);
            this.splitMain.Panel1.Controls.Add(this.btnToggleApplyAll);
            this.splitMain.Panel1.Controls.Add(this.btnApplySelected);
            this.splitMain.Panel1MinSize = 520;
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.groupBox2);
            this.splitMain.Panel2MinSize = 320;
            this.splitMain.Size = new System.Drawing.Size(1214, 786);
            this.splitMain.SplitterDistance = 850;
            this.splitMain.SplitterWidth = 10;
            this.splitMain.TabIndex = 7;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1214, 810);
            this.Controls.Add(this.splitMain);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "MDB Diff Tool";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.tlpChecked.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.tlpSelectedRow.ResumeLayout(false);
            this.contextMenuTables.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvDiff)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel1.PerformLayout();
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnLoadTables;
        private System.Windows.Forms.Button btnBrowseTarget;
        private System.Windows.Forms.TextBox txtTargetPath;
        private System.Windows.Forms.Label lblTarget;
        private System.Windows.Forms.Button btnBrowseSource;
        private System.Windows.Forms.TextBox txtSourcePath;
        private System.Windows.Forms.Label lblSource;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button btnApplyWholeTable;
        private System.Windows.Forms.Button btnCompareTable;
        private System.Windows.Forms.CheckedListBox clbTables;
        private System.Windows.Forms.DataGridView dgvDiff;
        private System.Windows.Forms.Button btnApplySelected;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnToggleCheckAll;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Button btnExcludeTable;
        private System.Windows.Forms.CheckBox chkShowAllTables;
        private System.Windows.Forms.Button btnSwap;
        private System.Windows.Forms.Button btnCancelLoad;
        private System.Windows.Forms.ContextMenuStrip contextMenuTables;
        private System.Windows.Forms.ToolStripMenuItem miSetupKey;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSettings;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemParallel;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemShowNullEmptyMarkers;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorSettings;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemOpenLogs;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSetLogs;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorSettings2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemOpenConfig;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemSetConfig;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemAbout;
        private System.Windows.Forms.Button btnDeleteTable;
        private System.Windows.Forms.Button btnOpenSource;
        private System.Windows.Forms.Button btnOpenTarget;
        private System.Windows.Forms.Button btnToggleApplyAll;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.TableLayoutPanel tlpSelectedRow;
        private System.Windows.Forms.TableLayoutPanel tlpChecked;
    }
}
