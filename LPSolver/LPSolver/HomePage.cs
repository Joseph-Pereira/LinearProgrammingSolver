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
using LPSolver.Analysis;  // for SensitivityAnalyzer, SensitivityReport, Range


namespace LPSolver
{
    public partial class HomeForm : Form
    {
        private List<Dictionary<string, double>> _finalTableau;
        private double[] _xstar;
        private double _zstar;
        private LinearStorage _lastModel;  

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
            cmbAlgo.Items.Add("Revise Simplex");
            cmbAlgo.Items.Add("Branch & Bound (General ILP)");
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
        // Parse LP/IP model and remember it for sensitivity (LP only)
        var model = InputParser.Parse(txtFile.Text);
        _lastModel = model;

        List<List<Dictionary<string, double>>> allIterations = null;

        if (algo == "Primal Simplex")
        {
            var solver = new LPSolver.Algorithms.PrimalSimplex();
            solver.Solve(model);
            allIterations = solver.Iterations;
        }
        else if (algo == "Dual Simplex")
        {
            var solver = new LPSolver.Algorithms.DualSimplex();
            solver.Solve(model);
            allIterations = solver.Iterations;
        }
        else if (algo == "Cutting Plane")
        {
            var cp = new LPSolver.Algorithms.CuttingPlane();
            cp.Solve(model);
            var cpIters = cp.Iterations;

            if (cpIters != null && cpIters.Count > 0)
            {
                for (int k = 0; k < cpIters.Count; k++)
                {
                    var T = cpIters[k];
                    string title = (k == cpIters.Count - 1)
                        ? $"Cutting Plane – Iteration {k} (Final)"
                        : $"Cutting Plane – Iteration {k}";
                    PrintTableau(T, title);
                }
                _finalTableau = cpIters.Last();
                btnSensitivity.Enabled = false;        // integer after cuts -> no LP sensitivity
                btnExport.Enabled = true;
            }
            else
            {
                rtbResults.AppendText("Cutting Plane produced no iterations." + Environment.NewLine);
                btnSensitivity.Enabled = false;
                btnExport.Enabled = false;
            }
            return;
        }
        else if (algo == "Branch & Bound (General ILP)")
        {
            // Uses your BranchAndBound class (dual simplex subproblems)
            var bb = new LPSolver.Algorithms.BranchAndBound();
            bb.Solve(model);   // prints canonical root + all subproblem iterations + best solution

            foreach (var line in bb.Output)
                rtbResults.AppendText(line + Environment.NewLine);

            _finalTableau = null;               // not an LP basis at the end
            btnSensitivity.Enabled = false;     // sensitivity not applicable for ILP
            btnExport.Enabled = true;
            return;
        }
                else if (algo == "Branch & Bound (Knapsack)")
                {
                    var (items, capacity) = LoadKnapsackFromFile(txtFile.Text);

                    if (items.Count == 0 || capacity <= 0)
                    {
                        rtbResults.AppendText("Invalid knapsack input file format.\n");
                        btnSensitivity.Enabled = false;
                        btnExport.Enabled = false;
                        return;
                    }

                    var ksolver = new LPSolver.Algorithms.KnapsackBnB.KnapsackSolver();
                    ksolver.Solve(items, capacity);

                    rtbResults.AppendText("=== 0/1 Knapsack (Branch & Bound) ===\n");
                    rtbResults.AppendText($"Capacity: {capacity:0.###}\n");
                    rtbResults.AppendText($"Optimal Value: {ksolver.OptimalValue:0.###}\n");
                    rtbResults.AppendText("Optimal Solution: " +
                        string.Join(", ",
                            ksolver.OptimalSolution
                                   .OrderBy(kv => kv.Key)
                                   .Select(kv => $"x{kv.Key}={kv.Value}")) + "\n");
                 

                    _finalTableau = null;
                    btnSensitivity.Enabled = false;
                    btnExport.Enabled = true;
                    return;
                }



                // === Print ALL iteration tableaux for Simplex ===
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

            _finalTableau = allIterations.Last(); // for LP sensitivity / export
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
            if (_finalTableau == null || _lastModel == null)
            {
                MessageBox.Show("Run a simplex solve first.");
                return;
            }

            var report = LPSolver.Analysis.SensitivityAnalyzer.Run(_finalTableau, _lastModel);

            rtbResults.AppendText(Environment.NewLine + "=== Sensitivity & Duality ===" + Environment.NewLine);

            // 1) Shadow prices
            rtbResults.AppendText(
                LPSolver.Analysis.SensitivityAnalyzer.BuildShadowPricesText(report));

            // 2) Reduced costs (pass number of decision variables)
            rtbResults.AppendText(
                LPSolver.Analysis.SensitivityAnalyzer.BuildReducedCostsText(
                    report, _lastModel.ObjectiveCoefficients.Length));

            // 3) Objective coefficient ranges (for BASIC vars)
            rtbResults.AppendText(
                LPSolver.Analysis.SensitivityAnalyzer.BuildObjectiveRangesText(report));

            // 4) RHS feasibility ranges
            rtbResults.AppendText(
                LPSolver.Analysis.SensitivityAnalyzer.BuildRhsRangesText(report));

            // 5) Strong duality check
            double diff = Math.Abs(report.ZStar - report.bTy);
            rtbResults.AppendText(
                "Strong duality: z*=" + report.ZStar.ToString("0.000") +
                ", b^T y=" + report.bTy.ToString("0.000") +
                ", |diff|=" + diff.ToString("0.000") + Environment.NewLine);
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
        private (List<LPSolver.Algorithms.KnapsackBnB.Variable> items, double capacity)
    LoadKnapsackFromFile(string path)
        {
            var items = new List<LPSolver.Algorithms.KnapsackBnB.Variable>();
            double capacity = 0;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                if (line.StartsWith("capacity", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2 && double.TryParse(parts[1], out double cap))
                        capacity = cap;
                    continue;
                }

                var tokens = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 3)
                {
                    if (int.TryParse(tokens[0], out int idx) &&
                        double.TryParse(tokens[1], out double val) &&
                        double.TryParse(tokens[2], out double wt))
                    {
                        items.Add(new LPSolver.Algorithms.KnapsackBnB.Variable(idx, val, wt));
                    }
                }
            }

            return (items, capacity);
        }


        private void rtbPreview_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
