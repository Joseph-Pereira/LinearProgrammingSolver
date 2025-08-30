using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPSolver.Algorithms
{
   public class LinearStorage
    {
        public bool IsMaximization { get; set; }
        public double[] ObjectiveCoefficients { get; set; }
        public List<Constraint> Constraints { get; set; }
        public string[]  SignRestriction { get; set; }
        
        public LinearStorage()
        {
            Constraints = new List<Constraint>();

        }

    }

    public class Constraint
    {
        public double[] Coefficients { get; set; }
        public string Sign {  get; set; }
        public double RHS { get; set; }

    }

  
    public class CanonicalConstraint
    {
        public double[] Coefiecients { get; set; }
        public string AdditionalValue { get; set; }
        public double RHS { get; set; }
    }
}
