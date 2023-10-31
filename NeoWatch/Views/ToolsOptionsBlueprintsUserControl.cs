using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NeoWatch.Views
{
    public partial class ToolsOptionsBlueprintsUserControl : UserControl
    {
        public ToolsOptionsBlueprintsUserControl()
        {
            InitializeComponent();
        }

        internal BlueprintsOptionPage BlueprintsOptionPage;

        public void Initialize()
        {
            if (BlueprintsOptionPage.Blueprints != null)
            {
                dataGridView.Rows.Clear();
            }
            // this.dataGridView.Rows.Add(new object[] { "test1", "test2" });
        }

        private void button_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = "c:\\";
            openFileDialog1.Filter = "Neo Watch Template (*.*)|*.*";
            openFileDialog1.FilterIndex = 0;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string text = System.IO.File.ReadAllText(openFileDialog1.FileName);
                BlueprintsOptionPage.Blueprints = text;
            }

            Initialize();
        }
    }
}
