/*==============================================================================
// Copyright © Microsoft Corporation.  All Rights Reserved.
// This code released under the terms of the 
// Microsoft Public License (MS-PL, http://opensource.org/licenses/ms-pl.html.)
==============================================================================*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.SolverFoundation.Common;
using Microsoft.SolverFoundation.Solvers;

namespace Samples.Solvers.CSP
{
    public class FootballMatchRoundRobinSample
    {
        /// <summary> ------------ User Guide Sample 3: Sports-team round-robin schedule ---------------
        /// </summary>
        /// 

        static private CspTerm[] GetColumn(CspTerm[][] termArray, int column)
        {
            int N = termArray.Length;
            if (N < 1)
                return null;
            CspTerm[] slice = new CspTerm[N];
            for (int row = 0; row < N; row++)
                slice[row] = termArray[row][column];
            return slice;
        }

        public static void Run(int teamsNo)
        {

            // schedule N teams to play N-1 matches (one against every other team) with a difference
            //   of no more than 1 extra game away or home.  Note that N must be even (since every team
            //   must be paired every week).

            ConstraintSystem S = ConstraintSystem.CreateSolver();

            // The teams are numbered 0 to N-1 for simplicity in index lookups,
            //    since our arrays are zero-based.
            CspDomain Teams = S.CreateIntegerInterval(0, teamsNo - 1);


            CspTerm[][] matches = S.CreateVariableArray(Teams, "opponents", teamsNo, teamsNo - 1);

            CspTerm[][] atHome = S.CreateBooleanArray("atHome", teamsNo, teamsNo - 1);

            // each row represents the N-1 games the teams play.  The 0th week has an even-odd
            //  assignment since by symmetry that is equivalent to any other assignment and
            //  we thereby eliminate redundant solutions being enumerated.

            for (int t = 0; t < teamsNo; t++)
            {
                CspTerm atHomeSum = S.Sum(atHome[t]);
                S.AddConstraints(
                  S.Unequal(t, matches[t]),                         // don't play self, and play every other team
                  S.LessEqual(teamsNo / 2 - 1, atHomeSum, S.Constant(teamsNo / 2)), // a balance of atHomes
                  S.Equal(t ^ 1, matches[t][0])                     // even-odd pairing in the initial round
                );
            }

            for (int w = 0; w < teamsNo - 1; w++)
            {
                S.AddConstraints(
                  S.Unequal(GetColumn(matches, w))                // every team appears once each week
                );
                for (int t = 0; t < teamsNo; t++)
                {
                    S.AddConstraints(
                      S.Equal(t, S.Index(matches, matches[t][w], w)),           // Each team's pair's pair must be itself.
                      S.Equal(atHome[t][w], !S.Index(atHome, matches[t][w], w)) // Each pair is Home-Away or Away-Home.
                      );
                }
            }

            // That's it!  The problem is delivered to the solver.
            // Now to get an answer...

            //bool unsolved = true;
            ConstraintSolverSolution soln = S.Solve();
            if (soln.HasFoundSolution)
            {
                //unsolved = false;

                Console.Write("       | ");
                for (int w = 0; w < teamsNo - 1; w++)
                    Console.Write("{1}Wk{0,2}", w + 1, w == 0 ? "" : " | ");
                Console.WriteLine();
                Console.Write("       | ");
                for (int w = 0; w < teamsNo - 1; w++)
                    Console.Write("{1}OP H", w+1, w == 0 ? "" : " | ");
                Console.WriteLine();
                Console.WriteLine("       {0}", "|" + new String('-', teamsNo * 6));
                for (int t = 0; t < teamsNo; t++)
                {
                    StringBuilder line = new StringBuilder();
                    line.AppendFormat("Team {0,2}| ", t + 1);
                    for (int w = 0; w < teamsNo - 1; w++)
                    {
                        object opponent, home;
                        if (!soln.TryGetValue(matches[t][w], out opponent))
                            throw new InvalidProgramException(matches[t][w].Key.ToString());
                        if (!soln.TryGetValue(atHome[t][w], out home))
                            throw new InvalidProgramException(atHome[t][w].Key.ToString());
                        line.AppendFormat("{2}{0,2} {1}", 
                            ((int)opponent) + 1,
                            (int)home == 1 ? "*" : " ",
                            w == 0 ? "" : " | "
                            );
                        //line.Append(opponent.ToString());
                        //line.Append(((int)home == 1) ? " H," : " A,");
                    }
                    System.Console.WriteLine(line.ToString());
                }
                System.Console.WriteLine();
            }
            else
                System.Console.WriteLine("No solution found.");
        }

        //public static void Main(string[] args)
        //{
        //    RoundRobinMatchesSample.RoundRobin(8);
        //}
    }
}

