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



            string filePath = @"C:\School\Third year\LPR381\Project\LinearProgrammingSolver\LPSolver\LPSolver\InputFiles\Ex1.txt";

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

       


    }


}
