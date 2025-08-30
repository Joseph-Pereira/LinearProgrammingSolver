using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPSolver.Algorithms
{
    internal class CuttingPlane
    {
        public List<List<Dictionary<string, double>>> Iterations { get; private set; } = new List<List<Dictionary<string, double>>>();
        private int cutCount = 0;
        private const double Epsilon = 1e-6;

        public void Solve(LinearStorage storage)
        {
            var canonical = new CanonicalForm(storage);
            var primal = new PrimalSimplex();
            primal.SolveFromTable(canonical.Table);
            Iterations.AddRange(primal.Iterations);
            var table = Iterations.Last();

            while (true)
            {
                if (IsInteger(table)) break;

                int sourceRow = ChooseSourceRow(table);
                AddCut(table, sourceRow);
                Iterations.Add(CloneTable(table)); // Table after adding cut

                var dual = new DualSimplex();
                dual.SolveFromTable(table);
                Iterations.AddRange(dual.Iterations.Skip(1));

                table = Iterations.Last();
            }
        }

        private bool IsInteger(List<Dictionary<string, double>> table)
        {
            for (int i = 1; i < table.Count; i++)
            {
                double rhs = table[i]["RHS"];
                if (Math.Abs(rhs - Math.Round(rhs)) > Epsilon) return false;
            }
            return true;
        }

        private int ChooseSourceRow(List<Dictionary<string, double>> table)
        {
            var candidates = new List<(int row, double dist, int sub)>();

            for (int i = 1; i < table.Count; i++)
            {
                double rhs = table[i]["RHS"];
                double f = rhs - Math.Floor(rhs);
                if (f < Epsilon || f > 1 - Epsilon) continue;
                double dist = Math.Abs(f - 0.5);
                string basic = GetBasicVar(table, i);
                int sub = int.Parse(basic.Substring(1));
                candidates.Add((i, dist, sub));
            }

            if (candidates.Count == 0) throw new InvalidOperationException("No fractional rows found but should be");

            candidates.Sort((a, b) =>
            {
                int distComp = a.dist.CompareTo(b.dist);
                return distComp != 0 ? distComp : a.sub.CompareTo(b.sub);
            });

            return candidates[0].row;
        }

        private void AddCut(List<Dictionary<string, double>> table, int sourceRow)
        {
            cutCount++;
            string newSlack = $"g{cutCount}";
            double rhs = table[sourceRow]["RHS"];
            double f0 = rhs - Math.Floor(rhs);

            var newRow = new Dictionary<string, double>();
            foreach (var key in table[0].Keys.Where(k => k != "RHS"))
            {
                double a = table[sourceRow][key];
                double f = a - Math.Floor(a);
                newRow[key] = -f;
            }
            newRow[newSlack] = 1.0;
            newRow["RHS"] = -f0;

            for (int i = 0; i < table.Count; i++)
            {
                table[i][newSlack] = 0.0;
            }

            table.Add(newRow);
        }

        private string GetBasicVar(List<Dictionary<string, double>> table, int rowIndex)
        {
            foreach (var key in table[rowIndex].Keys.Where(k => k != "RHS"))
            {
                if (Math.Abs(table[rowIndex][key] - 1.0) > Epsilon) continue;

                bool isBasic = true;
                for (int r = 0; r < table.Count; r++)
                {
                    if (r == rowIndex) continue;
                    if (Math.Abs(table[r][key]) > Epsilon)
                    {
                        isBasic = false;
                        break;
                    }
                }
                if (isBasic) return key;
            }
            throw new InvalidOperationException("No basic variable found for row");
        }

        private List<Dictionary<string, double>> CloneTable(List<Dictionary<string, double>> table)
        {
            return table.Select(r => new Dictionary<string, double>(r)).ToList();
        }
    }
}
