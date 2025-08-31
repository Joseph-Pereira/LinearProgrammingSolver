using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LPSolver;             // for LinearStorage

namespace LPSolver.Analysis
{
    // Simple holder for a numeric range (Down, Up).
    public sealed class Range
    {
        public double? Down { get; set; }
        public double? Up { get; set; }
        public Range() { }
        public Range(double? down, double? up) { Down = down; Up = up; }
    }

    public sealed class SensitivityReport
    {
        public double[] DualPrices { get; set; }                           // y (shadow prices)
        public Dictionary<string, double> ReducedCosts { get; set; }       // r_j
        public Dictionary<string, Range> RhsRanges { get; set; }           // Δb feasibility ranges
        public Dictionary<string, Range> CjRangesBasic { get; set; }       // Δc ranges for BASIC vars
        public string[] BasisNames { get; set; }                           // basic column names
        public double ZStar { get; set; }
        public double bTy { get; set; }

        public SensitivityReport()
        {
            DualPrices = new double[0];
            ReducedCosts = new Dictionary<string, double>();
            RhsRanges = new Dictionary<string, Range>();
            CjRangesBasic = new Dictionary<string, Range>();
            BasisNames = new string[0];
        }
    }

    public static class SensitivityAnalyzer
    {
        /// <summary>
        /// Compute dual prices, reduced costs, RHS ranges, and Δc ranges for BASIC vars,
        /// using the final simplex tableau *and the original model coefficients*.
        /// </summary>
        public static SensitivityReport Run(List<Dictionary<string, double>> table, LinearStorage model)
        {
            if (table == null || table.Count < 2)
                throw new ArgumentException("Invalid final tableau.");

            // ----- columns (exclude RHS) -----
            var colNames = new List<string>();
            foreach (var k in table[0].Keys)
                if (k != "RHS") colNames.Add(k);

            int m = table.Count - 1;       // constraints
            int n = colNames.Count;        // all columns except RHS

            // ----- c from MODEL (not from row 0!) -----
            // decision vars are x1..xn; other columns (slack/artificial) get cost 0
            double[] cFromModel = new double[n];
            for (int j = 0; j < n; j++)
            {
                string name = colNames[j];
                double cj = 0.0;
                if (name.Length >= 2 && (name[0] == 'x' || name[0] == 'X'))
                {
                    int idx;
                    if (int.TryParse(name.Substring(1), out idx) &&
                        model.ObjectiveCoefficients != null &&
                        idx >= 1 && idx <= model.ObjectiveCoefficients.Length)
                    {
                        cj = model.ObjectiveCoefficients[idx - 1];
                    }
                }
                cFromModel[j] = cj;
            }

            // ----- A, b from tableau -----
            double[,] A = new double[m, n];
            double[] b = new double[m];
            for (int i = 0; i < m; i++)
            {
                var row = table[i + 1];
                for (int j = 0; j < n; j++) A[i, j] = row[colNames[j]];
                b[i] = row["RHS"];
            }

            // ----- detect basis (unit columns) -----
            int[] basisOfRow = new int[m];
            for (int ii = 0; ii < m; ii++) basisOfRow[ii] = -1;
            for (int j = 0; j < n; j++)
            {
                int oneAt = -1; bool isUnit = true;
                for (int i = 0; i < m; i++)
                {
                    double v = A[i, j];
                    if (Math.Abs(v - 1.0) < 1e-9)
                    {
                        if (oneAt != -1) { isUnit = false; break; }
                        oneAt = i;
                    }
                    else if (Math.Abs(v) > 1e-9) { isUnit = false; break; }
                }
                if (isUnit && oneAt != -1) basisOfRow[oneAt] = j;
            }
            bool missing = false; for (int i = 0; i < m; i++) if (basisOfRow[i] < 0) { missing = true; break; }
            if (missing) throw new InvalidOperationException("Could not identify a valid basis from the tableau.");

            // ----- B, B^{-1}, y^T = c_B^T B^{-1} -----
            double[,] B = new double[m, m];
            for (int i = 0; i < m; i++)
            {
                int jB = basisOfRow[i];
                for (int r = 0; r < m; r++) B[r, i] = A[r, jB];
            }
            double[,] Binv = Invert(B);

            double[] cB = new double[m];
            for (int i = 0; i < m; i++) cB[i] = cFromModel[basisOfRow[i]];
            double[] y = MatVecLeft(cB, Binv); // dual prices

            // ----- reduced costs r_j = c_j - y^T a_j -----
            var reduced = new Dictionary<string, double>();
            for (int j = 0; j < n; j++)
            {
                double dot = 0.0; for (int i = 0; i < m; i++) dot += y[i] * A[i, j];
                reduced[colNames[j]] = cFromModel[j] - dot;
            }

            // ----- RHS (Δb) ranges keeping feasibility -----
            double[] xB = MatVec(Binv, b);
            var rhsRanges = new Dictionary<string, Range>();
            for (int k = 0; k < m; k++)
            {
                double dmin = double.NegativeInfinity, dmax = double.PositiveInfinity;
                for (int i = 0; i < m; i++)
                {
                    double dik = Binv[i, k];
                    if (dik > 1e-12) dmin = Math.Max(dmin, -xB[i] / dik);
                    else if (dik < -1e-12) dmax = Math.Min(dmax, -xB[i] / dik);
                }
                rhsRanges["row" + (k + 1)] = new Range(
                    double.IsNegativeInfinity(dmin) ? (double?)null : dmin,
                    double.IsPositiveInfinity(dmax) ? (double?)null : dmax
                );
            }

            // ----- Δc ranges for BASIC vars -----
            var cjRanges = new Dictionary<string, Range>();
            string[] basisNames = new string[m];
            for (int p = 0; p < m; p++)
            {
                int jB = basisOfRow[p];
                basisNames[p] = colNames[jB];

                // row p of B^{-1}
                double[] rp = new double[m];
                for (int j = 0; j < m; j++) rp[j] = Binv[p, j];

                double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
                for (int j = 0; j < n; j++)
                {
                    if (j == jB) continue;
                    double dj = 0.0; for (int i = 0; i < m; i++) dj += rp[i] * A[i, j];
                    double r0 = reduced[colNames[j]];
                    if (Math.Abs(dj) < 1e-12) continue;
                    double bound = r0 / dj;                    // keep r_j(Δ) ≤ 0 (max case)
                    if (dj > 0) { if (bound < hi) hi = bound; }
                    else { if (bound > lo) lo = bound; }
                }

                cjRanges[basisNames[p]] = new Range(
                    double.IsNegativeInfinity(lo) ? (double?)null : lo,
                    double.IsPositiveInfinity(hi) ? (double?)null : hi
                );
            }

            // ----- strong duality check -----
            double zStar = table[0]["RHS"];
            double bTy = 0.0; for (int i = 0; i < m; i++) bTy += b[i] * y[i];

            var rep = new SensitivityReport();
            rep.DualPrices = y;
            rep.ReducedCosts = reduced;
            rep.RhsRanges = rhsRanges;
            rep.CjRangesBasic = cjRanges;
            rep.BasisNames = basisNames;
            rep.ZStar = zStar;
            rep.bTy = bTy;
            return rep;
        }

