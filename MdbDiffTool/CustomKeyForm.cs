using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace MdbDiffTool
{
    public partial class CustomKeyForm : Form
    {
        public List<string> SelectedColumns { get; }

        public CustomKeyForm(string tableName, IEnumerable<string> allColumns, IEnumerable<string> selectedColumns)
        {
            InitializeComponent();

            this.Text = "Ключ для " + tableName;
            lblCaption.Text = "Выберите столбцы, которые образуют ключ:";

            var selectedSet = new HashSet<string>(selectedColumns ?? Enumerable.Empty<string>(),
                                                  StringComparer.OrdinalIgnoreCase);

            foreach (var col in allColumns)
            {
                int idx = clbColumns.Items.Add(col, selectedSet.Contains(col));
            }

            SelectedColumns = new List<string>();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            foreach (var item in clbColumns.CheckedItems)
            {
                SelectedColumns.Add(item.ToString());
            }

            if (SelectedColumns.Count == 0)
            {
                if (MessageBox.Show(this,
                        "Вы не выбрали ни одного столбца. Ключ будет сброшен на режим \"все столбцы\". Продолжить?",
                        "Нет столбцов",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.No)
                {
                    SelectedColumns.Clear();
                    this.DialogResult = DialogResult.None;
                    return;
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }

}
