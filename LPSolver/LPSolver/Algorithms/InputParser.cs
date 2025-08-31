using LPSolver.Algorithms;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LPSolver.Algorithms
{
    internal class InputParser
    {
        public static LinearStorage Parse(string filePath)
        {
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                LinearStorage storage = new LinearStorage();

                if (lines.Length < 3)
                {
                    throw new InvalidOperationException("Input file must have at least 3 lines: objective, constraints, and sign restrictions");
                }

                // Parse first line (objective function)
                string[] firstLine = lines[0].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (firstLine.Length < 2)
                {
                    throw new InvalidOperationException("First line must contain 'max'/'min' and at least one coefficient");
                }

                storage.IsMaximization = firstLine[0].ToLower() == "max";

                List<double> objCoeffs = new List<double>();
                for (int i = 1; i < firstLine.Length; i++)
                {
                    if (double.TryParse(firstLine[i], out double coeff))
                    {
                        objCoeffs.Add(coeff);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Invalid objective coefficient: {firstLine[i]}");
                    }
                }
                storage.ObjectiveCoefficients = objCoeffs.ToArray();

                Console.WriteLine($"Parsed {objCoeffs.Count} objective coefficients");

                // Parse constraint lines (all lines except first and last)
                for (int i = 1; i < lines.Length - 1; i++)
                {
                    string[] parts = lines[i].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int numCoeffs = storage.ObjectiveCoefficients.Length;

                    if (parts.Length < numCoeffs + 2)
                    {
                        throw new InvalidOperationException($"Constraint line {i} has insufficient elements. Expected at least {numCoeffs + 2}, got {parts.Length}");
                    }

                    double[] coeffs = new double[numCoeffs];
                    for (int j = 0; j < numCoeffs; j++)
                    {
                        if (double.TryParse(parts[j], out double coeff))
                        {
                            coeffs[j] = coeff;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Invalid constraint coefficient at line {i}, position {j}: {parts[j]}");
                        }
                    }

                    string relation = parts[numCoeffs];
                    if (!new[] { "<=", ">=", "=" }.Contains(relation))
                    {
                        throw new InvalidOperationException($"Invalid relation operator: {relation}. Must be <=, >=, or =");
                    }

                    if (!double.TryParse(parts[numCoeffs + 1], out double rhs))
                    {
                        throw new InvalidOperationException($"Invalid RHS value: {parts[numCoeffs + 1]}");
                    }

                    storage.Constraints.Add(new Constraint
                    {
                        Coefficients = coeffs,
                        Sign = relation,
                        RHS = rhs
                    });

                    Console.WriteLine($"Parsed constraint {storage.Constraints.Count}: {string.Join(" ", coeffs)} {relation} {rhs}");
                }

                // Parse sign restrictions (last line)
                string signLine = lines[lines.Length - 1].Trim();
                if (string.IsNullOrEmpty(signLine))
                {
                    throw new InvalidOperationException("Sign restrictions line is empty");
                }

                string[] signParts = signLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // Validate sign restrictions count
                if (signParts.Length != storage.ObjectiveCoefficients.Length)
                {
                    Console.WriteLine($"Warning: Sign restrictions count ({signParts.Length}) doesn't match variable count ({storage.ObjectiveCoefficients.Length})");

                    // Pad with '+' if too few, or truncate if too many
                    List<string> adjustedSigns = new List<string>();
                    for (int i = 0; i < storage.ObjectiveCoefficients.Length; i++)
                    {
                        if (i < signParts.Length)
                        {
                            adjustedSigns.Add(signParts[i]);
                        }
                        else
                        {
                            adjustedSigns.Add("+"); // Default to non-negative
                        }
                    }
                    signParts = adjustedSigns.ToArray();
                }

                // Validate each sign restriction
                for (int i = 0; i < signParts.Length; i++)
                {
                    string sign = signParts[i];
                    if (!new[] { "+", "-", "urs", "int", "bin" }.Contains(sign))
                    {
                        Console.WriteLine($"Warning: Invalid sign restriction '{sign}' for variable {i + 1}. Using '+' instead.");
                        signParts[i] = "+";
                    }
                }

                storage.SignRestriction = signParts;

                Console.WriteLine($"Parsed {storage.SignRestriction.Length} sign restrictions: {string.Join(" ", storage.SignRestriction)}");

                // Final validation
                Console.WriteLine($"Successfully parsed problem:");
                Console.WriteLine($"- Type: {(storage.IsMaximization ? "Maximization" : "Minimization")}");
                Console.WriteLine($"- Variables: {storage.ObjectiveCoefficients.Length}");
                Console.WriteLine($"- Constraints: {storage.Constraints.Count}");

                return storage;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error parsing input file '{filePath}': {ex.Message}", ex);
            }
        }
    }
}