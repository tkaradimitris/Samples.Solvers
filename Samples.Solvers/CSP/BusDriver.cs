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
using System.Text.RegularExpressions;
using System.Linq;

namespace Samples.Solvers.CSP
{

    public class BusDriversSample
    {
        //----------- User Guide Sample 4: Allocating Bus Drivers --------------------------------------

        // Helper function for reading the Bus Driver data file
        //
        static int ReadDriver(string line, out int[] tasks)
        {
            var numerals = line.TrimEnd().Split(' ').ToList();
            if ((numerals.Count < 3) || ((numerals.Count - 2) != Convert.ToInt32(numerals[1])))
                throw new ArgumentException("Expected: <cost> <tasks_no> <task1> <task2> ... <taskx> where x>=1 | " + line);
            tasks = new int[numerals.Count - 2];
            int cost = Convert.ToInt32(numerals[0]);
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Convert.ToInt32(numerals[i + 2]);
            }
            return cost;


            //List<string> numerals = Numerals(line);
            //if ((numerals.Count < 3) || ((numerals.Count - 2) != Convert.ToInt32(numerals[1])))
            //    throw new ArgumentException(line);
            //tasks = new int[numerals.Count - 2];
            //int cost = Convert.ToInt32(numerals[0]);
            //for (int i = 0; i < tasks.Length; i++)
            //{
            //    tasks[i] = Convert.ToInt32(numerals[i + 2]);
            //}
            //return cost;
        }

        ///// <summary> Helper function for reading the Bus Driver data file
        ///// </summary>
        //static List<string> Numerals(string line)
        //{
        //    List<string> result = new List<string>();
        //    int left = 0;
        //    while (left < line.Length)
        //    {
        //        char c = line[left];
        //        if (('0' <= c) && (c <= '9'))
        //        {
        //            int right = left + 1;
        //            while ((right < line.Length) && ('0' <= line[right]) && (line[right] <= '9'))
        //                right++;
        //            result.Add(line.Substring(left, right - left));
        //            left = right + 1;
        //        }
        //        else
        //            left++;
        //    }
        //    return result;
        //}

        /// <summary> Bus Drivers.  Data taken from data files of London bus companies, with the
        ///           problem being to find the cheapest, complete, non-overlapping set of task
        ///           assignments that will give a feasible schedule.
        /// </summary>
        public static void BusDrivers(string sourceFilePath)
        {

            // http://www-old.cs.st-andrews.ac.uk/~ianm/CSPLib/prob/prob022/index.html

            ConstraintSystem S = ConstraintSystem.CreateSolver();

            List<CspTerm> driverCosts = new List<CspTerm>();
            List<int[]> driversTasks = new List<int[]>();
            int nTasks = 0;

            // Read the data file.  Each row specifies a driver cost, a count of tasks, and the task numbers

            try
            {
                using (StreamReader sr = new StreamReader(sourceFilePath))
                {
                    String line;

                    while ((line = sr.ReadLine()) != null)
                    {
                        int[] tasks;
                        driverCosts.Add(S.Constant(ReadDriver(line, out tasks)));
                        nTasks += tasks.Length;
                        Array.Sort<int>(tasks);
                        driversTasks.Add(tasks);
                    }
                }
                int nDrivers = driversTasks.Count;

                // create a master list of tasks by sorting the raw union and then compressing out duplicates.

                int[] allTasks = new int[nTasks];
                nTasks = 0;
                foreach (int[] tasks in driversTasks)
                {
                    foreach (int x in tasks)
                        allTasks[nTasks++] = x;
                }
                Array.Sort<int>(allTasks);
                nTasks = 0;
                for (int i = 1; i < allTasks.Length; i++)
                {
                    if (allTasks[nTasks] < allTasks[i])
                        allTasks[++nTasks] = allTasks[i];
                }
                nTasks++;
                Array.Resize<int>(ref allTasks, nTasks);

                // We now have an array of all the tasks, and a list of all the drivers.

                // The problem statement comes down to:
                //    - each task must be assigned exactly once
                //    - minimize the cost of drivers

                // We add a boolean vector representing the drivers, true if the driver is to be used.

                CspTerm[] driversUsed = S.CreateBooleanVector("drivers", nDrivers);   // these are the Decision Variables

                //  We now create an array which maps which tasks are in which drivers.
                //  In addition to this static map, we create a dynamic map of the usage and the costs.

                CspTerm[][] taskActualUse = new CspTerm[nTasks][];
                CspTerm[] driverActualCost = new CspTerm[nDrivers];
                for (int t = 0; t < nTasks; t++)
                {
                    taskActualUse[t] = new CspTerm[nDrivers];
                    for (int r = 0; r < nDrivers; r++)
                    {
                        taskActualUse[t][r] = (0 <= Array.BinarySearch<int>(driversTasks[r], allTasks[t])) ? driversUsed[r] : S.False;
                    }
                    S.AddConstraints(
                      S.ExactlyMofN(1, taskActualUse[t])    // this task appears exactly once
                    );
                }

                // set the goal

                for (int r = 0; r < nDrivers; r++)
                {
                    driverActualCost[r] = driversUsed[r] * driverCosts[r];   // dynamic cost map
                }
                S.TryAddMinimizationGoals(S.Sum(driverActualCost));

                // now run the Solver and print the solutions

                int solnId = 0;
                ConstraintSolverSolution soln = S.Solve();
                if (soln.HasFoundSolution)
                {
                    System.Console.WriteLine("Solution #" + solnId++);
                    for (int d = 0; d < driversUsed.Length; d++)
                    {
                        object isUsed;
                        if (!soln.TryGetValue(driversUsed[d], out isUsed))
                            throw new InvalidProgramException("can't find drive in the solution: " + d.ToString());

                        // Take only the decision variables which turn out true.
                        // For each true row, print the row number and the list of tasks.

                        if (1 == (int)isUsed)
                        {
                            StringBuilder line = new StringBuilder(d.ToString());
                            line.Append(": ");
                            foreach (int x in driversTasks[d])
                            {
                                line.Append(x.ToString()).Append(", ");
                            }
                            System.Console.WriteLine(line.ToString());
                        }
                    }
                }
                if (solnId == 0)
                    System.Console.WriteLine("No solution found.");
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }
        }

