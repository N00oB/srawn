namespace MdbDiffTool
{
    partial class ValueDiffForm
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

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.lblHeader = new System.Windows.Forms.Label();
            this.tabMain = new System.Windows.Forms.TabControl();
            this.tabPageText = new System.Windows.Forms.TabPage();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.lblSourceTitle = new System.Windows.Forms.Label();
            this.txtSource = new MdbDiffTool.SyncRichTextBox();
            this.lblTargetTitle = new System.Windows.Forms.Label();
            this.txtTarget = new MdbDiffTool.SyncRichTextBox();
            this.tabPageLines = new System.Windows.Forms.TabPage();
            this.dgvLineDiff = new System.Windows.Forms.DataGridView();
            this.tabMain.SuspendLayout();
            this.tabPageText.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.tabPageLines.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvLineDiff)).BeginInit();
            this.SuspendLayout();
            // 
            // lblHeader
            // 
            this.lblHeader.AutoEllipsis = true;
            this.lblHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblHeader.Location = new System.Drawing.Point(0, 0);
            this.lblHeader.Name = "lblHeader";
            this.lblHeader.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            this.lblHeader.Size = new System.Drawing.Size(1139, 24);
            this.lblHeader.TabIndex = 0;
            this.lblHeader.Text = "Таблица: ? | Колонка: ? | Ключ: ?";
            // 
            // tabMain
            // 
            this.tabMain.Controls.Add(this.tabPageText);
            this.tabMain.Controls.Add(this.tabPageLines);
            this.tabMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMain.Location = new System.Drawing.Point(0, 24);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(1139, 608);
            this.tabMain.TabIndex = 1;
            // 
            // tabPageText
            // 
            this.tabPageText.Controls.Add(this.splitMain);
            this.tabPageText.Location = new System.Drawing.Point(4, 22);
            this.tabPageText.Name = "tabPageText";
            this.tabPageText.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageText.Size = new System.Drawing.Size(1131, 582);
            this.tabPageText.TabIndex = 0;
            this.tabPageText.Text = "Текст";
            this.tabPageText.UseVisualStyleBackColor = true;
            // 
            // splitMain
            // 
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(3, 3);
            this.splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.txtSource);
            this.splitMain.Panel1.Controls.Add(this.lblSourceTitle);
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.txtTarget);
            this.splitMain.Panel2.Controls.Add(this.lblTargetTitle);
            this.splitMain.Size = new System.Drawing.Size(1125, 576);
            this.splitMain.SplitterDistance = 375;
            this.splitMain.TabIndex = 0;
            // 
            // lblSourceTitle
            // 
            this.lblSourceTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblSourceTitle.Location = new System.Drawing.Point(0, 0);
            this.lblSourceTitle.Name = "lblSourceTitle";
            this.lblSourceTitle.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.lblSourceTitle.Size = new System.Drawing.Size(375, 22);
            this.lblSourceTitle.TabIndex = 0;
            this.lblSourceTitle.Text = "Источник";
            // 
            // txtSource
            // 
            this.txtSource.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtSource.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSource.Location = new System.Drawing.Point(0, 22);
            this.txtSource.Name = "txtSource";
            this.txtSource.ReadOnly = true;
            this.txtSource.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
            this.txtSource.Size = new System.Drawing.Size(375, 554);
            this.txtSource.TabIndex = 1;
            this.txtSource.Text = "";
            this.txtSource.WordWrap = false;
            // 
            // lblTargetTitle
            // 
            this.lblTargetTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblTargetTitle.Location = new System.Drawing.Point(0, 0);
            this.lblTargetTitle.Name = "lblTargetTitle";
            this.lblTargetTitle.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.lblTargetTitle.Size = new System.Drawing.Size(746, 22);
            this.lblTargetTitle.TabIndex = 0;
            this.lblTargetTitle.Text = "Приёмник";
            // 
            // txtTarget
            // 
            this.txtTarget.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtTarget.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtTarget.Location = new System.Drawing.Point(0, 22);
            this.txtTarget.Name = "txtTarget";
            this.txtTarget.ReadOnly = true;
            this.txtTarget.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
            this.txtTarget.Size = new System.Drawing.Size(746, 554);
            this.txtTarget.TabIndex = 1;
            this.txtTarget.Text = "";
            this.txtTarget.WordWrap = false;
            // 
            // tabPageLines
            // 
            this.tabPageLines.Controls.Add(this.dgvLineDiff);
            this.tabPageLines.Location = new System.Drawing.Point(4, 22);
            this.tabPageLines.Name = "tabPageLines";
            this.tabPageLines.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageLines.Size = new System.Drawing.Size(1131, 582);
            this.tabPageLines.TabIndex = 1;
            this.tabPageLines.Text = "Строки";
            this.tabPageLines.UseVisualStyleBackColor = true;
            // 
            // dgvLineDiff
            // 
            this.dgvLineDiff.AllowUserToAddRows = false;
            this.dgvLineDiff.AllowUserToDeleteRows = false;
            this.dgvLineDiff.AllowUserToResizeRows = false;
            this.dgvLineDiff.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells;
            this.dgvLineDiff.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvLineDiff.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvLineDiff.Location = new System.Drawing.Point(3, 3);
            this.dgvLineDiff.MultiSelect = false;
            this.dgvLineDiff.Name = "dgvLineDiff";
            this.dgvLineDiff.ReadOnly = true;
            this.dgvLineDiff.RowHeadersVisible = false;
            this.dgvLineDiff.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvLineDiff.Size = new System.Drawing.Size(1125, 576);
            this.dgvLineDiff.TabIndex = 0;
            this.dgvLineDiff.SelectionChanged += new System.EventHandler(this.dgvLineDiff_SelectionChanged);
            this.dgvLineDiff.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvLineDiff_CellClick);
            // 
            // ValueDiffForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1139, 632);
            this.Controls.Add(this.tabMain);
            this.Controls.Add(this.lblHeader);
            this.Name = "ValueDiffForm";
            this.Text = "Сравнение значения";
            UiTheme.ApplyDark(this);
            this.tabMain.ResumeLayout(false);
            this.tabPageText.ResumeLayout(false);
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.tabPageLines.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvLineDiff)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lblHeader;
        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabPageText;
        private System.Windows.Forms.TabPage tabPageLines;
        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.Label lblSourceTitle;
        private System.Windows.Forms.Label lblTargetTitle;
        private System.Windows.Forms.DataGridView dgvLineDiff;
        private SyncRichTextBox txtSource;
        private SyncRichTextBox txtTarget;
    }
}
