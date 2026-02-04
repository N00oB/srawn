namespace MdbDiffTool
{
    partial class SettingsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.numericMaxParallel = new System.Windows.Forms.NumericUpDown();
            this.lblMaxParallel = new System.Windows.Forms.Label();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.numericMaxParallel)).BeginInit();
            this.SuspendLayout();
            // 
            // numericMaxParallel
            // 
            this.numericMaxParallel.Location = new System.Drawing.Point(15, 42);
            this.numericMaxParallel.Name = "numericMaxParallel";
            this.numericMaxParallel.Size = new System.Drawing.Size(126, 20);
            this.numericMaxParallel.TabIndex = 0;
            // 
            // lblMaxParallel
            // 
            this.lblMaxParallel.AutoSize = true;
            this.lblMaxParallel.Location = new System.Drawing.Point(12, 9);
            this.lblMaxParallel.Name = "lblMaxParallel";
            this.lblMaxParallel.Size = new System.Drawing.Size(331, 13);
            this.lblMaxParallel.TabIndex = 1;
            this.lblMaxParallel.Text = "Максимальное число потоков при пакетном сравнении таблиц:";
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(187, 39);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(75, 23);
            this.btnOk.TabIndex = 2;
            this.btnOk.Text = "ОК";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(268, 39);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Отмена";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // SettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(368, 83);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.lblMaxParallel);
            this.Controls.Add(this.numericMaxParallel);
            this.Name = "SettingsForm";
            this.Text = "SettingsForm";
            ((System.ComponentModel.ISupportInitialize)(this.numericMaxParallel)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
            UiTheme.ApplyDark(this);
        }

        #endregion

        private System.Windows.Forms.NumericUpDown numericMaxParallel;
        private System.Windows.Forms.Label lblMaxParallel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}