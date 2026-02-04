using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace MdbDiffTool
{
    public partial class ExcludedTablesForm : Form
    {
        private readonly List<string> _items;

        // Результат диалога
        public List<string> ResultTables
        {
            get { return new List<string>(_items); }
        }

        public ExcludedTablesForm(IEnumerable<string> existing, string currentTableName = null)
        {
            InitializeComponent();

            _items = new List<string>();

            if (existing != null)
            {
                foreach (var t in existing)
                {
                    if (!string.IsNullOrWhiteSpace(t) &&
                        !_items.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase)))
                    {
                        _items.Add(t.Trim());
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(currentTableName))
            {
                txtName.Text = currentTableName;
                txtName.SelectAll();
            }

            RefreshList();
        }

        private void RefreshList()
        {
            lbTables.Items.Clear();
            foreach (var t in _items.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                lbTables.Items.Add(t);
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            var name = txtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            if (!_items.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
            {
                _items.Add(name);
                RefreshList();
            }

            txtName.Clear();
            txtName.Focus();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            var selected = lbTables.SelectedItem as string;
            if (selected == null)
                return;

            _items.RemoveAll(x => string.Equals(x, selected, StringComparison.OrdinalIgnoreCase));
            RefreshList();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
