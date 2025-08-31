using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LPSolver;

namespace LPSolver.Algorithms
{
    // Class for implementing Branch and Bound using Dual Simplex for Integer Linear Programming
    // Displays canonical form for root, all table iterations for each sub-problem, and the best integer solution
    internal class BranchAndBound
    {
        // List to store output strings for display (canonical forms, iterations, branching info, etc.)
        public List<string> Output { get; private set; } = new List<string>();

        // Best integer objective found (initially -inf for maximization)
        private double BestObjective { get; set; } = double.NegativeInfinity;

        // Best integer solution found
        public Dictionary<string, double> BestSolution { get; private set; } = null;

        // Solve the ILP using Branch and Bound
        public void Solve(LinearStorage originalStorage)
        {
            // Display the canonical form for the root node
            var rootCanonical = new CanonicalForm(originalStorage);
            Output.Add("Canonical Form (Root Node):");
            Output.Add(DisplayTable(rootCanonical.Table));

            // Start processing from the root node
            ProcessNode(originalStorage, double.NegativeInfinity);

            // At the end, display the best candidate
            if (BestSolution != null)
            {
                Output.Add("Best Integer Solution Found:");
                Output.Add(DisplaySolution(BestSolution));
                Output.Add($"Objective Value: {BestObjective}");
            }
            else
            {
                Output.Add("No feasible integer solution found.");
            }
        }

