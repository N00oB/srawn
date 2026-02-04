using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MdbDiffTool
{
    internal class UiTheme
    {
        public static void ApplyDark(Form form)
        {
            var back = Color.FromArgb(45, 45, 48);
            var backAlt = Color.FromArgb(37, 37, 38);
            var fore = Color.Gainsboro;

            form.BackColor = back;
            form.ForeColor = fore;

            foreach (Control c in form.Controls)
            {
                ApplyDarkToControl(c, back, backAlt, fore);
            }
        }

        private static void ApplyDarkToControl(Control c, Color back, Color backAlt, Color fore)
        {
            if (c is GroupBox)
            {
                c.BackColor = back;
                c.ForeColor = fore;
            }
            else if (c is Button btn)
            {
                btn.BackColor = Color.FromArgb(63, 63, 70);
                btn.ForeColor = fore;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderColor = Color.FromArgb(104, 104, 104);
            }
            else if (c is TextBox || c is CheckedListBox)
            {
                c.BackColor = backAlt;
                c.ForeColor = fore;
            }
            else if (c is DataGridView dgv)
            {
                dgv.BackgroundColor = back;
                dgv.GridColor = Color.DimGray;

                dgv.DefaultCellStyle.BackColor = backAlt;
                dgv.DefaultCellStyle.ForeColor = fore;
                dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(70, 90, 120);
                dgv.DefaultCellStyle.SelectionForeColor = Color.White;

                dgv.ColumnHeadersDefaultCellStyle.BackColor = back;
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = fore;
                dgv.EnableHeadersVisualStyles = false;
            }
            else if (c is ProgressBar)
            {
                // прогрессбару даём фон формы, он всё равно рисуется системной темой
                c.BackColor = back;
            }
            else
            {
                c.BackColor = back;
                c.ForeColor = fore;
            }

            foreach (Control child in c.Controls)
            {
                ApplyDarkToControl(child, back, backAlt, fore);
            }

        }
    }
}
