namespace MdbDiffTool
{
    partial class ExcludedTablesForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ListBox lbTables;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblList;
        private System.Windows.Forms.Label lblNew;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lbTables = new System.Windows.Forms.ListBox();
            this.txtName = new System.Windows.Forms.TextBox();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblList = new System.Windows.Forms.Label();
            this.lblNew = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lblList
            // 
            this.lblList.AutoSize = true;
            this.lblList.Location = new System.Drawing.Point(12, 9);
            this.lblList.Name = "lblList";
            this.lblList.Size = new System.Drawing.Size(163, 13);
            this.lblList.TabIndex = 0;
            this.lblList.Text = "Исключаемые таблицы (имена)";
            // 
            // lbTables
            // 
            this.lbTables.FormattingEnabled = true;
            this.lbTables.IntegralHeight = false;
            this.lbTables.Location = new System.Drawing.Point(12, 25);
            this.lbTables.Name = "lbTables";
            this.lbTables.Size = new System.Drawing.Size(260, 160);
            this.lbTables.TabIndex = 1;
            // 
            // lblNew
            // 
            this.lblNew.AutoSize = true;
            this.lblNew.Location = new System.Drawing.Point(12, 196);
            this.lblNew.Name = "lblNew";
            this.lblNew.Size = new System.Drawing.Size(119, 13);
            this.lblNew.TabIndex = 2;
            this.lblNew.Text = "Добавить имя таблицы";
            // 
            // txtName
            // 
            this.txtName.Location = new System.Drawing.Point(12, 212);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(260, 20);
            this.txtName.TabIndex = 3;
            // 
            // btnAdd
            // 
            this.btnAdd.Location = new System.Drawing.Point(12, 238);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(120, 23);
            this.btnAdd.TabIndex = 4;
            this.btnAdd.Text = "Добавить";
            this.btnAdd.UseVisualStyleBackColor = true;
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            // 
            // btnRemove
            // 
            this.btnRemove.Location = new System.Drawing.Point(152, 238);
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size(120, 23);
            this.btnRemove.TabIndex = 5;
            this.btnRemove.Text = "Удалить выбранную";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler(this.btnRemove_Click);
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(116, 275);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 6;
            this.btnOk.Text = "OK";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(197, 275);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Отмена";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // ExcludedTablesForm
            // 
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(284, 311);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnRemove);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.txtName);
            this.Controls.Add(this.lblNew);
            this.Controls.Add(this.lbTables);
            this.Controls.Add(this.lblList);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ExcludedTablesForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Исключаемые таблицы";
            this.ResumeLayout(false);
            this.PerformLayout();
            UiTheme.ApplyDark(this);
        }
    }
}
