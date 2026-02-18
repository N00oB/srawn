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
            this.btnDeleteTable = new System.Windows.Forms.Button();
            this.chkShowAllTables = new System.Windows.Forms.CheckBox();
            this.btnExcludeTable = new System.Windows.Forms.Button();
            this.btnToggleCheckAll = new System.Windows.Forms.Button();
            this.btnApplyWholeTable = new System.Windows.Forms.Button();
            this.btnCompareTable = new System.Windows.Forms.Button();
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
            this.toolStripSeparatorSettings = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemOpenLogs = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemOpenConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemSetLogs = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorSettings2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItemSetConfig = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.btnToggleApplyAll = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.contextMenuTables.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvDiff)).BeginInit();
            this.menuStrip1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
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
            this.groupBox1.Location = new System.Drawing.Point(12, 27);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(830, 138);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "БД";
            // 
            // btnCancelLoad
            // 
            this.btnCancelLoad.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancelLoad.Location = new System.Drawing.Point(700, 103);
            this.btnCancelLoad.Name = "btnCancelLoad";
            this.btnCancelLoad.Size = new System.Drawing.Size(124, 23);
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
            this.btnLoadTables.Size = new System.Drawing.Size(685, 23);
            this.btnLoadTables.TabIndex = 6;
            this.btnLoadTables.Text = "Загрузить таблицы (в списке будут только различные и не исключённые)";
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
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.groupBox4);
            this.groupBox2.Controls.Add(this.groupBox3);
            this.groupBox2.Controls.Add(this.chkShowAllTables);
            this.groupBox2.Controls.Add(this.btnExcludeTable);
            this.groupBox2.Controls.Add(this.clbTables);
            this.groupBox2.Location = new System.Drawing.Point(848, 27);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(342, 771);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Таблицы";
            // 
            // btnDeleteTable
            // 
            this.btnDeleteTable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDeleteTable.Location = new System.Drawing.Point(6, 53);
            this.btnDeleteTable.Name = "btnDeleteTable";
            this.btnDeleteTable.Size = new System.Drawing.Size(171, 28);
            this.btnDeleteTable.TabIndex = 6;
            this.btnDeleteTable.Text = "Удалить выбранную таблицу";
            this.btnDeleteTable.UseVisualStyleBackColor = true;
            this.btnDeleteTable.Click += new System.EventHandler(this.btnDeleteTable_Click);
            // 
            // chkShowAllTables
            // 
            this.chkShowAllTables.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.chkShowAllTables.AutoSize = true;
            this.chkShowAllTables.Location = new System.Drawing.Point(153, 715);
            this.chkShowAllTables.Name = "chkShowAllTables";
            this.chkShowAllTables.Size = new System.Drawing.Size(156, 17);
            this.chkShowAllTables.TabIndex = 5;
            this.chkShowAllTables.Text = "Показывать все таблицы";
            this.chkShowAllTables.UseVisualStyleBackColor = true;
            this.chkShowAllTables.CheckedChanged += new System.EventHandler(this.ChkShowAllTables_CheckedChanged);
            // 
            // btnExcludeTable
            // 
            this.btnExcludeTable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExcludeTable.Location = new System.Drawing.Point(153, 739);
            this.btnExcludeTable.Name = "btnExcludeTable";
            this.btnExcludeTable.Size = new System.Drawing.Size(183, 23);
            this.btnExcludeTable.TabIndex = 4;
            this.btnExcludeTable.Text = "Исключение таблиц";
            this.btnExcludeTable.UseVisualStyleBackColor = true;
            // 
            // btnToggleCheckAll
            // 
            this.btnToggleCheckAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnToggleCheckAll.Location = new System.Drawing.Point(6, 19);
            this.btnToggleCheckAll.Name = "btnToggleCheckAll";
            this.btnToggleCheckAll.Size = new System.Drawing.Size(171, 28);
            this.btnToggleCheckAll.TabIndex = 3;
            this.btnToggleCheckAll.Text = "Выделить/Снять всё";
            this.btnToggleCheckAll.UseVisualStyleBackColor = true;
            // 
            // btnApplyWholeTable
            // 
            this.btnApplyWholeTable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnApplyWholeTable.Location = new System.Drawing.Point(6, 53);
            this.btnApplyWholeTable.Name = "btnApplyWholeTable";
            this.btnApplyWholeTable.Size = new System.Drawing.Size(171, 28);
            this.btnApplyWholeTable.TabIndex = 2;
            this.btnApplyWholeTable.Text = "Залить таблицу(ы) целиком";
            this.btnApplyWholeTable.UseVisualStyleBackColor = true;
            // 
            // btnCompareTable
            // 
            this.btnCompareTable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCompareTable.Location = new System.Drawing.Point(6, 19);
            this.btnCompareTable.Name = "btnCompareTable";
            this.btnCompareTable.Size = new System.Drawing.Size(171, 28);
            this.btnCompareTable.TabIndex = 1;
            this.btnCompareTable.Text = "Сравнить выбранную таблицу";
            this.btnCompareTable.UseVisualStyleBackColor = true;
            // 
            // clbTables
            // 
            this.clbTables.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.clbTables.ContextMenuStrip = this.contextMenuTables;
            this.clbTables.FormattingEnabled = true;
            this.clbTables.Location = new System.Drawing.Point(6, 23);
            this.clbTables.Name = "clbTables";
            this.clbTables.Size = new System.Drawing.Size(141, 739);
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
            this.dgvDiff.Location = new System.Drawing.Point(12, 171);
            this.dgvDiff.Name = "dgvDiff";
            this.dgvDiff.Size = new System.Drawing.Size(830, 589);
            this.dgvDiff.TabIndex = 2;
            // 
            // btnApplySelected
            // 
            this.btnApplySelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnApplySelected.Location = new System.Drawing.Point(712, 775);
            this.btnApplySelected.Name = "btnApplySelected";
            this.btnApplySelected.Size = new System.Drawing.Size(130, 23);
            this.btnApplySelected.TabIndex = 3;
            this.btnApplySelected.Text = "Записать выбранное";
            this.btnApplySelected.UseVisualStyleBackColor = true;
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(16, 780);
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
            this.progressBar.Location = new System.Drawing.Point(12, 775);
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
            // toolStripMenuItemAbout
            //
            this.toolStripMenuItemAbout.Name = "toolStripMenuItemAbout";
            this.toolStripMenuItemAbout.Size = new System.Drawing.Size(94, 20);
            this.toolStripMenuItemAbout.Text = "О программе";
            this.toolStripMenuItemAbout.Click += new System.EventHandler(this.ToolStripMenuItemAbout_Click);
            // 
            // toolStripMenuItemParallel
            // 
            this.toolStripMenuItemParallel.Name = "toolStripMenuItemParallel";
            this.toolStripMenuItemParallel.Size = new System.Drawing.Size(208, 22);
            this.toolStripMenuItemParallel.Text = "Параметры сравнения…";
            this.toolStripMenuItemParallel.Click += new System.EventHandler(this.ToolStripMenuItemParallel_Click);

            //
            // toolStripSeparatorSettings
            //
            this.toolStripSeparatorSettings.Name = "toolStripSeparatorSettings";
            this.toolStripSeparatorSettings.Size = new System.Drawing.Size(205, 6);

            //
            // toolStripMenuItemOpenLogs
            //
            this.toolStripMenuItemOpenLogs.Name = "toolStripMenuItemOpenLogs";
            this.toolStripMenuItemOpenLogs.Size = new System.Drawing.Size(208, 22);
            this.toolStripMenuItemOpenLogs.Text = "Открыть папку с логами";
            this.toolStripMenuItemOpenLogs.Click += new System.EventHandler(this.ToolStripMenuItemOpenLogs_Click);


            //
            // toolStripMenuItemSetLogs
            //
            this.toolStripMenuItemSetLogs.Name = "toolStripMenuItemSetLogs";
            this.toolStripMenuItemSetLogs.Size = new System.Drawing.Size(208, 22);
            this.toolStripMenuItemSetLogs.Text = "Задать папку с логами…";
            this.toolStripMenuItemSetLogs.Click += new System.EventHandler(this.ToolStripMenuItemSetLogs_Click);

            //
            // toolStripSeparatorSettings2
            //
            this.toolStripSeparatorSettings2.Name = "toolStripSeparatorSettings2";
            this.toolStripSeparatorSettings2.Size = new System.Drawing.Size(205, 6);
            //
            // toolStripMenuItemOpenConfig
            //
            this.toolStripMenuItemOpenConfig.Name = "toolStripMenuItemOpenConfig";
            this.toolStripMenuItemOpenConfig.Size = new System.Drawing.Size(208, 22);
            this.toolStripMenuItemOpenConfig.Text = "Открыть папку с конфигурацией";
            this.toolStripMenuItemOpenConfig.Click += new System.EventHandler(this.ToolStripMenuItemOpenConfigFolder_Click);

            //
            // toolStripMenuItemSetConfig
            //
            this.toolStripMenuItemSetConfig.Name = "toolStripMenuItemSetConfig";
            this.toolStripMenuItemSetConfig.Size = new System.Drawing.Size(208, 22);
            this.toolStripMenuItemSetConfig.Text = "Задать папку с конфигурацией…";
            this.toolStripMenuItemSetConfig.Click += new System.EventHandler(this.ToolStripMenuItemSetConfigFolder_Click);
            // 
            // btnToggleApplyAll
            // 
            this.btnToggleApplyAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnToggleApplyAll.Location = new System.Drawing.Point(580, 775);
            this.btnToggleApplyAll.Name = "btnToggleApplyAll";
            this.btnToggleApplyAll.Size = new System.Drawing.Size(130, 23);
            this.btnToggleApplyAll.TabIndex = 6;
            this.btnToggleApplyAll.Text = "Отметить / снять всё";
            this.btnToggleApplyAll.UseVisualStyleBackColor = true;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.btnCompareTable);
            this.groupBox3.Controls.Add(this.btnDeleteTable);
            this.groupBox3.Location = new System.Drawing.Point(153, 17);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(183, 89);
            this.groupBox3.TabIndex = 7;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "По выделению строки";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.btnToggleCheckAll);
            this.groupBox4.Controls.Add(this.btnApplyWholeTable);
            this.groupBox4.Location = new System.Drawing.Point(153, 112);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(183, 88);
            this.groupBox4.TabIndex = 8;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "По выбору галочек";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1214, 810);
            this.Controls.Add(this.menuStrip1);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.dgvDiff);
            this.Controls.Add(this.btnApplySelected);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnToggleApplyAll);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "Form1";
            this.Text = "MDB Diff Tool";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.contextMenuTables.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvDiff)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
            UiTheme.ApplyDark(this);
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
    }
}
