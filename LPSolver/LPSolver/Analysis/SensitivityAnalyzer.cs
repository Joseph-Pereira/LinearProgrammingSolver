using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LPSolver;   

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
        public double[] DualPrices { get; set; }                           // y
        public Dictionary<string, double> ReducedCosts { get; set; }       // r_j
        public Dictionary<string, Range> RhsRanges { get; set; }           // Δb ranges (feasibility)
        public Dictionary<string, Range> CjRangesBasic { get; set; }       // Δc ranges for current basic vars
        public string[] BasisNames { get; set; }                           // names of basic columns
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
        /// Expects the FINAL simplex tableau:
        /// table[0] = objective row with -c_j (for Max) and RHS = z*.
        /// table[1..m] = constraint rows; each row has columns for all variables and "RHS".
        /// Columns keys are variable names; "RHS" is the right-hand side.
        /// </summary>
        public static SensitivityReport Run(List<Dictionary<string, double>> table, LinearStorage model)
        {
            if (table == null || table.Count < 2)
                throw new ArgumentException("Invalid final tableau.");

            // --- collect column names (excluding RHS) ---
            var colNames = new List<string>();
            foreach (var k in table[0].Keys)
                if (k != "RHS") colNames.Add(k);

            int m = table.Count - 1;
            int n = colNames.Count;

            // --- build c from the MODEL (not from row 0) ---
            // decision vars expected as x1..xn; slack/artificial/e/a/etc. get cost 0
            double[] cFromModel = new double[n];
            for (int j = 0; j < n; j++)
            {
                string name = colNames[j];
                double cj = 0.0;

                if (name.Length >= 2 && (name[0] == 'x' || name[0] == 'X'))
                {
                    int idx;
                    if (int.TryParse(name.Substring(1), out idx) &&
                        idx >= 1 && model.ObjectiveCoefficients != null &&
                        idx <= model.ObjectiveCoefficients.Length)
                    {
                        cj = model.ObjectiveCoefficients[idx - 1];
                    }
                }
                cFromModel[j] = cj;
            }

            // --- build A and b from tableau rows ---
            double[,] A = new double[m, n];
            double[] b = new double[m];
            for (int i = 0; i < m; i++)
            {
                var row = table[i + 1];
                for (int j = 0; j < n; j++) A[i, j] = row[colNames[j]];
                b[i] = row["RHS"];
            }

            // --- detect basis (unit columns) ---
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
                    else if (Math.Abs(v) > 1e-9)
                    {
                        isUnit = false; break;
                    }
                }
                if (isUnit && oneAt != -1) basisOfRow[oneAt] = j;
            }

            bool missing = false; for (int i = 0; i < m; i++) if (basisOfRow[i] < 0) { missing = true; break; }
            if (missing) throw new InvalidOperationException("Could not identify a valid basis from the tableau.");

            // --- form B and B^{-1} ---
            double[,] B = new double[m, m];
            for (int i = 0; i < m; i++)
            {
                int jB = basisOfRow[i];
                for (int r = 0; r < m; r++) B[r, i] = A[r, jB];
            }
            double[,] Binv = Invert(B);

            // --- y^T = c_B^T B^{-1} ---
            double[] cB = new double[m];
            for (int i = 0; i < m; i++) cB[i] = cFromModel[basisOfRow[i]];
            double[] y = MatVecLeft(cB, Binv);

            // --- reduced costs r_j = c_j - y^T a_j ---
            var reduced = new Dictionary<string, double>();
            for (int j = 0; j < n; j++)
            {
                double dot = 0.0;
                for (int i = 0; i < m; i++) dot += y[i] * A[i, j];
                reduced[colNames[j]] = cFromModel[j] - dot;
            }

            // --- RHS ranges (feasibility) ---
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

            // --- c-ranges for basic variables ---
            var cjRanges = new Dictionary<string, Range>();
            string[] basisNames = new string[m];
            for (int p = 0; p < m; p++)
            {
                int jB = basisOfRow[p];
                basisNames[p] = colNames[jB];

                double[] rp = new double[m];
                for (int j = 0; j < m; j++) rp[j] = Binv[p, j];

                double lo = double.NegativeInfinity, hi = double.PositiveInfinity;
                for (int j = 0; j < n; j++)
                {
                    if (j == jB) continue;
                    double dj = 0.0; for (int i = 0; i < m; i++) dj += rp[i] * A[i, j];
                    double r0 = reduced[colNames[j]];
                    if (Math.Abs(dj) < 1e-12) continue;
                    double bound = r0 / dj;      // keep r_j(Δ) <= 0 for max
                    if (dj > 0) { if (bound < hi) hi = bound; }
                    else { if (bound > lo) lo = bound; }
                }

                cjRanges[basisNames[p]] = new Range(
                    double.IsNegativeInfinity(lo) ? (double?)null : lo,
                    double.IsPositiveInfinity(hi) ? (double?)null : hi
                );
            }

            // --- strong duality ---
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


        private static double[] MatVec(double[,] M, double[] v)
        {
            int r = M.GetLength(0), c = M.GetLength(1);
            double[] y = new double[r];
            for (int i = 0; i < r; i++)
            {
                double s = 0.0;
                for (int j = 0; j < c; j++) s += M[i, j] * v[j];
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
                double s = 0.0;
                for (int i = 0; i < r; i++) s += vLeft[i] * M[i, j];
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
                // pivot
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
