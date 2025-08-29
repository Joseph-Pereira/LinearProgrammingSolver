using LPSolver.Algorithms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LPSolver
{
    public partial class HomeForm : Form
    {
        private List<Dictionary<string, double>> _finalTableau;
        private double[] _xstar;
        private double _zstar;

        public HomeForm()
        {
            InitializeComponent();


        }

        private void HomeForm_Load(object sender, EventArgs e)
        {
            // algorithms
            cmbAlgo.Items.Clear();
            cmbAlgo.Items.Add("Primal Simplex");
            cmbAlgo.Items.Add("Dual Simplex");
            cmbAlgo.Items.Add("Branch & Bound (Knapsack)");
            cmbAlgo.Items.Add("Cutting Plane");
            cmbAlgo.SelectedIndex = 0;

            // Make both boxes monospace for clean alignment
            rtbPreview.Font = new Font("Consolas", 10f);
            rtbResults.Font = new Font("Consolas", 10f);
            rtbResults.WordWrap = false;  // keep columns aligned

            btnSensitivity.Enabled = false;
            btnExport.Enabled = false;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtFile.Text = ofd.FileName;
                    rtbPreview.Text = File.ReadAllText(ofd.FileName);

                    
                }
            }
        }



        private void rtbLog_TextChanged(object sender, EventArgs e)
        {

        }

        // Pretty-print a single tableau to the bottom RichTextBox
        private void PrintTableau(List<Dictionary<string, double>> T, string title)
        {
            if (T == null || T.Count == 0) return;

            var cols = T[0].Keys.ToList(); // includes "RHS"
            int w = 11;

            // Title
            rtbResults.AppendText(title + Environment.NewLine);
            rtbResults.AppendText(new string('=', title.Length) + Environment.NewLine);

            // Header
            rtbResults.AppendText(string.Join("", cols.Select(c => c.PadLeft(w))) + Environment.NewLine);
            rtbResults.AppendText(new string('-', w * cols.Count) + Environment.NewLine);

            // Rows
            foreach (var r in T)
                rtbResults.AppendText(string.Join("", cols.Select(c => r[c].ToString("0.000").PadLeft(w))) + Environment.NewLine);

            rtbResults.AppendText(Environment.NewLine);
        }



        private void btnRun_Click_1(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtFile.Text) || !File.Exists(txtFile.Text))
            {
                MessageBox.Show("Please select a valid input file first.");
                return;
            }

            rtbResults.Clear();
            string algo = cmbAlgo.SelectedItem?.ToString() ?? "Primal Simplex";

            try
            {
                // Parse model from file
                var model = InputParser.Parse(txtFile.Text);

                List<List<Dictionary<string, double>>> allIterations = null;

                // === Choose algorithm ===
                if (algo == "Primal Simplex")
                {
                    var solver = new LPSolver.Algorithms.PrimalSimplex();
                    solver.Solve(model);
                    allIterations = solver.Iterations; // <-- all tables
                }
                else if (algo == "Dual Simplex")
                {
                    var solver = new LPSolver.Algorithms.DualSimplex();
                    solver.Solve(model);
                    allIterations = solver.Iterations; // <-- all tables
                }
                else if (algo == "Branch & Bound (Knapsack)")
                {
                    // (No tableaux here—keep your current knapsack output)
                    var items = new List<LPSolver.Algorithms.KnapsackBnB.Variable>
            {
                new LPSolver.Algorithms.KnapsackBnB.Variable(1, 2, 11),
                new LPSolver.Algorithms.KnapsackBnB.Variable(2, 3, 8),
                new LPSolver.Algorithms.KnapsackBnB.Variable(3, 3, 6),
                new LPSolver.Algorithms.KnapsackBnB.Variable(4, 5, 14),
                new LPSolver.Algorithms.KnapsackBnB.Variable(5, 2, 10),
                new LPSolver.Algorithms.KnapsackBnB.Variable(6, 4, 10),
            };
                    double capacity = 40;

                    var solver = new LPSolver.Algorithms.KnapsackBnB.KnapsackSolver();
                    solver.Solve(items, capacity);

                    rtbResults.AppendText($"Optimal Value: {solver.OptimalValue}\n");
                    rtbResults.AppendText("Optimal Solution: " +
                        string.Join(", ", solver.OptimalSolution.OrderBy(kv => kv.Key)
                            .Select(kv => $"x{kv.Key}={kv.Value}")));
                    btnSensitivity.Enabled = false; // no simplex tableau for sensitivity here
                    btnExport.Enabled = true;
                    return;
                }
                else if (algo == "Cutting Plane")
                {
                    rtbResults.AppendText("Cutting Plane not yet implemented.");
                    btnSensitivity.Enabled = false;
                    btnExport.Enabled = false;
                    return;
                }

                // === Print ALL iteration tableaux ===
                if (allIterations != null && allIterations.Count > 0)
                {
                    for (int k = 0; k < allIterations.Count; k++)
                    {
                        var T = allIterations[k];
                        string title = (k == allIterations.Count - 1)
                            ? $"Iteration {k} (Final)"
                            : $"Iteration {k}";
                        PrintTableau(T, title);
                    }

                    // Keep final tableau for sensitivity/export (last iteration)
                    _finalTableau = allIterations.Last();
                    btnSensitivity.Enabled = true;
                    btnExport.Enabled = true;
                }
                else
                {
                    rtbResults.AppendText("No iterations to display.");
                    btnSensitivity.Enabled = false;
                    btnExport.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                rtbResults.Text = "[ERROR] " + ex.Message;
                MessageBox.Show(ex.Message, "Unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSensitivity_Click_1(object sender, EventArgs e)
        {
            if (_finalTableau == null)
            {
                MessageBox.Show("Solve a model first.");
                return;
            }

            // If/when you add SensitivityAnalyzer, uncomment:
            // var report = Analysis.SensitivityAnalyzer.Run(_finalTableau).AsText();
            // ResultsLine(); ResultsLine(report);
        }

        private void btnExport_Click_1(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(rtbResults.Text))
            {
                MessageBox.Show("No results to export.");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(sfd.FileName, rtbResults.Text);

                }
            }
        }
    }
}
