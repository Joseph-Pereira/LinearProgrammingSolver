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
            double largestNeg = 0;
            foreach (var kv in objRow)
            {
                if (kv.Key == "RHS") continue;
                if (kv.Value < largestNeg)
                {
                    largestNeg = kv.Value;
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

            
            for(int j = 0; j < numConstraints; j++)
            {
                var constraint = storage.Constraints[j];
                if (constraint.Sign == "<=")
                {
                    objRow[$"s{j + 1}"] = 0;
                }else if (constraint.Sign == ">=")
                {
                    objRow[$"e{j + 1}"] = 0;
                }
                
            }
            objRow["RHS"] = 0;

            Table.Add(objRow);

            for(int i = 0;i < numConstraints; i++)
            {
               var Const = storage.Constraints[i];
               var conRows = new Dictionary<string, double>();
                
                for(int j = 0; j < Varnum; j++)
                {
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
                }
                else if (Const.Sign == ">=")
                {
                    conRows[$"e{i + 1}"] = -1; 
                }
                conRows["RHS"] = Const.RHS;
                Table.Add(conRows);
            }

        }



    }


   



}
