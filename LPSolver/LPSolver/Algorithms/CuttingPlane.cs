using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            var table = CloneTable(Iterations.Last()); // Work with a copy to avoid modifying original

            while (true)
            {
                if (IsInteger(table)) break;

                int sourceRow = ChooseSourceRow(table);
                AddCut(table, sourceRow);
                Iterations.Add(CloneTable(table)); // Capture state after adding cut

                var dual = new DualSimplex();
                var dualTable = CloneTable(table); // Pass a copy to DualSimplex
                dual.SolveFromTable(dualTable);
                Iterations.AddRange(dual.Iterations.Skip(1));

                table = CloneTable(Iterations.Last()); // Update table with the latest state
            }

            // Write iterations and solution to file
            string filePath = "CuttingPlaneSolution.txt";
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Cutting Plane Algorithm Iterations:");
                for (int i = 0; i < Iterations.Count; i++)
                {
                    writer.WriteLine($"\nIteration {i}:");
                    PrintTableToFile(writer, Iterations[i]);
                }

                // Extract and display the final solution
                var finalTable = Iterations.Last();
                double z = -finalTable[0]["RHS"]; // Since obj row has negative coeffs
                Dictionary<string, double> solution = new Dictionary<string, double>();
                for (int row = 1; row < finalTable.Count; row++)
                {
                    string basicVar = GetBasicVar(finalTable, row);
                    if (basicVar.StartsWith("x"))
                    {
                        solution[basicVar] = finalTable[row]["RHS"];
                    }
                }

                writer.WriteLine("\nFinal Solution:");
                foreach (var kv in solution)
                {
                    writer.WriteLine($"{kv.Key} = {kv.Value}");
                }
                writer.WriteLine($"Optimal Z = {z}");
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
            string newSlack = $"c{cutCount}";
            double rhs = table[sourceRow]["RHS"];
            double f0 = rhs - Math.Floor(rhs);

            var newRow = new Dictionary<string, double>();
            var originalKeys = table[0].Keys.Where(k => k != "RHS").ToList(); // Get all keys except RHS

            // Populate new row with fractional parts for original variables
            foreach (var key in originalKeys)
            {
                double a = table[sourceRow][key];
                double f = a - Math.Floor(a);
                newRow[key] = -f;
            }
            newRow[newSlack] = 1.0; // Set the new slack variable coefficient
            newRow["RHS"] = -f0;    // Set the RHS value for the new row

            // Add the new slack variable to all existing rows, initializing to 0
            foreach (var row in table)
            {
                row[newSlack] = 0.0;
            }

            // Add the new row at the bottom
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
            // Create a deep copy of the table list and its dictionaries
            return table.Select(r => new Dictionary<string, double>(r)).ToList();
        }

        private void PrintTableToFile(StreamWriter writer, List<Dictionary<string, double>> table)
        {
            if (table.Count == 0) return;

            // Separate original variables, new slack variables, and RHS
            var originalKeys = table[0].Keys.Where(k => k != "RHS" && !k.StartsWith("c")).OrderBy(k => k).ToList();
            var newSlackKeys = table[0].Keys.Where(k => k.StartsWith("c")).OrderBy(k => k).ToList();
            var allKeys = originalKeys.Concat(newSlackKeys).Concat(new[] { "RHS" }).ToList(); // New slacks before RHS

            // Print header
            foreach (var key in allKeys)
            {
                writer.Write($"{key,-8}");
            }
            writer.WriteLine();

            // Print rows with proper formatting and alignment
            foreach (var row in table)
            {
                foreach (var key in allKeys)
                {
                    double val = row.ContainsKey(key) ? row[key] : 0;
                    writer.Write($"{val:F2}".PadLeft(8));
                }
                writer.WriteLine();
            }
        }
    }

}