        // ----------------- Text section builders (mirrors your txt style) -----------------
        public static string BuildReducedCostsText(SensitivityReport r, int numDecisionVars)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Reduced costs:");
            // print decision vars first as x1..xn; then any extras in the map
            for (int j = 1; j <= numDecisionVars; j++)
            {
                string key = "x" + j.ToString();
                if (r.ReducedCosts.ContainsKey(key))
                    sb.AppendLine("  " + key + ": " + r.ReducedCosts[key].ToString("0.000"));
            }

            // extras (e.g., slacks) in stable order
            foreach (var kv in r.ReducedCosts.OrderBy(k => k.Key))
            {
                if (kv.Key.Length >= 2 && (kv.Key[0] == 'x' || kv.Key[0] == 'X')) continue;
                sb.AppendLine("  " + kv.Key + ": " + kv.Value.ToString("0.000"));
            }
            return sb.ToString();
        }

        public static string BuildShadowPricesText(SensitivityReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Shadow prices (y):");
            for (int i = 0; i < r.DualPrices.Length; i++)
                sb.AppendLine("  Constraint " + (i + 1) + ": " + r.DualPrices[i].ToString("0.000"));
            return sb.ToString();
        }

        // Δc ranges for BASIC vars (allowable change while basis optimal)
        public static string BuildObjectiveRangesText(SensitivityReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Objective coefficient ranges (Δc) for BASIC variables:");
            foreach (var name in r.BasisNames)
            {
                Range g = r.CjRangesBasic[name];
                string lo = g.Down.HasValue ? g.Down.Value.ToString("0.000") : "-inf";
                string hi = g.Up.HasValue ? g.Up.Value.ToString("0.000") : "+inf";
                sb.AppendLine("  " + name + ": [" + lo + " , " + hi + "]");
            }
            return sb.ToString();
        }

