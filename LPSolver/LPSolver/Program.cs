using LPSolver;
using LPSolver.Algorithms;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HomeForm());

            try
            {


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



        //Testing the cutting plane:
        //static void TestCuttingPlaneAndSaveToFile()
        //{
        //    // Set up the problem from the slides
        //    var storage = new LinearStorage
        //    {
        //        ObjectiveCoefficients = new double[] { 8, 5 } // Max 8x1 + 5x2
        //    };

        //    storage.Constraints.Add(new Constraint
        //    {
        //        Coefficients = new double[] { 1, 1 },
        //        Sign = "<=",
        //        RHS = 6
        //    });

        //    storage.Constraints.Add(new Constraint
        //    {
        //        Coefficients = new double[] { 9, 5 },
        //        Sign = "<=",
        //        RHS = 45
        //    });

        //    var cutter = new CuttingPlane();
        //    cutter.Solve(storage);

        //    Console.WriteLine("Solution has been saved to CuttingPlaneSolution.txt");
        //}






}
