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
            //TestCuttingPlaneAndSaveToFile(); // this method is just to test the cutting plane (use as template for how to call the function)
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


}
