using System;
using System.IO;
using System.Windows.Forms;

namespace LPSolver
{
    public partial class ResultForm : Form
    {
        private Button btnSave, btnClose;

        public ResultForm()
        {
            InitializeComponent();
           
        }


        private void BtnSave_Click(object sender, EventArgs e, SaveFileDialog sfd)
        {
            if (sfd.ShowDialog(this) == DialogResult.OK)
                File.WriteAllText(sfd.FileName, resultBox.Text);
        }

        // === Public API for HomeForm ===
        public void SetText(string text) => resultBox.Text = text;
        public void Append(string text) => resultBox.AppendText(text);
        public void ClearText() => resultBox.Clear();

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void FormResults_Load(object sender, EventArgs e)
        {

        }
    }
}
