using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SolverFoundation.Services;
using Microsoft.SolverFoundation.Common;
/*==============================================================================
// Copyright © Microsoft Corporation.  All Rights Reserved.
// This code released under the terms of the 
// Microsoft Public License (MS-PL, http://opensource.org/licenses/ms-pl.html.)
==============================================================================*/

// -------------------------------------------------------------------------------------------------------------
// Problem Type: Constraint programming, mixed integer programming.
//
// Problem Description:
//
// This sample illustrates an implementation of a column generation algorithm using Solver Foundation Services.
// This sample is based on a blog post by from Erwin Kalvelagen's blog at http://yetanothermathprogrammingconsultant.blogspot.com/.
//locate blog from: https://yetanothermathprogrammingconsultant.blogspot.com/search?q=column+generation
// -------------------------------------------------------------------------------------------------------------
namespace Samples.Solvers.Services
{
    public class ColumnGeneration
    {
        //Original Roll width, we need to find out how many of these are needed to meet the demand
        private static int _rollWidth = 100;

        //Objective limit for slave model
        private const double _objLimit = -0.001;

        //Current number patterns 
        private int _patternCount = 0;

        private SolverContext _context = null;

        //Composition of a particular roll in a particular pattern
        private List<PatternItemSize> _patternRolls;

        //Demand for a particular roll
        private List<SizeDemand> _demands;

        //Output variable to store the number of times a particular pattern used
        private List<CuttingPattern> _patterns;

        //Duals of constraints
        private List<KeyValuePair<int, Rational>> _shadowPrices;

        private Set _setRoll;

        private Model _masterModelContinuous;
        private Model _slaveModel;


        private static string _demandConstraintName = "DemandConstraint";

        /// <summary>
        /// Initialize demand for different rolls
        /// and some other class fields
        /// </summary>
        /// <remarks>Find the initial patterns by producing only one size roll</remarks>
        public void Initialize()
        {
            _patternRolls = new List<PatternItemSize>();
            _demands = new List<SizeDemand>();
            _patterns = new List<CuttingPattern>();

            //demand for rolls: use UNIQUE widths!
            _demands.Add(new SizeDemand(12, 211));
            _demands.Add(new SizeDemand(27, 345));
            _demands.Add(new SizeDemand(31, 395));
            _demands.Add(new SizeDemand(36, 610));
            _demands.Add(new SizeDemand(45, 97));
            _demands.Add(new SizeDemand(68, 121));

            //Initial number of patterns and its composition
            //Create one pattern per demanded roll
            //(each cutting pattern will initially contain a single width roll,
            //and cut as many items as they fit in the roll's width)
            //1. Crete one pattern per roll
            _patternCount = _demands.Count();
            for (int i = 0; i < _patternCount; i++)
            {
                _patterns.Add(new CuttingPattern() { PatternID = i, Count = 0 });
            }
            //2. add each roll on one pattern
            _demands.ForEach(demand => AddRollToPatterns(demand));

            _context = SolverContext.GetContext();
            _setRoll = new Set(Domain.IntegerNonnegative, "Roll");
            _shadowPrices = new List<KeyValuePair<int, Rational>>();
            Console.WriteLine("Starting with {0} patterns (1 pattern per size, fit as many items as possible, while <={1} [rollWidth]).", _patternCount, _rollWidth);
        }

