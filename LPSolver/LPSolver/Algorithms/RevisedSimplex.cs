using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPSolver.Algorithms
{
    
    internal class RevisedSimplex
    {
        private const double EPSILON = 1e-10;

        
        public List<RevisedSimplexIteration> Iterations { get; private set; } = new List<RevisedSimplexIteration>();

        
        public void Solve(LinearStorage storage)
        {
            
            var canonical = new CanonicalForm(storage);
            SolveFromCanonical(canonical.Table);
        }

       
        public void SolveFromCanonical(List<Dictionary<string, double>> table)
        {
            try
            {
                var setup = InitializeFromTable(table);
                int iterationCount = 0;

                Console.WriteLine("=== REVISED PRIMAL SIMPLEX ALGORITHM ===");
                Console.WriteLine($"Initial basis: {string.Join(", ", setup.BasicVariableNames)}");

                while (iterationCount < 100) 
                {
                    iterationCount++;
                    Console.WriteLine($"\n--- ITERATION {iterationCount} ---");

                    
                    double[] basicSolution = MultiplyMatrixVector(setup.BasisInverse, setup.RHS);

                    
                    var priceOutResult = PerformPriceOut(setup, table);

                   
                    var iteration = new RevisedSimplexIteration
                    {
                        IterationNumber = iterationCount,
                        BasicVariableNames = new List<string>(setup.BasicVariableNames),
                        BasicSolution = (double[])basicSolution.Clone(),
                        BasisInverse = CloneMatrix(setup.BasisInverse),
                        ShadowPrices = (double[])priceOutResult.ShadowPrices.Clone(),
                        ReducedCosts = new Dictionary<string, double>(priceOutResult.ReducedCosts),
                        ObjectiveValue = priceOutResult.ObjectiveValue,
                        IsOptimal = priceOutResult.IsOptimal,
                        EnteringVariable = null,
                        LeavingVariable = null
                    };

                    
                    DisplayProductForm(iteration);
                    DisplayPriceOut(iteration);

                  
                    if (priceOutResult.IsOptimal)
                    {
                        Console.WriteLine("*** OPTIMAL SOLUTION FOUND ***");
                        Iterations.Add(iteration);
                        break;
                    }

                    
                    string enteringVar = FindEnteringVariable(priceOutResult.ReducedCosts);
                    if (enteringVar == null)
                    {
                        Console.WriteLine("Error: No entering variable found");
                        break;
                    }

                    iteration.EnteringVariable = enteringVar;
                    Console.WriteLine($"Entering variable: {enteringVar}");

                 
                    double[] directionVector = CalculateDirectionVector(setup, enteringVar, table);

                 
                    var ratioResult = PerformRatioTest(basicSolution, directionVector, setup.BasicVariableNames);

                    if (ratioResult.LeavingIndex == -1)
                    {
                        Console.WriteLine("*** UNBOUNDED SOLUTION ***");
                        iteration.IsUnbounded = true;
                        Iterations.Add(iteration);
                        break;
                    }

                    string leavingVar = setup.BasicVariableNames[ratioResult.LeavingIndex];
                    iteration.LeavingVariable = leavingVar;
                    iteration.PivotRatio = ratioResult.MinRatio;

                    Console.WriteLine($"Leaving variable: {leavingVar} (ratio: {ratioResult.MinRatio:F3})");

               
                    UpdateBasisInverse(setup, directionVector, ratioResult.LeavingIndex);

                  
                    setup.BasicVariableNames[ratioResult.LeavingIndex] = enteringVar;

                    Console.WriteLine($"New basis: {string.Join(", ", setup.BasicVariableNames)}");

                    Iterations.Add(iteration);
                }

                if (iterationCount >= 100)
                {
                    Console.WriteLine("Maximum iterations reached - possible cycling");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Revised Simplex: {ex.Message}");
                throw;
            }
        }

        
        private RevisedSimplexSetup InitializeFromTable(List<Dictionary<string, double>> table)
        {
            var setup = new RevisedSimplexSetup();

       
            var allVariables = table[0].Keys.Where(k => k != "RHS").ToList();

            int numConstraints = table.Count - 1;

            
            setup.BasicVariableNames = new List<string>();

           
            foreach (var varName in allVariables)
            {
                if (varName.StartsWith("s") || varName.StartsWith("e"))
                {
                    setup.BasicVariableNames.Add(varName);
                }
            }

            
            if (setup.BasicVariableNames.Count < numConstraints)
            {
                foreach (var varName in allVariables)
                {
                    if (setup.BasicVariableNames.Contains(varName)) continue;

                    if (IsUnitColumn(table, varName) && setup.BasicVariableNames.Count < numConstraints)
                    {
                        setup.BasicVariableNames.Add(varName);
                    }
                }
            }

           
            setup.BasisInverse = CreateIdentityMatrix(numConstraints);

         
            setup.RHS = new double[numConstraints];
            for (int i = 0; i < numConstraints; i++)
            {
                setup.RHS[i] = table[i + 1]["RHS"];
            }

          
            setup.OriginalTable = table;

            return setup;
        }

        
        private PriceOutResult PerformPriceOut(RevisedSimplexSetup setup, List<Dictionary<string, double>> table)
        {
            var result = new PriceOutResult();

            
            double[] basicCosts = new double[setup.BasicVariableNames.Count];
            for (int i = 0; i < setup.BasicVariableNames.Count; i++)
            {
                string varName = setup.BasicVariableNames[i];
                basicCosts[i] = table[0][varName];
            }

            
            result.ShadowPrices = MultiplyTransposedMatrixVector(setup.BasisInverse, basicCosts);

            
            result.ReducedCosts = new Dictionary<string, double>();
            var allVariables = table[0].Keys.Where(k => k != "RHS").ToList();

            foreach (string varName in allVariables)
            {
                if (setup.BasicVariableNames.Contains(varName))
                {
                    result.ReducedCosts[varName] = 0.0; 
                }
                else
                {
                   
                    double[] column = new double[setup.RHS.Length];
                    for (int i = 0; i < setup.RHS.Length; i++)
                    {
                        column[i] = table[i + 1][varName];
                    }

                   
                    double originalCost = table[0][varName];
                    double reducedCost = originalCost - DotProduct(result.ShadowPrices, column);
                    result.ReducedCosts[varName] = reducedCost;
                }
            }

        
            double[] basicSolution = MultiplyMatrixVector(setup.BasisInverse, setup.RHS);
            result.ObjectiveValue = DotProduct(basicCosts, basicSolution);

           
            result.IsOptimal = true;
            foreach (var kvp in result.ReducedCosts)
            {
                if (!setup.BasicVariableNames.Contains(kvp.Key) && kvp.Value < -EPSILON)
                {
                    result.IsOptimal = false;
                    break;
                }
            }

            return result;
        }

      
        private string FindEnteringVariable(Dictionary<string, double> reducedCosts)
        {
            string enteringVar = null;
            double mostNegative = -EPSILON;

            foreach (var kvp in reducedCosts)
            {
                if (kvp.Value < mostNegative)
                {
                    mostNegative = kvp.Value;
                    enteringVar = kvp.Key;
                }
            }

            return enteringVar;
        }

      
        private double[] CalculateDirectionVector(RevisedSimplexSetup setup, string enteringVar,
            List<Dictionary<string, double>> table)
        {
           
            double[] enteringColumn = new double[setup.RHS.Length];
            for (int i = 0; i < setup.RHS.Length; i++)
            {
                enteringColumn[i] = table[i + 1][enteringVar];
            }

            return MultiplyMatrixVector(setup.BasisInverse, enteringColumn);
        }

        
        private RatioTestResult PerformRatioTest(double[] basicSolution, double[] directionVector,
            List<string> basicVariableNames)
        {
            var result = new RatioTestResult();
            result.LeavingIndex = -1;
            result.MinRatio = double.PositiveInfinity;

            for (int i = 0; i < basicSolution.Length; i++)
            {
                if (directionVector[i] > EPSILON)
                {
                    double ratio = basicSolution[i] / directionVector[i];
                    if (ratio < result.MinRatio - EPSILON)
                    {
                        result.MinRatio = ratio;
                        result.LeavingIndex = i;
                    }
                }
            }

            return result;
        }

      
        private void UpdateBasisInverse(RevisedSimplexSetup setup, double[] directionVector, int leavingIndex)
        {
            int m = setup.BasisInverse.GetLength(0);
            double pivotElement = directionVector[leavingIndex];

           
            double[,] etaMatrix = CreateIdentityMatrix(m);

            for (int i = 0; i < m; i++)
            {
                if (i == leavingIndex)
                {
                    etaMatrix[i, leavingIndex] = 1.0 / pivotElement;
                }
                else
                {
                    etaMatrix[i, leavingIndex] = -directionVector[i] / pivotElement;
                }
            }

      
            setup.BasisInverse = MultiplyMatrices(etaMatrix, setup.BasisInverse);
        }

      
        private void DisplayProductForm(RevisedSimplexIteration iteration)
        {
            Console.WriteLine("\n--- PRODUCT FORM OF INVERSE ---");
            Console.WriteLine($"Current Basis Inverse B^-1:");

            for (int i = 0; i < iteration.BasisInverse.GetLength(0); i++)
            {
                Console.Write("[ ");
                for (int j = 0; j < iteration.BasisInverse.GetLength(1); j++)
                {
                    Console.Write($"{iteration.BasisInverse[i, j],8:F3} ");
                }
                Console.WriteLine("]");
            }

            Console.WriteLine($"\nBasic Solution x_B = B^-1 * b:");
            for (int i = 0; i < iteration.BasicSolution.Length; i++)
            {
                Console.WriteLine($"{iteration.BasicVariableNames[i]} = {iteration.BasicSolution[i]:F3}");
            }
        }

      
        private void DisplayPriceOut(RevisedSimplexIteration iteration)
        {
            Console.WriteLine("\n--- PRICE OUT OPERATION ---");

            Console.Write("Shadow Prices y = c_B^T * B^-1: [ ");
            foreach (double price in iteration.ShadowPrices)
            {
                Console.Write($"{price,8:F3} ");
            }
            Console.WriteLine("]");

            Console.WriteLine("\nReduced Costs:");
            foreach (var kvp in iteration.ReducedCosts.OrderBy(x => x.Key))
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value,8:F3}");
            }

            Console.WriteLine($"\nCurrent Objective Value: {iteration.ObjectiveValue:F3}");
        }

      
        private bool IsUnitColumn(List<Dictionary<string, double>> table, string varName)
        {
            int numConstraints = table.Count - 1;
            int unitPosition = -1;

            for (int i = 1; i <= numConstraints; i++)
            {
                double value = table[i][varName];

                if (Math.Abs(value - 1.0) < EPSILON)
                {
                    if (unitPosition != -1) return false; 
                    unitPosition = i;
                }
                else if (Math.Abs(value) > EPSILON)
                {
                    return false; 
                }
            }

            return unitPosition != -1; 
        }

        private double[,] CreateIdentityMatrix(int size)
        {
            double[,] identity = new double[size, size];
            for (int i = 0; i < size; i++)
            {
                identity[i, i] = 1.0;
            }
            return identity;
        }

        private double[,] CloneMatrix(double[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[,] clone = new double[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    clone[i, j] = matrix[i, j];
                }
            }

            return clone;
        }

        private double[,] MultiplyMatrices(double[,] A, double[,] B)
        {
            int rowsA = A.GetLength(0);
            int colsA = A.GetLength(1);
            int colsB = B.GetLength(1);

            double[,] result = new double[rowsA, colsB];

            for (int i = 0; i < rowsA; i++)
            {
                for (int j = 0; j < colsB; j++)
                {
                    for (int k = 0; k < colsA; k++)
                    {
                        result[i, j] += A[i, k] * B[k, j];
                    }
                }
            }

            return result;
        }

        private double[] MultiplyMatrixVector(double[,] matrix, double[] vector)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[] result = new double[rows];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i] += matrix[i, j] * vector[j];
                }
            }

            return result;
        }

        private double[] MultiplyTransposedMatrixVector(double[,] matrix, double[] vector)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[] result = new double[cols];

            for (int j = 0; j < cols; j++)
            {
                for (int i = 0; i < rows; i++)
                {
                    result[j] += matrix[i, j] * vector[i];
                }
            }

            return result;
        }

        private double DotProduct(double[] a, double[] b)
        {
            double sum = 0;
            for (int i = 0; i < a.Length; i++)
            {
                sum += a[i] * b[i];
            }
            return sum;
        }
    }


    internal class RevisedSimplexSetup
    {
        public List<string> BasicVariableNames { get; set; }
        public double[,] BasisInverse { get; set; }
        public double[] RHS { get; set; }
        public List<Dictionary<string, double>> OriginalTable { get; set; }
    }

    public class RevisedSimplexIteration
    {
        public int IterationNumber { get; set; }
        public List<string> BasicVariableNames { get; set; }
        public double[] BasicSolution { get; set; }
        public double[,] BasisInverse { get; set; }
        public double[] ShadowPrices { get; set; }
        public Dictionary<string, double> ReducedCosts { get; set; }
        public double ObjectiveValue { get; set; }
        public bool IsOptimal { get; set; }
        public bool IsUnbounded { get; set; }
        public string EnteringVariable { get; set; }
        public string LeavingVariable { get; set; }
        public double PivotRatio { get; set; }
    }

    internal class PriceOutResult
    {
        public double[] ShadowPrices { get; set; }
        public Dictionary<string, double> ReducedCosts { get; set; }
        public double ObjectiveValue { get; set; }
        public bool IsOptimal { get; set; }
    }

    internal class RatioTestResult
    {
        public int LeavingIndex { get; set; }
        public double MinRatio { get; set; }
    }
}