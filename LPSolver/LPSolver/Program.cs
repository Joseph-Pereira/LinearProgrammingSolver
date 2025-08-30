using LPSolver;
using LPSolver.Algorithms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace LPSolver
{
    internal class Program
    {
        [STAThread]
        static void Main()
        {
            TestCuttingPlaneAndSaveToFile();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HomeForm());



            string filePath = @"C:\Users\ferre\OneDrive - belgiumcampus.ac.za\LPR381\Project\LinearProgrammingSolver\LPSolver\LPSolver\InputFiles\Ex1.txt";

            var storage = InputParser.Parse(filePath);

            var solver = new DualSimplex();

            solver.Solve(storage);

            int iterationNumber = 0;
            foreach (var iteration in solver.Iterations)
            {
                Console.WriteLine($"Iteration {iterationNumber++}:");

                foreach (var row in iteration)
                {
                    foreach (var kvp in row)
                    {
                        Console.Write($"{kvp.Key}:{kvp.Value,8:F2} ");
                    }
                    Console.WriteLine();
                }

                Console.WriteLine(new string('-', 50));
            }

            Console.WriteLine("Done.");
        }

        //Testing the cutting plane:
        static void TestCuttingPlaneAndSaveToFile()
        {
            // Set up the problem from the slides
            var storage = new LinearStorage
            {
                ObjectiveCoefficients = new double[] { 8, 5 } // Max 8x1 + 5x2
            };

            storage.Constraints.Add(new Constraint
            {
                Coefficients = new double[] { 1, 1 },
                Sign = "<=",
                RHS = 6
            });

            storage.Constraints.Add(new Constraint
            {
                Coefficients = new double[] { 9, 5 },
                Sign = "<=",
                RHS = 45
            });

            var cutter = new CuttingPlane();
            cutter.Solve(storage);

            // Write iterations and solution to a text file
            string filePath = "CuttingPlaneSolution.txt";
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Cutting Plane Algorithm Iterations:");
                for (int i = 0; i < cutter.Iterations.Count; i++)
                {
                    writer.WriteLine($"\nIteration {i}:");
                    PrintTableToFile(writer, cutter.Iterations[i]);
                }

                // Extract and display the final solution
                var finalTable = cutter.Iterations.Last();
                double z = -finalTable[0]["RHS"]; // Since obj row has negative coeffs
                Dictionary<string, double> solution = new Dictionary<string, double>();
                for (int row = 1; row < finalTable.Count; row++)
                {
                    string basicVar = GetBasicVariable(finalTable, row);
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

            Console.WriteLine($"Solution has been saved to {filePath}");
        }

        private static void PrintTableToFile(StreamWriter writer, List<Dictionary<string, double>> table)
        {
            if (table.Count == 0) return;

            var allKeys = table.SelectMany(d => d.Keys).Distinct().Where(k => k != "RHS").OrderBy(k => k).ToList();
            allKeys.Add("RHS"); // RHS last

            // Print header
            foreach (var key in allKeys)
            {
                writer.Write($"{key,-8}");
            }
            writer.WriteLine();

            // Print rows
            foreach (var row in table)
            {
                foreach (var key in allKeys)
                {
                    double val = row.ContainsKey(key) ? row[key] : 0;
                    writer.Write($"{val:F2,-8}");
                }
                writer.WriteLine();
            }
        }

        private static string GetBasicVariable(List<Dictionary<string, double>> table, int rowIndex)
        {
            const double Epsilon = 1e-6;
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
            return null; // Should not happen
        }




    }


}