        /// <summary>
        /// Takes the decision domain as a parameter and builds LP/MIP model
        /// depending on the domain
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        private Model BuildMasterModel(Domain domain)
        {
            _context.ClearModel();
            Model masterModel = _context.CreateModel();

            //Creating the sets
            Set setPattern = new Set(domain: Domain.IntegerNonnegative, name: "Pattern");

            //parameter for demanded sizes
            Parameter paramDemands = new Parameter(domain: Domain.IntegerNonnegative, name: "ParamDemands", indexSets: _setRoll);
            paramDemands.SetBinding(binding: _demands, valueField: "Demand", indexFields: "Width");

            //parameter for number that each pattern must be applied to get all sizes and their demanded quantityv
            Parameter paramPatternRolls = new Parameter(domain: Domain.IntegerNonnegative, name: "paramPatternRolls", indexSets: new Set[] { setPattern, _setRoll });
            paramPatternRolls.SetBinding(binding: _patternRolls, valueField: "Count", indexFields: new string[] { "PatternID", "Width" });

            //Add both parameters to model
            masterModel.AddParameters(paramDemands, paramPatternRolls);

            //Decision: Created, bind data and add to the model
            //This is where the solver will place values (how many times to cut each pattern)
            Decision decisionPatternCounts = new Decision(domain: domain, name: "PatternCounts", indexSets: setPattern);
            decisionPatternCounts.SetBinding(binding: _patterns, valueField: "Count", indexFields: "PatternID");
            masterModel.AddDecision(decision: decisionPatternCounts);

            //Adding the demand constraint 
            masterModel.AddConstraint(_demandConstraintName, Model.ForEach
                                                            (
                                                              _setRoll, roll => //from _setRoll, run for each roll
                                                                  //sum of (pattern items for given roll) * (pattern count) >= (demandf for size)
                                                                  Model.Sum
                                                                  (
                                                                    Model.ForEach
                                                                      (
                                                                        setPattern, pattern => //from setPattern, run for each pattern
                                                                           decisionPatternCounts[pattern] * paramPatternRolls[pattern, roll]
                                                                       )
                                                                  )
                                                              >= paramDemands[roll]
                                                            ));

            //Minize the total cuts
            masterModel.AddGoal("TotalRolls", GoalKind.Minimize, Model.Sum(Model.ForEach(setPattern, pattern => decisionPatternCounts[pattern])));
            return masterModel;
        }

        /// <summary>
        /// Set the master model to be the current model in the context.
        /// Create the model on the first call
        /// </summary>
        private void SetCurrentToMaster(Domain domain)
        {
            if (domain == Domain.RealNonnegative && _masterModelContinuous != null)
            {
                _context.ClearModel();
                _context.CurrentModel = _masterModelContinuous;
            }
            else
            {
                Model masterModel = BuildMasterModel(domain);
                if (domain == Domain.RealNonnegative)
                    _masterModelContinuous = masterModel;
            }
        }

        /// <summary>
        /// Initially formulate the model as an LP
        /// Solve the LP model. If the model is solved to an optimal result,
        /// find the duals of the constraints. FindNewPattern() method uses duals and 
        /// tries to find a new pattern. If a new pattern is found, it is added to the PatternRolls data.
        /// This model keeps on growing if there is a new pattern found in the FindNewPattern() method 
        /// and resolved. This process continues until there is no new pattern
        /// </summary>
        /// <returns>Returns true if an optimal solution is found, otherwise false</returns>
        public bool SolveMasterModel()
        {
            SetCurrentToMaster(Domain.RealNonnegative);
            Model masterModel = _context.CurrentModel;
            SimplexDirective simplex = new SimplexDirective();
            simplex.GetSensitivity = true; //setting this to true, generates shadow prices, needed to calc new pattern
            Solution sol = _context.Solve(simplex);

            //Check if the solution is optimal
            if (sol.Quality == SolverQuality.Optimal)
            {
                Report report = sol.GetReport(ReportVerbosity.All);

                //LinearReport has the Sensitivity report
                LinearReport lpReport = report as LinearReport;

                //Copying the duals from lpReport.GetShadowPrices
                //This is needed to match indexes (Roll Widths) in the SetRoll. For example: GetShadowPrices returns the keys as DemandConstraint(12), 
                //but keys should just be 12 to match the indexes
                //Currently a limitation with our API
                //https://ideas.repec.org/p/fth/nesowa/96-18.html
                _shadowPrices.Clear();
                foreach (KeyValuePair<string, Rational> pair in lpReport.GetShadowPrices(masterModel.Constraints.First()))
                {
                    _shadowPrices.Add(new KeyValuePair<int, Rational>(GetIndexFromName(pair.Key), pair.Value));
                }
                //we have defined a single decision with multiple values (how many cuts per pattern to reach demanded rolls)
                var goal = masterModel.Goals.First().ToDouble();
                PrintSolution(masterModel.Decisions.First().GetValues(), goal, false);
                //Console.WriteLine();
                //Console.WriteLine("Now totalRolls required to meet the demand:{0}", masterModel.Goals.First().ToDouble());
                //Console.WriteLine();
                return true;
            }
            return false;
        }

