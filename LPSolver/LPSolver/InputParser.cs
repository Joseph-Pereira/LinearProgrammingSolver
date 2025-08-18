using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LPSolver
{
    internal class InputParser
    {
        public static LinearStorage Parse(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);
            LinearStorage storage = new LinearStorage();

            
            string[] firstLine = lines[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            storage.IsMaximization = firstLine[0].ToLower() == "max";

            List<double> objCoeffs = new List<double>();
            for (int i = 1; i < firstLine.Length; i++)
            {
                objCoeffs.Add(double.Parse(firstLine[i]));
            }
            storage.ObjectiveCoefficients = objCoeffs.ToArray();

            
            for (int i = 1; i < lines.Length - 1; i++)
            {
                string[] parts = lines[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int numCoeffs = storage.ObjectiveCoefficients.Length;

                double[] coeffs = new double[numCoeffs];
                for (int j = 0; j < numCoeffs; j++)
                {
                    coeffs[j] = double.Parse(parts[j]);
                }

                string relation = parts[numCoeffs];
                double rhs = double.Parse(parts[numCoeffs + 1]);

                storage.Constraints.Add(new Constraint
                {
                    Coefficients = coeffs,
                    Sign = relation,
                    RHS = rhs
                });
            }

            
            storage.SignRestriction = lines[lines.Length - 1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return storage;
        }
    }
}
