using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LPSolver;

namespace LPSolver.Algorithms
{

    internal class PrimalSimplex
    {

        public List<List<Dictionary<string, double>>> Iterations { get; private set; } = new List<List<Dictionary<string, double>>>();

        public void Solve(LinearStorage storage)
        {
            var canonical = new CanonicalForm(storage);
            Iterations.Add(CloneTable(canonical.Table));

            while (true)
            {
                string pivotCol = FindPivotColumn(canonical.Table[0]);
                if (pivotCol == null) break;

                int pivotRowIndex = FindPivotRow(canonical.Table, pivotCol);
                if (pivotRowIndex == -1) throw new InvalidOperationException("Unbounded");

                PivotTable(canonical.Table, pivotRowIndex, pivotCol);

                Iterations.Add(CloneTable(canonical.Table));
            }
        }

        private List<Dictionary<string, double>> CloneTable(List<Dictionary<string, double>> table)
        {
            return table.Select(r => new Dictionary<string, double>(r)).ToList();
        }

        private string FindPivotColumn(Dictionary<string, double> objRow)
        {
            string pivotCol = null;
            double mostNegative = 0;

            foreach (var kv in objRow)
            {
                if (kv.Key == "RHS") continue;

                
                if (kv.Value < mostNegative)
                {
                    mostNegative = kv.Value;
                    pivotCol = kv.Key;
                }
            }

            return pivotCol; 
        }

        private int FindPivotRow(List<Dictionary<string, double>> table, string pivotCol)
        {
            double minTheta = double.PositiveInfinity;
            int leavingRow = -1;
            for (int i = 1; i < table.Count; i++)
            {
                double colVal = table[i][pivotCol];
                double rhs = table[i]["RHS"];
                double theta = (colVal > 0) ? rhs / colVal : double.PositiveInfinity;

                if (theta == 0)
                {
                    bool hasPositiveNeighbor = false;
                    if (i > 1 && table[i - 1][pivotCol] > 0) hasPositiveNeighbor = true;
                    if (i < table.Count - 1 && table[i + 1][pivotCol] > 0) hasPositiveNeighbor = true;
                    if (!hasPositiveNeighbor) continue;
                }

                if (theta < minTheta)
                {
                    minTheta = theta;
                    leavingRow = i;
                }
            }
            return leavingRow;
        }

        private void PivotTable(List<Dictionary<string, double>> table, int pivotRowIndex, string pivotCol)
        {
            double pivotElement = table[pivotRowIndex][pivotCol];
            var originalPivotRow = new Dictionary<string, double>(table[pivotRowIndex]);


            for (int i = 0; i < table.Count; i++)
            {
                if (i == pivotRowIndex) continue;

                double pivotColumnFactor = table[i][pivotCol];


                foreach (var key in table[i].Keys.ToList())
                {
                    double oldValue = table[i][key];
                    double originalPivotRowValue = originalPivotRow[key];


                    table[i][key] = oldValue - (pivotColumnFactor * originalPivotRowValue / pivotElement);
                }
            }
            foreach (var key in table[pivotRowIndex].Keys.ToList())
            {
                table[pivotRowIndex][key] = originalPivotRow[key] / pivotElement;
            }
        }

        public void SolveFromTable(List<Dictionary<string, double>> table)
        {
            var canonical = new CanonicalFormStub(table);

            
            Iterations.Add(CloneTable(canonical.Table));

            while (true)
            {
                string pivotCol = FindPivotColumn(canonical.Table[0]);
                if (pivotCol == null) break;

                int pivotRowIndex = FindPivotRow(canonical.Table, pivotCol);
                if (pivotRowIndex == -1) throw new InvalidOperationException("Unbounded");

                PivotTable(canonical.Table, pivotRowIndex, pivotCol);
                Iterations.Add(CloneTable(canonical.Table));
            }
        }

        

        internal class CanonicalFormStub
        {
            public List<Dictionary<string, double>> Table { get; }
            public CanonicalFormStub(List<Dictionary<string, double>> existingTable)
            {
                Table = existingTable;
            }
        }

    }

    public class CanonicalForm
    {

        public List<Dictionary<string, double>> Table { get; private set; }