        /// <summary>
        /// This is final model to solve. You can see it is the same model except this is a MIP
        /// </summary>
        public void SolveFinalMIPModel()
        {
            SetCurrentToMaster(Domain.IntegerNonnegative);
            Model masterMIPModel = _context.CurrentModel;
            SimplexDirective simplex = new SimplexDirective();
            Solution sol = _context.Solve(simplex);

            if (sol.Quality == SolverQuality.Optimal)
            {
                Console.WriteLine("\n**** Final Solution ****");
                var goal = masterMIPModel.Goals.First().ToDouble();
                PrintSolution(masterMIPModel.Decisions.First().GetValues(), goal, true);
            }

            //TODO: PrintSolution(masterMIPModel.Decisions.First().GetValues()) & Report report = sol.GetReport(ReportVerbosity.All) SHOW DIFF VALUES!!!
            //2nd appears to be more accurate

            Report report = sol.GetReport(ReportVerbosity.All);
            Console.WriteLine(report);
        }

        /// <summary>
        /// Find a new pattern (use slave model to calc size contents of new pattern)
        /// </summary>
        /// <returns>True if found a new pattern, otherwise false</returns>
        public bool FindNewPattern()
        {
            //try to find a new pattern which will eventually result in less total rolls
            SetCurrentToSlaveModel();

            Solution sol = _context.Solve();

            Goal objective = sol.Goals.First();
            Decision newPattern = sol.Decisions.First();
            if (sol.Quality == SolverQuality.Optimal && objective.ToDouble() < _objLimit)
            {
                Console.WriteLine("\nFound a new {0}th pattern (based on prev shadow prices)", _patternCount + 1);
                //Console.WriteLine("  ({0})",
                //    string.Join(", ",_shadowPrices.Select(pair => string.Format("{0}=>{1}",pair.Key, pair.Value)))
                //);
                foreach (Object[] patternRoll in newPattern.GetValues())
                {
                    _patternRolls.Add(new PatternItemSize(id: _patternCount, width:  Convert.ToInt32(patternRoll[1]), count: Convert.ToInt32(patternRoll[0])));
                }
                _patterns.Add(new CuttingPattern() { PatternID = _patternCount, Count = 0 });
                _patternCount++;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Set the slave model to be the current model in the context.
        /// Create the model on the first call
        /// Slave model is used to calculate a new "best possible" pattern (how many items per size to include)
        /// </summary>
        private void SetCurrentToSlaveModel()
        {
            //2. slave model has been created, reuse it
            if (_slaveModel != null)
            {
                _context.ClearModel();
                _context.CurrentModel = _slaveModel;
            }
            //1. slave model not created, yet, create it now
            else
            {
                _context.ClearModel();
                Model slaveModel = _context.CreateModel();

                //One param per size, key by size, value by shadowPrice
                Parameter paramShadowPrices = new Parameter(domain: Domain.RealNonnegative, name: "paramShadowPrices", indexSets: _setRoll);
                paramShadowPrices.SetBinding(binding: _shadowPrices, valueField: "Value", indexFields: "Key");
                slaveModel.AddParameter(paramShadowPrices);

                //what to calculate: quantity per size for new pattern (decision)
                //Each pattern defines how many times each size will be included in it(but we must stay <=100 [rollWidth])
                Decision newPattern = new Decision(Domain.IntegerNonnegative, "newPattern", _setRoll);
                slaveModel.AddDecision(newPattern);

                //constraint: all included sizes cannot exceed width of roll
                //eg 2*12cm + 0*60cm + 3*20cm = 84cm must be <= 100cm (solver will try 2* 0* 3* to verify contraint is ok)
                slaveModel.AddConstraint("c1", Model.Sum(Model.ForEach(_setRoll, roll => roll * newPattern[roll])) <= _rollWidth);

                //Shadow Price = Change in the Objective Function Value (goal) per unit increase in the right hand side of a constraint
                //we have a single constraint, but actually it is like having one contraint per size:
                //--total production of a size(sum of (times each pattern will be cut * items per size)) >= demand for that size
                //example: for size x1, the demand is 211. The solution must cut that many rolls, using any pattern(s), to satisfy prod >= 211
                //if SP for x1 is 1/8  === if we  change Demand for x1 size by +1 (make 211->212), the total rolls will be increased by 1/8
                //** we cannot have shadow prices with integer values, thats why the prev run was with positive reals **
                //we want the new pattern to include that size(s) mix that will best complement the previous patterns
                //by coming closer to filling up a full roll (1=full roll, minus =(items * effect on roll)
                //note that 1-sum(effect) cannot be negative since it would break the conditions fot roll width
                slaveModel.AddGoal("g1", GoalKind.Minimize, 1 - Model.Sum(Model.ForEach(_setRoll, roll => newPattern[roll] * paramShadowPrices[roll])));
                
                //maximize does not work... ?
                //slaveModel.AddGoal("g1", GoalKind.Maximize, Model.Sum(Model.ForEach(_setRoll, roll => newPattern[roll] * paramShadowPrices[roll])));

                //store slave mode, to reuse it on next iteration
                _slaveModel = slaveModel;
            }

        }

        #region Helper functions

        /// <summary>
        /// Add roll compistion in a pattern
        /// </summary>
        /// <param name="demand"></param>
        private void AddRollToPatterns(SizeDemand demand)
        {
            var idxRoll = _demands.IndexOf(demand); //index of roll on the list

            //each roll is intially added to the pattern with the same index (1-1 allocation)
            for (int i = 0; i < _patternCount; i++)
            {
                //roll index matches pattern index -> add role in pattern
                //for quantity, use as many items as possible
                if (i == idxRoll)
                {
                    
                    _patternRolls.Add(new PatternItemSize(i, demand.Width, _rollWidth / demand.Width));
                }
                //roll does not match pattern's index. Use quantity = 0 (do not cut any rolls while cutting using this pattern)
                else
                {
                    _patternRolls.Add(new PatternItemSize(i, demand.Width, 0));
                }
            }
        }

        /// <summary>
        /// Prints the patterns to console
        /// </summary>
        public void PrintPatterns()
        {
            for (int i = 0; i < (_patterns.Count() + 1) * 7; i++)
            {
                Console.Write("-");
            }
            Console.WriteLine();
            Console.Write("          ");
            string strPattern = string.Empty;
            for (int i = 0; i < _patterns.Count(); i++)
            {
                strPattern = string.Format("P({0})   ", i + 1);
                strPattern = strPattern.PadRight(7);
                Console.Write(strPattern);
            }
            Console.WriteLine();
            for (int i = 0; i < (_patterns.Count() + 1) * 7; i++)
            {
                Console.Write("-");
            }
            Console.WriteLine();
            int count = 0;
            int rollCount = 0;
            string strRoll = string.Empty;
            IEnumerable<PatternItemSize> patternRollsOrdered = _patternRolls.OrderBy(roll => roll.Width).ThenBy(roll => roll.PatternID);
            foreach (PatternItemSize roll in patternRollsOrdered)
            {
                if (count % _patterns.Count() == 0)
                {
                    strRoll = string.Format("Roll({0})", _demands[rollCount].Width);
                    strRoll = strRoll.PadRight(10);
                    Console.Write(strRoll);
                    rollCount++;
                }
                Console.Write(roll.Count.ToString().PadRight(7));
                count++;
                if (count % _patterns.Count() == 0)
                    Console.WriteLine();
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Prints the patterns to console
        /// </summary>
        public void PrintPatternsX(List<double> patternInsstances)
        {
            int charsPerPattern = 5;
            int headerChars = 11 + 8;
            Console.WriteLine("{0}{2}{3}{1}",
                "┌",
                "┐",
                new string('─', headerChars),
                new string('─', _patterns.Count() * charsPerPattern)
                );
            Console.Write("{0}", new string(' ', headerChars));
            string strPattern = string.Empty;
            for (int i = 0; i < _patterns.Count(); i++)
            {
                strPattern = string.Format(" P{0,-3}", i + 1);
                //strPattern = strPattern.PadRight(7);
                Console.Write(strPattern);
            }
            Console.WriteLine();
            for (int i = 0; i < headerChars + _patterns.Count() * charsPerPattern; i++)
            {
                Console.Write("-");
            }
            Console.WriteLine();
            int count = 0;
            int rollCount = 0;
            string strRoll = string.Empty;
            IEnumerable<PatternItemSize> patternRollsOrdered = _patternRolls.OrderBy(roll => roll.Width).ThenBy(roll => roll.PatternID);
            foreach (PatternItemSize roll in patternRollsOrdered)
            {
                if (count % _patterns.Count() == 0)
                {
                    strRoll = string.Format(" {0,3}cm {1,4} {2,-7}", _demands[rollCount].Width, _demands[rollCount].Demand, _shadowPrices[rollCount].Value);
                    strRoll = strRoll.PadRight(10);
                    Console.Write(strRoll);
                    rollCount++;
                }
                if (roll.Count == 0)
                    Console.Write(" ∙   ");
                else
                    Console.Write(" {0,-3} ",roll.Count);
                count++;
                if (count % _patterns.Count() == 0)
                    Console.WriteLine();
            }
            Console.WriteLine();
        }

        public void PrintSolution(IEnumerable<Object[]> values, double goal, bool mips)
        {
            int charsPerPattern = 5;
            int headerChars = 11+6;

            List<double> patternInstances = new List<double>();
            foreach (var value in values)
                patternInstances.Add(mips ? Convert.ToInt32(value[0]) : Math.Ceiling((double)value[0]));
            //patternInstances.Add((int)Math.Ceiling((double)value[0]));
            PrintPatternsX(patternInstances);

            for (int i = 0; i < headerChars + _patterns.Count() * charsPerPattern; i++)
            {
                Console.Write("-");
            }
            Console.WriteLine();
            Console.Write("           Counts  ");
            string strValue = string.Empty;
            double val;
            foreach (Object[] value in values)
            {
                val = mips ? Convert.ToInt32(value[0]) : Math.Ceiling((double)value[0]);
                //strValue = Math.Ceiling((double)value[0]).ToString();
                //strValue = strValue.PadRight(7);
                //Console.Write(strValue);
                if (val == 0)
                    Console.Write(" ∙   ");
                else
                    Console.Write(" {0,-4}", val)
;            }
            Console.Write(" Σint={0} Σdbl={1} Goal={2}", patternInstances.Sum(), values.Select(x=>(double)x[0]).Sum(), goal);
            Console.WriteLine();
            for (int i = 0; i < headerChars + _patterns.Count() * charsPerPattern; i++)
            {
                Console.Write("-");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Get the Index from the constraint name
        /// </summary>
        /// <param name="strConstraintName"></param>
        /// <returns></returns>
        private int GetIndexFromName(string strConstraintName)
        {
            int startIndex = _demandConstraintName.Length + 1;
            int lastIndex = strConstraintName.LastIndexOf(')');
            int length = lastIndex - startIndex;
            return Convert.ToInt32(strConstraintName.Substring(startIndex, length));
        }

        #endregion
    }

    //class Program
    //{
    //    static void Main(string[] args)
    //    {
    //        ColumnGeneration generation = new ColumnGeneration();
    //        bool bContinue;
    //        generation.Initialize();
    //        do
    //        {
    //            bContinue = generation.SolveMasterModel();
    //            //If found an optimal solution to relaxed model
    //            if (bContinue)
    //                bContinue = generation.FindNewPattern();
    //        } while (bContinue);

    //        generation.SolveFinalMIPModel();
    //    }
    //}

    #region Helper classes 
    public class SizeDemand
    {
        public int Width
        {
            get;
            set;
        }

        public int Demand
        {
            get;
            set;
        }

        public SizeDemand(int width, int demand)
        {
            Width = width;
            Demand = demand;
        }

        ///// <summary>
        ///// Cannot use a string Index Key, since param is Int and expects a unique integer as key
        ///// </summary>
        //public string Key
        //{
        //    get
        //    {
        //        return string.Format("roll{0}_{1}", Width, Demand);
        //    }
        //}

        public override string ToString()
        {
            return string.Format("Width: {0} | Demand: {1}", Width, Demand);
        }
    }

    /// <summary>
    /// Each instance represents how many times a given size is included in a pattern(defined by PatternID)
    /// A pattern includes multiple instances, one per size
    /// </summary>
    public class PatternItemSize
    {
        public int PatternID
        {
            get;
            set;
        }

        public int Width
        {
            get;
            set;
        }

        public int Count
        {
            get;
            set;
        }

        public PatternItemSize(int id, int width, int count)
        {
            PatternID = id;
            Width = width;
            Count = count;
        }

        public override string ToString()
        {
            return string.Format("Id: {1} | Width: {0} | Count: {2}", Width, PatternID, Count);
        }
    }

    /// <summary>
    /// Each instance is used to denote how many times a pattern (defined by PatternID) will be cut to meet required demand
    /// </summary>
    public class CuttingPattern
    {
        public int PatternID
        {
            get;
            set;
        }

        public int Count
        {
            get;
            set;
        }

        public override string ToString()
        {
            return string.Format("Id: {0} | Count: {1}", PatternID, Count);
        }
    }

    #endregion
}