        public static void Run(string parameters)
        {
            // http://www-old.cs.st-andrews.ac.uk/~ianm/CSPLib/prob/prob022/index.html

            ConstraintSystem S = ConstraintSystem.CreateSolver();

            List<CspTerm> driverCosts = new List<CspTerm>();
            List<int[]> driversTasks = new List<int[]>();
            int nTasks = 0;

            // Read the data file.  Each row specifies a driver cost, a count of tasks, and the task numbers
            try
            {
                //parse parameters to extract data
                //<line no> = bus driver no-1 (eg line 1 = driver0)
                //1 4 3 4 16 17 
                //1 = cost of driver
                // 4 = number of bus routes (tasks)
                // 3, 4, 16, 17 = the bus routes (tasks) for the driver
                var lines = Regex.Split(parameters, "\r\n|\r|\n");
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    int[] tasks;
                    driverCosts.Add(S.Constant(ReadDriver(line, out tasks)));
                    nTasks += tasks.Length;
                    Array.Sort<int>(tasks);
                    driversTasks.Add(tasks);

                }
                int nDrivers = driversTasks.Count;

                // create a master list of unique tasks (bus routes) that be assigned to drivers
                List<int> tasksU = new List<int>();
                driversTasks.ForEach(x => tasksU.AddRange(x));
                int[] allTasks = tasksU.OrderBy(x => x).Distinct().ToArray();
                nTasks = allTasks.Length;


                // We now have an array of all the tasks, and a list of all the drivers.

                // The problem statement comes down to:
                //    - each task must be assigned exactly once
                //    - minimize the cost of drivers

                // We add a boolean vector representing the drivers, true if the driver is to be used.

                CspTerm[] driversUsed = S.CreateBooleanVector("drivers", nDrivers);   // these are the Decision Variables

                //  We now create an array which maps which tasks are in which drivers.
                //  In addition to this static map, we create a dynamic map of the usage and the costs.
                CspTerm[][] taskActualUse = new CspTerm[nTasks][];
                CspTerm[] driverActualCost = new CspTerm[nDrivers];
                for (int t = 0; t < nTasks; t++) //for each task / bus route
                {
                    taskActualUse[t] = new CspTerm[nDrivers]; //for each route, array of all bus drivers (used to flag who may drive the route)
                    for (int r = 0; r < nDrivers; r++) //for each driver
                    {
                        taskActualUse[t][r] = (0 <= Array.BinarySearch<int>(driversTasks[r], allTasks[t])) ? driversUsed[r] : S.False;
                    }
                    S.AddConstraints(
                      S.ExactlyMofN(1, taskActualUse[t])    // this task appears exactly once
                    );
                }

                // set the goal: minimize total driver's cost
                for (int r = 0; r < nDrivers; r++)
                {
                    driverActualCost[r] = driversUsed[r] * driverCosts[r];   // dynamic cost map
                }
                S.TryAddMinimizationGoals(S.Sum(driverActualCost));

                // now run the Solver and print the solutions
                int solnId = 0;
                ConstraintSolverSolution soln = S.Solve();
                if (soln.HasFoundSolution)
                {
                    System.Console.WriteLine("Solution #" + solnId++);
                    for (int d = 0; d < driversUsed.Length; d++)
                    {
                        object isUsed;
                        if (!soln.TryGetValue(driversUsed[d], out isUsed))
                            throw new InvalidProgramException("can't find drive in the solution: " + d.ToString());

                        // Take only the decision variables which turn out true.
                        // For each true row, print the row number and the list of tasks.

                        if (1 == (int)isUsed)
                        {
                            StringBuilder line = new StringBuilder(d.ToString());
                            line.Append(": ");
                            foreach (int x in driversTasks[d])
                            {
                                line.Append(x.ToString()).Append(", ");
                            }
                            System.Console.WriteLine(line.ToString());
                        }
                    }
                }
                if (solnId == 0)
                    System.Console.WriteLine("No solution found.");
            }
            catch (Exception e)
            {
                // Let the user know what went wrong.
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }
        }

        public static void Run()
        {
            string parameters = @"1 2 11 18 
1 3 11 3 4 
1 3 11 18 19 
1 4 11 12 14 15 
1 4 11 18 19 20 
1 4 11 12 19 20 
1 2 1 18 
1 3 1 3 4 
1 3 1 18 19 
1 4 1 2 14 15 
1 4 1 18 19 20 
1 4 1 2 19 20 
1 4 1 2 3 10 
1 2 7 18 
1 3 7 3 4 
1 3 7 18 19 
1 3 7 14 15 
1 4 7 18 19 20 
1 4 7 8 9 10 
1 4 7 14 15 16 
1 5 7 8 9 5 6 
1 5 7 3 4 5 6 
1 4 12 13 14 10 
1 4 12 13 15 16 
1 4 12 13 5 6 
1 4 12 13 20 21 
1 4 12 13 14 21 
1 3 2 3 10 
1 4 2 3 15 16 
1 4 2 3 5 6 
1 4 2 3 20 21 
1 4 2 3 4 21 
1 3 8 9 10 
1 4 8 9 5 6 
1 4 8 9 20 21 
1 4 8 9 16 17 
1 3 13 14 10 
1 3 13 14 21 
1 4 13 14 16 17 
1 4 13 14 15 17 
1 5 13 14 15 16 22 
1 4 13 14 21 22 
1 3 3 4 21 
1 4 3 4 16 17 
1 4 3 4 21 22 
1 2 18 10 
1 3 18 15 16 
1 3 18 5 6 
1 3 18 20 21 
1 3 18 19 21 
1 4 18 15 16 17 
1 4 18 19 16 17 
1 4 18 19 20 17 
1 4 18 20 21 22 
1 4 18 19 21 22 
1 4 18 19 20 22 
1 3 14 15 17 
1 4 14 15 16 22 
1 4 4 5 6 23 
1 3 19 20 17 
1 3 19 20 22 
1 4 19 20 21 23 
1 4 19 20 22 23 
1 4 19 20 21 0 
1 3 15 16 22 
1 4 15 16 17 23 
1 4 15 16 22 23 
1 4 15 16 17 0 
1 3 5 6 23 
1 3 20 21 23 
1 3 20 21 0 
1 2 10 22 
1 3 10 22 23 
1 3 16 17 23 
1 3 16 17 0 
1 2 21 23 
1 2 21 0 
";
            Run(parameters);
        }

        //public static void Main(string[] args)
        //{
        //    if (args.Length != 1)
        //    {
        //        Console.WriteLine("Usage: BusDriver <input file>");
        //        return;
        //    }
        //    BusDriversSample.BusDrivers(args[0]);
        //}

    }
}

