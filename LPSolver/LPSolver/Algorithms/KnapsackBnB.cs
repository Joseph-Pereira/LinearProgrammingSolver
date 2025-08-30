using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPSolver.Algorithms
{
    internal class KnapsackBnB
    {

        public class Variable // Class for variable
        {
            public int Index { get; set; } // 1-based index like x1, x2
            public double ZValue { get; set; } // Z values
            public double CValue { get; set; } // Constraint coefficients (there's only one constraint in knapsack)
            public double Ratio => ZValue / CValue; // Zi/Ci to get ranking

            public Variable(int index, double zValue, double cValue) //Constructor
            {
                Index = index;
                ZValue = zValue;
                CValue = cValue;
            }
        }

        public class Node
        {
            public string Id { get; set; }
            public string ParentId { get; set; }
            public int Depth { get; set; }
            public Dictionary<int, int> Fixed = new Dictionary<int, int>(); // Index -> 0 or 1
            public double CurrentValue { get; set; }
            public double RemainingCapacity { get; set; }
            public double UpperBound { get; set; }
            public int FractionalItemIndex { get; set; } = -1; // -1 if no fractional
            public string Status { get; set; } = "Unknown";
            public string SolutionString => string.Join(", ", Fixed.OrderBy(kv => kv.Key).Select(kv => $"x{kv.Key}={kv.Value}"));
        }

        public class KnapsackSolver
        {
            private double BestValue = 0;
            private Dictionary<int, int> BestSolution = new Dictionary<int, int>();
            private List<Node> AllNodes = new List<Node>();

            // Public properties to access results
            public double OptimalValue => BestValue;
            public Dictionary<int, int> OptimalSolution => new Dictionary<int, int>(BestSolution);

            public void Solve(List<Variable> items, double capacity)
            {
                // Reset state in case of reuse
                BestValue = 0;
                BestSolution.Clear();
                AllNodes.Clear();

                // Sort items by ratio descending
                items = items.OrderByDescending(i => i.Ratio).ToList();

                // Root node
                var root = new Node
                {
                    Id = "0",
                    ParentId = "",
                    Depth = 0,
                    RemainingCapacity = capacity,
                    CurrentValue = 0
                };
                AllNodes.Add(root);

                // Solve
                BranchAndBound(root, items, capacity);

                // Write CSV
                WriteCsv("knapsack_branches.csv");
            }

            private void BranchAndBound(Node node, List<Variable> allItems, double originalCapacity)
            {
                // Get remaining items (not fixed)
                var remainingItems = allItems.Where(i => !node.Fixed.ContainsKey(i.Index)).OrderByDescending(i => i.Ratio).ToList();

                // Compute current used capacity from fixed=1
                double usedCapacity = node.Fixed.Where(kv => kv.Value == 1).Sum(kv => allItems.First(i => i.Index == kv.Key).CValue);
                node.RemainingCapacity = originalCapacity - usedCapacity;
                if (node.RemainingCapacity < 0)
                {
                    node.Status = "Infeasible";
                    node.UpperBound = -1;
                    Console.WriteLine($"Node {node.Id} (Parent {node.ParentId}): {node.SolutionString} - {node.Status}, RemCap: {node.RemainingCapacity}");
                    return;
                }

                // Relaxation: Greedy fractional on remaining
                List<int> takenWhole = new List<int>();
                double relaxValue = 0;
                double remCap = node.RemainingCapacity;
                int fracIndex = -1;
                foreach (var item in remainingItems)
                {
                    if (item.CValue <= remCap)
                    {
                        // Add whole
                        relaxValue += item.ZValue;
                        remCap -= item.CValue;
                        takenWhole.Add(item.Index);
                    }
                    else if (remCap > 0)
                    {
                        // Fractional
                        double frac = remCap / item.CValue;
                        relaxValue += frac * item.ZValue;
                        fracIndex = item.Index;
                        break; // Stop after fractional
                    }
                }
                node.UpperBound = node.CurrentValue + relaxValue;
                node.FractionalItemIndex = fracIndex;

                Console.WriteLine($"Node {node.Id} (Parent {node.ParentId}): {node.SolutionString} - Bound: {node.UpperBound}, RemCap: {node.RemainingCapacity}");

                // Prune if bound <= best
                if (node.UpperBound <= BestValue)
                {
                    node.Status = "Pruned";
                    Console.WriteLine($"  -> {node.Status} (Bound {node.UpperBound} <= Best {BestValue})");
                    return;
                }

                // If no fractional (integer solution)
                if (fracIndex == -1)
                {
                    node.Status = "Candidate";
                    double candidateValue = node.UpperBound; // Includes fixed + whole taken
                    if (candidateValue > BestValue)
                    {
                        BestValue = candidateValue;
                        BestSolution = new Dictionary<int, int>(node.Fixed);
                        foreach (var tid in takenWhole) //makes all the best solution variables 1
                        {
                            BestSolution[tid] = 1;
                        }
                        foreach (var item in allItems.Where(i => !BestSolution.ContainsKey(i.Index))) //makes the rest of the variables 0
                        {
                            BestSolution[item.Index] = 0;
                        }
                        Console.WriteLine($"  -> New Best: {BestValue} (Solution: {string.Join(", ", BestSolution.OrderBy(kv => kv.Key).Select(kv => $"x{kv.Key}={kv.Value}"))})");
                    }
                    else
                    {
                        Console.WriteLine($"  -> {node.Status} but not better than current best {BestValue}");
                    }
                    return;
                }

                node.Status = "Branched";
                Console.WriteLine($"  -> {node.Status} on x{fracIndex}");

                // Branch on fractional item
                var fracItem = allItems.First(i => i.Index == fracIndex);

                // Branch 1: Set to 0
                var child0 = new Node
                {
                    ParentId = node.Id,
                    Id = node.Id + ".1",
                    Depth = node.Depth + 1,
                    Fixed = new Dictionary<int, int>(node.Fixed),
                    CurrentValue = node.CurrentValue,
                };
                child0.Fixed[fracIndex] = 0;
                AllNodes.Add(child0);
                BranchAndBound(child0, allItems, originalCapacity);

                // Branch 2: Set to 1 (if feasible)
                if (fracItem.CValue <= node.RemainingCapacity)
                {
                    var child1 = new Node
                    {
                        ParentId = node.Id,
                        Id = node.Id + ".2",
                        Depth = node.Depth + 1,
                        Fixed = new Dictionary<int, int>(node.Fixed),
                        CurrentValue = node.CurrentValue + fracItem.ZValue,
                    };
                    child1.Fixed[fracIndex] = 1;
                    AllNodes.Add(child1);
                    BranchAndBound(child1, allItems, originalCapacity);
                }
                else
                {
                    Console.WriteLine($"  -> Branch x{fracIndex}=1 infeasible (weight {fracItem.CValue} > rem {node.RemainingCapacity})");
                }
            }

            private void WriteCsv(string filePath)
            {
                var csv = new StringBuilder();
                csv.AppendLine("NodeID,ParentID,Depth,FixedVariables,CurrentValue,RemainingCapacity,UpperBound,FractionalItem,Status,BestAtThisPoint");

                foreach (var node in AllNodes)
                {
                    csv.AppendLine($"{node.Id},{node.ParentId},{node.Depth},\"{node.SolutionString}\",{node.CurrentValue},{node.RemainingCapacity},{node.UpperBound},{(node.FractionalItemIndex != -1 ? $"x{node.FractionalItemIndex}" : "")},{node.Status},{BestValue}");
                }

                //File.WriteAllText(filePath, csv.ToString());
            }
        }


        //How you would call the program
        //class Program
        //{
        //    static void Main(string[] args)
        //    {
        //        // Example from slides: Items x1 to x5, capacity 15
        //        var items = new List<Variable>
        //{
        //    //new Variable(1, 4, 12),
        //    //new Variable(2, 2, 2),
        //    //new Variable(3, 2, 1),
        //    //new Variable(4, 1, 1),
        //    //new Variable(5, 10, 4)

        //    //Testing another problem
        //    new Variable(1, 2, 11),
        //    new Variable(2, 3, 8),
        //    new Variable(3, 3, 6),
        //    new Variable(4, 5, 14),
        //    new Variable(5, 2, 10),
        //    new Variable(6, 4, 10)
        //};
        //        double capacity = 40;
        ////!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        //        var solver = new KnapsackSolver(); //Important Part
        //        solver.Solve(items, capacity); // Solving the problem here
        ////!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        //        // Output results using public properties
        //        Console.WriteLine($"Optimal Value: {solver.OptimalValue}");
        //        Console.WriteLine($"Optimal Solution: {string.Join(", ", solver.OptimalSolution.OrderBy(kv => kv.Key).Select(kv => $"x{kv.Key}={kv.Value}"))}");

        //        Console.WriteLine("Branch tree saved to knapsack_branches.csv in the program's output directory (e.g., bin/Debug/netX.0). Open in Excel for details.");
        //    }
        //}

    }
}
