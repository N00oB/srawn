using System;
using System.Windows.Forms;
using MdbDiffTool.Core; // пространство имён с AppConfig

namespace MdbDiffTool
{
    public partial class SettingsForm : Form
    {
        private readonly AppConfig _config;

        public SettingsForm(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            InitializeComponent();

            // события
            this.Load += SettingsForm_Load;
            btnOk.Click += btnOk_Click;
            btnCancel.Click += btnCancel_Click;

            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Настройки";
        }

        private void SettingsForm_Load(object sender, EventArgs e)
        {
            numericMaxParallel.Minimum = 1;
            numericMaxParallel.Maximum = 64;

            int value = _config.MaxParallelTables;
            if (value <= 0)
                value = 4;

            if (value < numericMaxParallel.Minimum)
                value = (int)numericMaxParallel.Minimum;
            if (value > numericMaxParallel.Maximum)
                value = (int)numericMaxParallel.Maximum;

            numericMaxParallel.Value = value;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            _config.MaxParallelTables = (int)numericMaxParallel.Value;
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