        // Recursive method to process a node in the B&B tree
        private void ProcessNode(LinearStorage storage, double incumbent)
        {
            Output.Add("Processing new node...");

            // Solve the LP relaxation using Dual Simplex
            var solver = new DualSimplex();
            List<List<Dictionary<string, double>>> iterations;
            List<Dictionary<string, double>> optimalTable;
            double objective;

            try
            {
                solver.Solve(storage);
                iterations = solver.Iterations;
                optimalTable = iterations.Last();
                objective = optimalTable[0]["RHS"];  // Objective value from the tableau

                // Display all iterations for this sub-problem
                Output.Add("Table Iterations for this sub-problem:");
                foreach (var it in iterations)
                {
                    Output.Add(DisplayTable(it));
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Infeasible"))
            {
                Output.Add("Sub-problem is infeasible. Fathoming node.");
                return;  // Fathom by infeasibility
            }
            catch (InvalidOperationException ex) when (ex.Message == "Unbounded")
            {
                Output.Add("Sub-problem is unbounded. Assuming ILP is unbounded or handle accordingly.");
                return;  // Fathom or handle unbounded case (assuming bounded problems for now)
            }
            catch (Exception ex)
            {
                Output.Add($"Error solving sub-problem: {ex.Message}");
                return;
            }

            // Prune by bound: if relaxation objective <= current best integer, no better solution here
            if (objective <= BestObjective)
            {
                Output.Add($"Pruning node by bound: Relaxation obj {objective} <= Best {BestObjective}");
                return;
            }

            // Extract the solution from the optimal tableau
            var solution = GetSolution(optimalTable);

            // Check for degeneracy: simplistic check if any basic RHS is zero (but code already handles some cases)
            bool degenerate = false;
            for (int i = 1; i < optimalTable.Count; i++)
            {
                if (Math.Abs(optimalTable[i]["RHS"]) < 1e-6)
                {
                    degenerate = true;
                    break;
                }
            }
            if (degenerate)
            {
                Output.Add("Warning: Degeneracy detected (zero RHS in constraint). Proceeding.");
            }

            // Check if solution is integer for all decision variables (x1, x2, ...)
            bool isInteger = true;
            foreach (var kv in solution.Where(kv => kv.Key.StartsWith("x")))
            {
                double val = kv.Value;
                if (Math.Abs(val - Math.Round(val)) > 1e-6)
                {
                    isInteger = false;
                    break;
                }
            }

            if (isInteger)
            {
                // Integer solution found, update best if better
                if (objective > BestObjective)
                {
                    BestObjective = objective;
                    BestSolution = new Dictionary<string, double>(solution);
                    Output.Add($"New best integer solution found with obj {BestObjective}");
                }
                Output.Add("Fathoming node: Integer solution.");
                return;
            }

            // Branch: find fractional decision variable with frac part closest to 0.5
            string branchVar = null;
            double minDist = double.PositiveInfinity;
            double branchVal = 0;
            foreach (var kv in solution.Where(kv => kv.Key.StartsWith("x")))
            {
                double val = kv.Value;
                if (Math.Abs(val - Math.Round(val)) <= 1e-6) continue;  // Skip integers
                double frac = val - Math.Floor(val);
                double dist = Math.Abs(frac - 0.5);
                if (dist < minDist)
                {
                    minDist = dist;
                    branchVar = kv.Key;
                    branchVal = val;
                }
            }

            if (branchVar == null)
            {
                Output.Add("No fractional variable found, but not integer? Error.");
                return;
            }

            int floorVal = (int)Math.Floor(branchVal);
            int ceilVal = floorVal + 1;
            int varIndex = int.Parse(branchVar.Substring(1)) - 1;  // x1 -> index 0

            Output.Add($"Branching on {branchVar} with value {branchVal} (closest to 0.5 frac)");

            // Left branch: x <= floor
            {
                var leftStorage = CloneStorage(storage);
                double[] coefs = new double[storage.ObjectiveCoefficients.Length];
                coefs[varIndex] = 1;
                leftStorage.Constraints.Add(new Constraint { Coefficients = coefs, Sign = "<=", RHS = floorVal });
                Output.Add($"Left branch: {branchVar} <= {floorVal}");
                ProcessNode(leftStorage, BestObjective);
            }

            // Right branch: x >= ceil
            {
                var rightStorage = CloneStorage(storage);
                double[] coefs = new double[storage.ObjectiveCoefficients.Length];
                coefs[varIndex] = 1;
                rightStorage.Constraints.Add(new Constraint { Coefficients = coefs, Sign = ">=", RHS = ceilVal });
                Output.Add($"Right branch: {branchVar} >= {ceilVal}");
                ProcessNode(rightStorage, BestObjective);
            }
        }

        // Clone the LinearStorage for a new sub-problem
        private LinearStorage CloneStorage(LinearStorage storage)
        {
            return new LinearStorage
            {
                ObjectiveCoefficients = (double[])storage.ObjectiveCoefficients.Clone(),
                Constraints = storage.Constraints.Select(c => new Constraint
                {
                    Coefficients = (double[])c.Coefficients.Clone(),
                    Sign = c.Sign,
                    RHS = c.RHS
                }).ToList()
            };
        }

        // Extract variable values from the optimal tableau
        private Dictionary<string, double> GetSolution(List<Dictionary<string, double>> table)
        {
            var solution = new Dictionary<string, double>();
            var vars = table[0].Keys.Where(k => k != "RHS").ToList();
            int numRows = table.Count;
            int m = numRows - 1;

            foreach (var varName in vars)
            {
                // Collect column
                double[] col = new double[numRows];
                for (int r = 0; r < numRows; r++)
                {
                    col[r] = table[r][varName];
                }

                // Check if basic
                if (Math.Abs(col[0]) > 1e-6)  // Not zero in obj row
                {
                    solution[varName] = 0;
                    continue;
                }

                int oneCount = 0;
                int basicRow = -1;
                for (int r = 1; r < numRows; r++)
                {
                    double val = col[r];
                    if (Math.Abs(val) < 1e-6)
                    {
                        continue;
                    }
                    else if (Math.Abs(val - 1) < 1e-6)
                    {
                        oneCount++;
                        basicRow = r;
                    }
                    else
                    {
                        oneCount = 2;  // Not unit
                        break;
                    }
                }

                if (oneCount == 1)
                {
                    solution[varName] = table[basicRow]["RHS"];
                }
                else
                {
                    solution[varName] = 0;
                }
            }

            return solution;
        }

        // Display a tableau as a string
        private string DisplayTable(List<Dictionary<string, double>> table)
        {
            var sb = new StringBuilder();
            var keys = table[0].Keys.ToList();  
            sb.AppendLine(string.Join("\t", keys));

            foreach (var row in table)
            {
                var values = keys.Select(k => row.ContainsKey(k) ? row[k].ToString("F4") : "0");
                sb.AppendLine(string.Join("\t", values));
            }

            return sb.ToString();
        }

        // Display a solution dictionary as a string
        private string DisplaySolution(Dictionary<string, double> solution)
        {
            var sb = new StringBuilder();
            foreach (var kv in solution.OrderBy(k => k.Key))
            {
                sb.AppendLine($"{kv.Key}: {kv.Value}");
            }
            return sb.ToString();
        }
    }

   
}