        // Δb feasibility ranges
        public static string BuildRhsRangesText(SensitivityReport r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RHS ranges (Δb that keep feasibility):");
            foreach (var kv in r.RhsRanges)
            {
                string lo = kv.Value.Down.HasValue ? kv.Value.Down.Value.ToString("0.000") : "-inf";
                string hi = kv.Value.Up.HasValue ? kv.Value.Up.Value.ToString("0.000") : "+inf";
                sb.AppendLine("  " + kv.Key + ": [" + lo + " , " + hi + "]");
            }
            return sb.ToString();
        }

        // ----------------- Small linear algebra helpers -----------------
        private static double[] MatVec(double[,] M, double[] v)
        {
            int r = M.GetLength(0), c = M.GetLength(1);
            double[] y = new double[r];
            for (int i = 0; i < r; i++)
            {
                double s = 0.0; for (int j = 0; j < c; j++) s += M[i, j] * v[j];
                y[i] = s;
            }
            return y;
        }

        private static double[] MatVecLeft(double[] vLeft, double[,] M) // vLeft^T * M
        {
            int r = M.GetLength(0), c = M.GetLength(1);
            if (vLeft.Length != r) throw new ArgumentException("Dimension mismatch.");
            double[] y = new double[c];
            for (int j = 0; j < c; j++)
            {
                double s = 0.0; for (int i = 0; i < r; i++) s += vLeft[i] * M[i, j];
                y[j] = s;
            }
            return y;
        }

        private static double[,] Invert(double[,] A)
        {
            int n = A.GetLength(0);
            double[,] M = (double[,])A.Clone();
            double[,] I = new double[n, n];
            for (int i = 0; i < n; i++) I[i, i] = 1.0;

            for (int k = 0; k < n; k++)
            {
                int piv = k; double best = Math.Abs(M[k, k]);
                for (int i = k + 1; i < n; i++)
                {
                    double val = Math.Abs(M[i, k]);
                    if (val > best) { best = val; piv = i; }
                }
                if (best < 1e-14) throw new InvalidOperationException("Singular matrix in inversion.");

                if (piv != k) { SwapRows(M, k, piv); SwapRows(I, k, piv); }

                double diag = M[k, k];
                for (int j = 0; j < n; j++) { M[k, j] /= diag; I[k, j] /= diag; }

                for (int i = 0; i < n; i++) if (i != k)
                    {
                        double f = M[i, k];
                        if (Math.Abs(f) < 1e-16) continue;
                        for (int j = 0; j < n; j++)
                        { M[i, j] -= f * M[k, j]; I[i, j] -= f * I[k, j]; }
                    }
            }
            return I;
        }

        private static void SwapRows(double[,] X, int r1, int r2)
        {
            int m = X.GetLength(1);
            for (int j = 0; j < m; j++)
            {
                double t = X[r1, j];
                X[r1, j] = X[r2, j];
                X[r2, j] = t;
            }
        }
    }
}
