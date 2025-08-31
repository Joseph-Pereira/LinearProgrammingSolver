using LPSolver;
using LPSolver.Algorithms;
using System;
using System.Collections.Generic;
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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new HomeForm());

            try
            {
                // Example: pass file path via args or hardcode for testing
                string filePath = @"C:\School\Third year\LPR381\Project\Inputs\max  +5 +2.txt";

                Console.WriteLine($"Parsing input file: {filePath}");
                var storage = InputParser.Parse(filePath);

                Console.WriteLine("\n=== DUAL SIMPLEX ===");
                var dual = new DualSimplex();
                dual.Solve(storage);
                PrintIterations(dual.Iterations);

                Console.WriteLine("\n=== FINAL SOLUTION ===");
                PrintOptimal(dual.Iterations.Last(), storage.IsMaximization);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
            }


        }
        private static void PrintIterations(List<List<Dictionary<string, double>>> iterations)
        {
            for (int i = 0; i < iterations.Count; i++)
            {
                Console.WriteLine($"\n--- Iteration {i} ---");
                PrintTable(iterations[i]);
            }
        }

        private static void PrintTable(List<Dictionary<string, double>> table)
        {
            var headers = table[0].Keys.ToList();
            Console.WriteLine(string.Join("\t", headers));

            foreach (var row in table)
            {
                Console.WriteLine(string.Join("\t", headers.Select(h => row[h].ToString("0.###"))));
            }
        }

        private static void PrintOptimal(List<Dictionary<string, double>> table, bool isMax)
        {
            Console.WriteLine("\nOptimal Solution:");
            var objRow = table[0];
            double z = objRow["RHS"];
            Console.WriteLine($"Objective Value (Z) = {z} ({(isMax ? "Max" : "Min")})");

            foreach (var key in objRow.Keys.Where(k => k.StartsWith("x")))
            {
                double value = 0;
                // look for row where variable = 1
                for (int i = 1; i < table.Count; i++)
                {
                    if (Math.Abs(table[i][key] - 1) < 1e-9 &&
                        table[i].Count(kv => Math.Abs(kv.Value) > 1e-9) == 2) // only itself + RHS
                    {
                        value = table[i]["RHS"];
                        break;
                    }
                }
                Console.WriteLine($"{key} = {value}");
            }
        }
    }






}