        public CanonicalForm(LinearStorage storage)
        {

            Table = new List<Dictionary<string, double>>();
            int Varnum = storage.ObjectiveCoefficients.Length;
            int numConstraints = storage.Constraints.Count;
            var objRow = new Dictionary<string, double>();
            for (int i = 0; i < Varnum; i++)
            {
                string VarName = $"x{i + 1}";
                double coef = -storage.ObjectiveCoefficients[i];
                objRow[VarName] = coef;
            }


            for (int j = 0; j < numConstraints; j++)
            {
                var constraint = storage.Constraints[j];
                if (constraint.Sign == "<=")
                {
                    objRow[$"s{j + 1}"] = 0;
                }
                else if (constraint.Sign == ">=")
                {
                    objRow[$"e{j + 1}"] = 0;
                }

            }
            objRow["RHS"] = 0;

            Table.Add(objRow);

            for (int i = 0; i < numConstraints; i++)
            {
                var Const = storage.Constraints[i];
                var conRows = new Dictionary<string, double>();

                
                for (int j = 0; j < Varnum; j++)
                {
                    if (Const.Sign == ">=")
                        conRows[$"x{j + 1}"] = -Const.Coefficients[j];  
                    else
                        conRows[$"x{j + 1}"] = Const.Coefficients[j];   
                }

               
                for (int j = 0; j < numConstraints; j++)
                {
                    if (storage.Constraints[j].Sign == "<=")
                        conRows[$"s{j + 1}"] = 0;
                    else if (storage.Constraints[j].Sign == ">=")
                        conRows[$"e{j + 1}"] = 0;
                }

               
                if (Const.Sign == "<=")
                {
                    conRows[$"s{i + 1}"] = 1;
                    conRows["RHS"] = Const.RHS;
                }
                else if (Const.Sign == ">=")
                {
                    conRows[$"e{i + 1}"] = 1;  
                    conRows["RHS"] = -Const.RHS;  
                }

                Table.Add(conRows);
            }

        }



    }

    internal class DualSimplex
    {
        public List<List<Dictionary<string, double>>> Iterations { get; private set; } = new List<List<Dictionary<string, double>>>();

        public void Solve(LinearStorage storage)
        {
            var canonical = new CanonicalForm(storage);
            Iterations.Add(CloneTable(canonical.Table));

            while (true)
            {
                
                int pivotRowIndex = FindPivotRow(canonical.Table);
                if (pivotRowIndex == -1) break;

             
                string pivotCol = FindPivotColumn(canonical.Table, pivotRowIndex);
                if (pivotCol == null) throw new InvalidOperationException("Infeasible (dual simplex cannot continue)");

               
                PivotTable(canonical.Table, pivotRowIndex, pivotCol);

            
                Iterations.Add(CloneTable(canonical.Table));
            }

          
            if (canonical.Table[0].Any(kv => kv.Key != "RHS" && kv.Value < 0))
            {
                var primal = new PrimalSimplex();
                primal.SolveFromTable(canonical.Table);
                foreach (var it in primal.Iterations.Skip(1))
                    Iterations.Add(CloneTable(it));
            }
        }

        private List<Dictionary<string, double>> CloneTable(List<Dictionary<string, double>> table)
        {
            return table.Select(r => new Dictionary<string, double>(r)).ToList();
        }

        private int FindPivotRow(List<Dictionary<string, double>> table)
        {
            int pivotRow = -1;
            double mostNeg = 0;
            for (int i = 1; i < table.Count; i++)
            {
                double rhs = table[i]["RHS"];
                if (rhs < mostNeg)
                {
                    mostNeg = rhs;
                    pivotRow = i;
                }
            }
            return pivotRow;
        }

        private string FindPivotColumn(List<Dictionary<string, double>> table, int pivotRowIndex)
        {
            var row = table[pivotRowIndex];
            string pivotCol = null;
            double minRatio = double.PositiveInfinity;

            foreach (var kv in row)
            {
                if (kv.Key == "RHS") continue;
                double a_ij = kv.Value;
                if (a_ij >= 0) continue; 

                double objCoef = table[0][kv.Key];
                double ratio = objCoef / a_ij; 
                if (ratio < minRatio)
                {
                    minRatio = ratio;
                    pivotCol = kv.Key;
                }
            }
            return pivotCol;
        }

        private void PivotTable(List<Dictionary<string, double>> table, int pivotRowIndex, string pivotCol)
        {
            double pivotElement = table[pivotRowIndex][pivotCol];
            var originalPivotRow = new Dictionary<string, double>(table[pivotRowIndex]);

            for (int i = 0; i < table.Count; i++)
            {
                if (i == pivotRowIndex) continue;
                double factor = table[i][pivotCol];
                foreach (var key in table[i].Keys.ToList())
                {
                    table[i][key] -= factor * originalPivotRow[key] / pivotElement;
                }
            }

            foreach (var key in table[pivotRowIndex].Keys.ToList())
            {
                table[pivotRowIndex][key] = originalPivotRow[key] / pivotElement;
            }
        }
    }










}
