using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.SolverFoundation.Common;
using Microsoft.SolverFoundation.Services;
using Microsoft.SolverFoundation.Solvers;

namespace Samples.Solvers.MIP
{
    /// <summary>
    /// Cut a roll of cloth, using knapsack (fit most valued items in container with weight limit)
    /// https://www.geeksforgeeks.org/0-1-knapsack-problem-dp-10/
    /// </summary>
    public class CuttingStock
    {
        /// <summary>
        /// Knapsack enumerator -- enumerate up to "numAnswers" combinations of "weights" such that the sum of the weights is less than the weight limit.
        /// It places the patterns of items inside the list of patterns.  The efficiency parameter ensures that we don't output any which use less than "efficiency" percent
        /// off the weightlimit.
        /// </summary>
        /// <param name="maxAnswers">maximum number of combinations to get out.  Limits runtime.  If zero return all.</param>
        /// <param name="weights">weight of each item to go into the knapsack</param>
        /// <param name="weightLimit">knapsack weight limit</param>
        /// <param name="efficiency">limit patterns to use at least this % of the weight limit (between 0.0 and 1.0) </param>
        /// <param name="patterns">output list of patterns of inclusion of the weights.</param>
        public static void SolveKnapsack(int maxAnswers, int[] weights, int weightLimit, double efficiency, out List<int[]> patterns)
        {
            // convenience value.
            int NumItems = weights.Length;
            ConstraintSystem solver = ConstraintSystem.CreateSolver();
            CspDomain dom = solver.CreateIntegerInterval(0, weightLimit);

            CspTerm knapsackSize = solver.Constant(weightLimit);

            // these represent the quantity of each item.
            CspTerm[] itemQty = solver.CreateVariableVector(dom, "Quantity", NumItems);
            CspTerm[] itemWeights = new CspTerm[NumItems];

            for (int cnt = 0; cnt < NumItems; cnt++)
            {
                itemWeights[cnt] = solver.Constant(weights[cnt]);
            }

            // contributors to the weight (weight * variable value)
            CspTerm[] contributors = new CspTerm[NumItems];
            for (int cnt = 0; cnt < NumItems; cnt++)
            {
                contributors[cnt] = itemWeights[cnt] * itemQty[cnt];
            }

            // single constraint
            CspTerm knapSackCapacity = solver.GreaterEqual(knapsackSize, solver.Sum(contributors));
            solver.AddConstraints(knapSackCapacity);

            // must be efficient
            CspTerm knapSackAtLeast = solver.LessEqual(knapsackSize * efficiency, solver.Sum(contributors));
            solver.AddConstraints(knapSackAtLeast);

            // start counter and allocate a list for the results.
            int nanswers = 0;
            patterns = new List<int[]>();

            ConstraintSolverSolution sol = solver.Solve();
            while (sol.HasFoundSolution)
            {
                int[] pattern = new int[NumItems];
                // extract this pattern from the enumeration.
                for (int cnt = 0; cnt < NumItems; cnt++)
                {
                    object val;
                    sol.TryGetValue(itemQty[cnt], out val);
                    pattern[cnt] = (int)val;
                }
                // add it to the output.
                patterns.Add(pattern);
                nanswers++;
                // stop if we reach the limit of results.
                if (maxAnswers > 0 && nanswers >= maxAnswers)
                    break;
                sol.GetNext();
            }
        }

        // exercise the solveKnapsack stand-alone.  The main purpose is to feed the cutting stock, but it can be run stand alone as well.
        public static void Knapsack()
        {
            Console.WriteLine("*** Knapsack ***");
            int NumItems = 5;
            int WeightLimit = 40;
            int maxPatterns = 300;
            double efficiency = 0.90;
            bool verbose = true; // set this to true if you want some (useful?) output to the console.

            int[] weights = new int[NumItems];
            // not really random since I'm always starting with the same seed.  Can use System.DateTime.Now.Millisecond to get marginally random.
            Random rand = new Random(12247);
            for (int cnt = 0; cnt < NumItems; cnt++)
            {
                weights[cnt] = rand.Next(1, 10);
                if (verbose)
                {
                    System.Console.WriteLine(String.Format("item[{0}].weight={1}", cnt, weights[cnt]));
                }
            }

            List<int[]> patterns;
            SolveKnapsack(maxPatterns, weights, WeightLimit, efficiency, out patterns);
            if (verbose)
            {
                System.Console.WriteLine(String.Format("Knapsack generated {0} patterns", patterns.Count));
                for (int cnt = 0; cnt < patterns.Count; cnt++)
                {
                    System.Console.Write(String.Format("Pattern{0}:\t", cnt));
                    for (int itemCnt = 0; itemCnt < NumItems; itemCnt++)
                    {
                        System.Console.Write(String.Format("{0} ", patterns[cnt][itemCnt]));
                    }
                    System.Console.WriteLine();
                }
            }

            Console.WriteLine();
        }
        
