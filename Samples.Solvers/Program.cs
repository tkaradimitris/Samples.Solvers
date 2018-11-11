using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SolverFoundation;
using Microsoft.SolverFoundation.Solvers;

namespace Samples.Solvers
{
    class Program
    {
        static void Main(string[] args)
        {
            // The code provided will print ‘Hello World’ to the console.
            // Press Ctrl+F5 (or go to Debug > Start Without Debugging) to run your app.

            //https://msdn.microsoft.com/en-us/library/ff524501(v=vs.93).aspx#Solvers%20Samples

            //var context = new Microsoft.SolverFoundation.Services.SolverContext();
            //var env = new Env();
            //var solver = new SimplexSolver(env);
            //solver.Add

            var options = InitOptions();
            int option = -1;
            int? param1;
            int? param2;
            while (true)
            {
                while (option == -1)
                {
                    PrintOptions(options);
                    option = ReadSelection(label: "select option no", defaultNo: 1);
                    if (option > 0 && (option > options.Count || option < 0)) option = -1;
                }

                if (option == 0) Environment.Exit(0);
                Console.WriteLine();

                Console.WriteLine(options[option]);
                if (option == 1)
                {
                    param1 = ReadSelection("Max agents", 100);
                    param2 = ReadSelection("Max solutions", 100);
                    var solveShifts = new CSP.ShiftsPlanner(param1.GetValueOrDefault());
                    solveShifts.Solve(maxSolutions: param2.GetValueOrDefault());
                    if (solveShifts.ShiftsForce != null && solveShifts.ShiftsForce.Count > 0)
                        solveShifts.ShowSolution(no: 1, shiftsForce: solveShifts.ShiftsForce.First().Value, showAgents: false, showSlots: true);
                }
                else if (option == 2) CSP.BusDriversSample.Run();
                else if (option == 3) CSP.ZebraSample.Run();
                else if (option == 4)
                {
                    param1 = 8;
                    param1 = ReadSelection("Teams", param1);
                    if (param1.GetValueOrDefault() < 1 || param1.GetValueOrDefault() > 10) break;
                    CSP.FootballMatchRoundRobinSample.Run(teamsNo: param1.Value);
                }
                else if(option == 5)
                {
                    // All the below samples takes advantage of the convention for vids in CQN solver
                    // vids convention is that the only row/goal gets vid 0 and each 
                    // variable gets vids from 1 ... VariableCount, by the order they were added
                    CQN.Rosenbrock.SolveRosenbrock();
                    CQN.Rosenbrock.SolveFirstMultidimensionalVariant(1000);
                    CQN.Rosenbrock.SolveSecondMultidimensionalVariant(500);
                }
                else if (option == 6)
                {
                    MIP.CuttingStock.Knapsack();
                    MIP.CuttingStock.ShortCuttingStock();
                }


                Console.WriteLine("-press any key to restart or Ctrl-c to exit -");
                Console.ReadKey();
                option = -1;
            }
        }

        protected static Dictionary<int, string> InitOptions()
        {
            Dictionary<int, string> options = new Dictionary<int, string>();
            options.Add(1, "CCN Shifts - Minimize agents for daily requirements");
            options.Add(2, "Bus Drivers - Minimize Cost");
            options.Add(3, "Zebra - Find Who-Color-Pet-Drink");
            options.Add(4, "RoundRobin - Sched Teams paired matches");
            options.Add(5, "Rosenbrock - Rosenbrock function");
            options.Add(6, "CuttingStock - Cut cloth roll with min waste");
            return options;
        }

        protected static void PrintOptions(Dictionary<int, string> options)
        {
            Console.Clear();
            Console.WriteLine(" Solver Samples");
            if (options == null || options.Count == 0)
            {
                Console.Write(" No options defined! Fix code!{0}", Environment.NewLine);
                return;
            }
        
            foreach (var option in options)
                Console.WriteLine("  {0,-2}. {1}", option.Key, option.Value);
            Console.WriteLine("      {0}", "-press 0 to exit-");
        }

        protected static int ReadSelection(string label, int? defaultNo = null)
        {
            Console.Write("  {0}{1}: ",
                label,
                defaultNo.HasValue ? string.Format(" [{0}]", defaultNo) : ""
                );
            string input = Console.ReadLine();

            int i = 0;
            bool ok = int.TryParse(input, out i);
            if (!ok && defaultNo.HasValue)
                i = defaultNo.Value;
            else if (!ok)
                i = -1;

            //Console.WriteLine("{0}", i == -1 ? "-invalid-" : i.ToString());
            return i;
        }
    }
}