        /// <summary>
        /// Solves a sample (randomly generated?) cutting stock problem.  
        /// Given a bolt of cloth of fixed width, and demand for cut strips of the cloth, determine the min "loss" cut patterns to use and how many
        /// of them.  
        /// Loss is defined as the scrap thrown away.
        /// It is acceptable to have extra cut widths made.  They do not contribute to the cost.  (this may be unrealistic in the real world)
        /// Solver runs by 1st creating an enumeration of possible cut patterns using a CspSolver, then choosing between the patterns and selecting a qty of the patterns such that the
        /// amount of scrap is minimized and all demand is met using the SimplexSolver MIP code.
        /// 
        /// In an industrial case, there would likely be more constraints in the generation of the cut patterns.  There can be other restrictions such as "these can't be done together" 
        /// or "these MUST be done together (matching pattern or color?)".  This can easily be added to the CspSolver model.  
        /// Also, there are likely other characteristics of the cuts or the master problem which would need adaptations.
        /// 
        /// Further, the limit on the columns generated is implemented in a very arbitrary order.  It is more likely that some ordering of the 
        /// value of the columns is needed.  In most industrial occurances, the dual variables from the LP relaxation would likely be used to
        /// guide the generation of columns in an interative fasion rather than a one-time shot at the beginning.
        /// 
        /// YMMV
        /// </summary>
        public static void ShortCuttingStock()
        {
            Console.WriteLine("*** Short Cutting Stock ***");
            int NumItems = 5; // how many cut widths to generate
            int ClothWidth = 40; // width of the stock to cut the widths from
            double efficiency = 0.7; // reject cut patterns less than this % used of the clothwidth
            int maxPatterns = 100; // max # of patterns to generate
            bool verbose = true; // set this to true if you want some (useful?) output
            bool saveMpsFile = false; // set this to true if you want it to save an mps file in c:\\temp\\cutstock.mps

            int itemSizeMin = 5; // minimum size for random generation of cut
            int itemSizeMax = 10; // maximum size for random generation of cut
            int itemDemandMin = 10; // minimum random demand for each cut
            int itemDemandMax = 40; // maximum random demand for each cut

            int seed = 12447;// use System.DateTime.Now.Millisecond; instead if you want a random problem.
            if (verbose)
            {
                System.Console.WriteLine(String.Format("Random seed={0}\tmaxWidth={1}", seed, ClothWidth));
            }

            Random rand = new Random(seed);

            int[] cuts = new int[NumItems];
            int[] demand = new int[NumItems];
            // item weights and demands
            for (int cnt = 0; cnt < NumItems; cnt++)
            {
                cuts[cnt] = rand.Next(itemSizeMin, itemSizeMax); ;
                demand[cnt] = rand.Next(itemDemandMin, itemDemandMax);
                if (verbose)
                {
                    System.Console.WriteLine(String.Format("item[{0}]\tweight={1}\tdemand={2}", cnt, cuts[cnt], demand[cnt]));
                }
            }
            List<int[]> patterns;
            SolveKnapsack(maxPatterns, cuts, ClothWidth, efficiency, out patterns);
            SimplexSolver solver2 = new SimplexSolver();
            int vId = 0;
            int[] usage = new int[patterns.Count];
            // construct rows that make sure that the demand is met for each kind of cut 
            for (int cnt = 0; cnt < NumItems; cnt++)
            {
                solver2.AddRow(String.Format("item{0}", cnt), out vId);
                solver2.SetBounds(vId, demand[cnt], Rational.PositiveInfinity);
            }
            int patCnt = 0;
            if (verbose)
            {
                System.Console.WriteLine(String.Format("Generated {0} patterns", patterns.Count));
            }
            // set usage coeffs (A matrix entries) -- put the patterns as columns in the MIP.
            Dictionary<int, int> patIdForCol = new Dictionary<int, int>();
            foreach (int[] pattern in patterns)
            {
                int pId = 0;
                String varName = String.Format("Pattern{0}", patCnt);
                solver2.AddVariable(varName, out pId);
                patIdForCol[pId] = patCnt;
                solver2.SetIntegrality(pId, true);
                solver2.SetBounds(pId, 0, Rational.PositiveInfinity);
                for (int cnt = 0; cnt < NumItems; cnt++)
                {
                    solver2.SetCoefficient(cnt, pId, pattern[cnt]); // set the coefficient in the matrix
                                                                    // accumulate the quantity used for this pattern.  It will be used to figure out the scrap later.
                    usage[patCnt] += pattern[cnt] * cuts[cnt];
                }
                patCnt++;
            }
            // set objective coeffs.  --- the cost is the scrap 
            solver2.AddRow("Scrap", out vId);
            for (int cnt = 0; cnt < patterns.Count; cnt++)
            {
                int colId = solver2.GetIndexFromKey(String.Format("Pattern{0}", cnt));
                solver2.SetCoefficient(vId, colId, (ClothWidth - usage[cnt]));
            }
            solver2.AddGoal(vId, 0, true);
            // invoke the IP solver.
            SimplexSolverParams parms = new SimplexSolverParams();
            parms.MixedIntegerGenerateCuts = true;
            parms.MixedIntegerPresolve = true;

            if (saveMpsFile)
            {
                MpsWriter writer = new MpsWriter(solver2);
                using (TextWriter textWriter = new StreamWriter(File.OpenWrite("c:\\temp\\cutstock.mps")))
                {
                    writer.WriteMps(textWriter, true);
                }
            }
            solver2.Solve(parms);
            if (solver2.LpResult == LinearResult.Optimal &&
              solver2.MipResult == LinearResult.Optimal)
            {
                //Rational[] solutionVals = solver2.GetValues();
                int goalIndex = 0;
                // output if desired.
                if (verbose)
                {
                    System.Console.WriteLine("Solver complete, printing cut plan.");
                    foreach (int cnt in solver2.VariableIndices)
                    {
                        Rational val = solver2.GetValue(cnt);
                        if (val != 0)
                        {
                            if (solver2.IsGoal(cnt))
                            {
                                goalIndex = cnt;
                                System.Console.WriteLine(String.Format("Goal:{0}\t:   {1}\t", val, solver2.GetKeyFromIndex(cnt)));
                            }
                            else if (solver2.IsRow(cnt))
                            {
                                System.Console.WriteLine(String.Format("{0}:\tValue=   {1}\t", solver2.GetKeyFromIndex(cnt), val));

                            }
                            else
                            {
                                System.Console.Write(String.Format("{0}\tQuantity={1}:\t", solver2.GetKeyFromIndex(cnt), val));
                                for (int cnt2 = 0; cnt2 < NumItems; cnt2++)
                                {
                                    System.Console.Write(String.Format("{0} ", patterns[patIdForCol[cnt]][cnt2]));
                                }
                                System.Console.WriteLine(String.Format("\tUsage:{0} / {2} efficiency={1}%", usage[cnt - NumItems], (int)(100 * (double)usage[cnt - NumItems] / (double)ClothWidth), ClothWidth));
                            }
                        }
                    }
                    System.Console.WriteLine(String.Format("Total scrap={0}", solver2.GetSolutionValue(goalIndex)));
                }
            }
            else
            {
                System.Console.WriteLine("Generated problem is infeasible.  It is likely that more generated columns are needed.");
            }

            Console.WriteLine();
        }

        //static void Main(string[] args)
        //{
        //    String sampleToRun = "CuttingStock";
        //    if (args.Length > 0)
        //    {
        //        sampleToRun = args[0];
        //    }
        //    switch (sampleToRun)
        //    {
        //        case "Knapsack":
        //            Knapsack();
        //            break;
        //        case "ShortCuttingStock":
        //        default:
        //            ShortCuttingStock();
        //            break;
        //    }
        //}
    }
}
