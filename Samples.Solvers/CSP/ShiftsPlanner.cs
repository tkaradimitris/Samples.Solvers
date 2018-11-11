using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SolverFoundation.Common;
using Microsoft.SolverFoundation.Solvers;

namespace Samples.Solvers.CSP
{
    public class ShiftsPlanner
    {
        public List<Models.Shift> Shifts { get; set; }
        public Dictionary<int, List<Models.ShiftForce>> ShiftsForce { get; set; }
        public List<Models.HalfHourRequirement> HalfHourRequirements { get; set; }
        public int MaxAgents { get; private set; }


        public ShiftsPlanner(int maxAgents = 100)
        {
            MaxAgents = maxAgents;
            Init();
            //Init(maxAgents: maxAgentsPer30min);
        }

        public ConstraintSolverSolution SolveXX()
        {
            ConstraintSystem S = ConstraintSystem.CreateSolver();


            // The shifts are numbered 0 to N-1 for simplicity in index lookups,
            //    since our arrays are zero-based.
            //CspDomain cspShifts = S.CreateIntegerInterval(first: 0, last: Shifts.Count - 1);
            CspDomain cspShifts = S.CreateIntegerInterval(first: 0, last: 200);

            //The force no per shift (decision)
            //CspTerm[][] cspShiftsForce = S.CreateVariableArray(domain: cspShifts, key: "force", rows: ShiftsForce.Count, columns: 1);
            //CspTerm[] cspShiftsForce = S.CreateVariableVector(domain: cspShifts, key: "force", length: Shifts.Count);
            CspTerm[][] cspShiftsForce = S.CreateVariableArray(domain: cspShifts, key: "force", rows: Shifts.Count, columns: 1);

            //index shifts, their variable CspTerm by the shift's relative no (0=first, 1=second, etc)
            Dictionary<int, Tuple<Models.Shift, CspTerm[]>> dictShifts = new Dictionary<int, Tuple<Models.Shift, CspTerm[]>>();
            int i = 0;
            Shifts.ForEach(x => { dictShifts.Add(i, new Tuple<Models.Shift, CspTerm[]>(x, cspShiftsForce[i])); i++; });
            //S.AddConstraints(S.GreaterEqual(constant: 0, inputs: dictShifts.Values.Select(x => x.Item2).ToArray()));

            //var halfHours = ShiftForce.GetHalfTimes(Shifts);

            //Constraint - Agents on every half hour must be >= requirement for that half hour
            foreach (var halfHourRq in HalfHourRequirements)
            {
                //find shifts including that halftime, and calculate their sum of force
                List<CspTerm> cspActive = new List<CspTerm>();
                foreach (var entry in dictShifts)
                {
                    if (entry.Value.Item1.IncludesHalfHour(halfHourRq.Start))
                        cspActive.Add(entry.Value.Item2[0]);
                }

                //add constraint for sum of force of active shifts on that halfhour
                if (cspActive.Count > 0)
                    S.AddConstraints(
                      S.GreaterEqual(constant: halfHourRq.RequiredForce, inputs: S.Sum(cspActive.ToArray()))
                    );
            }

            var goal = S.Sum(dictShifts.Values.Select(x=>x.Item2[0]).ToArray());
            bool ok = S.TryAddMinimizationGoals(goal);

            ConstraintSolverSolution solution = S.Solve();
            Console.WriteLine("*** Requirements ***");
            HalfHourRequirements.ForEach(x => Console.WriteLine("  {0:hh}:{0:mm} :: {1,3} agent(s)",x.Start, x.RequiredForce));
            Console.WriteLine();
            if (solution.HasFoundSolution)
            {
                Console.WriteLine("*** Agents per Shift ***");
                foreach (var entry in dictShifts)
                {
                    
                    object oAgents;
                    if (!solution.TryGetValue(entry.Value.Item2[0], out oAgents))
                        throw new InvalidProgramException("can't find shift in the solution: " + entry.Value.Item1.Name);

                    // Take only the decision variables which turn out true.
                    // For each true row, print the row number and the list of tasks.

                    int agentsNo = (int)oAgents;
                    Console.Write("  {0} | {2:hh}:{2:mm}-{3:hh}:{3:mm} | {1,3} [{4}] agent(s)\n", 
                        entry.Value.Item1.Name, 
                        agentsNo, 
                        entry.Value.Item1.Start, 
                        entry.Value.Item1.End,
                        entry.Value.Item2[0].Key);
                }
                Console.WriteLine();
            }
            else
                Console.WriteLine("No solution found");
            return solution;
        }

        public ConstraintSolverSolution Solve(int maxSolutions = 100)
        {
            if (maxSolutions < 1) maxSolutions = 1;
            if (maxSolutions > 1000) maxSolutions = 1000;

            ConstraintSystem S = ConstraintSystem.CreateSolver();


            //Define how many agents may work per shift
            CspDomain cspShiftsForceDomain = S.CreateIntegerInterval(first: 0, last: MaxAgents);

            //Decision variables, one per shift, that will hold the result of how may agents must work per shift to fullfil requirements
            CspTerm[] cspShiftsForce = S.CreateVariableVector(domain: cspShiftsForceDomain, key: "force", length: Shifts.Count);
            
            //index shifts, their variable CspTerm by the shift's relative no (0=first, 1=second, etc)
            Dictionary<Models.Shift, CspTerm> shiftsX = new Dictionary<Models.Shift, CspTerm>();
            int i = 0;
            Shifts.ForEach(x => { shiftsX.Add(x, cspShiftsForce[i]); i++; });
            //S.AddConstraints(S.GreaterEqual(constant: 0, inputs: dictShifts.Values.Select(x => x.Item2).ToArray()));

            //var halfHours = ShiftForce.GetHalfTimes(Shifts);

            //Constraint - Agents on every half hour must be >= requirement for that half hour
            foreach (var halfHourRq in HalfHourRequirements)
            {
                //find shifts including that halftime, and calculate their sum of force
                List<CspTerm> cspActive = new List<CspTerm>();
                foreach (var entry in shiftsX)
                {
                    if (entry.Key.IncludesHalfHour(halfHourRq.Start))
                        cspActive.Add(entry.Value);
                }

                //add constraint for sum of force of active shifts on that halfhour
                if (cspActive.Count > 0)
                    S.AddConstraints(
                      S.LessEqual(constant: halfHourRq.RequiredForce, inputs: S.Sum(cspActive.ToArray()))
                    );
            }

            var goal = S.Sum(shiftsX.Values.ToArray());
            bool ok = S.TryAddMinimizationGoals(goal);

            ConstraintSolverSolution solution = S.Solve();
            //Console.WriteLine("*** Requirements ***");
            //HalfHourRequirements.ForEach(x => Console.WriteLine("  {0:hh}:{0:mm} :: {1,3} agent(s)", x.Start, x.RequiredForce));
            Console.WriteLine();
            if (solution.HasFoundSolution)
            {
                int no = 1;
                ShiftsForce = new Dictionary<int, List<Models.ShiftForce>>();
                while (solution.HasFoundSolution)
                {
                    if (no > maxSolutions) break;
                    var shiftsForce = GetResults(solution: solution, shiftsX: shiftsX);
                    if (shiftsForce != null)
                        ShiftsForce.Add(no, shiftsForce);
                    //ShowSolution(solution: solution, shiftsX: shiftsX, no: no);
                    solution.GetNext();
                    no++;
                }
            }
            else
            {
                ShiftsForce = null;
                Console.WriteLine("No solution found");
            }

            if (ShiftsForce != null)
                foreach (var shiftForceEntry in ShiftsForce)
                    ShowSolution(no: shiftForceEntry.Key, shiftsForce: shiftForceEntry.Value, showAgents: true, showSlots: false);

            return solution;
        }

        protected List<Models.ShiftForce> GetResults(ConstraintSolverSolution solution, Dictionary<Models.Shift, CspTerm> shiftsX)
        {
            if (solution == null || !solution.HasFoundSolution) return null;

            int force;
            List<Models.ShiftForce> shiftsForce = new List<Models.ShiftForce>();
            foreach (var entry in shiftsX)
            {
                force = solution.GetIntegerValue(entry.Value);
                var shiftForce = new Models.ShiftForce(entry.Key, force);
                shiftsForce.Add(shiftForce);
            }
            return shiftsForce;
        }

        public void ShowSolution(int no, List<Models.ShiftForce> shiftsForce, bool showAgents = true, bool showSlots = true)
        {
            if (showAgents)
            {
                Console.WriteLine(" ┌─────────────────────────────────┐");
                Console.WriteLine(" │ #{0,-3} Agents per Shift           │", no);
                Console.WriteLine(" ├──────┬─────────────┬────────────┤");

                foreach (var shiftForce in shiftsForce)
                {
                    Console.WriteLine(" │ {0,4} │ {2:hh}:{2:mm}-{3:hh}:{3:mm} │ {1,3} agent{4} │",
                        shiftForce.Name,
                        shiftForce.Force,
                        shiftForce.Start,
                        shiftForce.End,
                        shiftForce.Force == 1 ? " " : "s");
                }
                Console.WriteLine(" ├──────┼─────────────┼────────────┤");
                Console.WriteLine(" | {0,4} │ {2:hh}:{2:mm}-{3:hh}:{3:mm} │ {1,3} agent{4} │",
                    "Σ",
                    shiftsForce.Sum(sf => sf.Force),
                    shiftsForce.Min(sf => sf.Start),
                    shiftsForce.Max(sf => sf.End),
                    shiftsForce.Sum(sf => sf.Force) == 1 ? "" : "s");
                Console.WriteLine(" └──────┴─────────────┴────────────┘");
                Console.WriteLine();
            }

            if (showSlots)
            {
                Console.WriteLine(" ┌─────────────────────────────────┐");
                Console.WriteLine(" │  #{0,-3} Schedule Status           │", no);
                Console.WriteLine(" ├────────┬─────┬─────┬────────────┤");
                Console.WriteLine(" │    30' │ Req │ Act | Excs │ ERR |");
                Console.WriteLine(" ├────────┼─────┼─────┼──────┼─────┤");
                HalfHourRequirements.ForEach(hh =>
                {
                    var totalForce = shiftsForce.Sum(sf => sf.IncludesHalfHour(hh.Start) ? sf.Force : 0);
                    Console.WriteLine(" │  {0:hh}:{0:mm} │ {1,3} │ {2,3} │ {4,4} │ {3} │",
                        hh.Start,
                        hh.RequiredForce,
                        totalForce,
                        totalForce >= hh.RequiredForce ? "   " : "ERR",
                        totalForce - hh.RequiredForce
                        );
                });
                Console.WriteLine(" ├────────┴─────┼─────┼──────┴─────┤");
                Console.WriteLine(" │ Total Agents │ {0,3} │            │", shiftsForce.Sum(sf => sf.Force));
                Console.WriteLine(" └──────────────┴──────────────────┘");
                Console.WriteLine();
            }
        }

        protected void Init()//int maxAgents = 40)
        {
            TimeSpan ts8h = GetTimeSpanHours(8);
            TimeSpan ts10h = GetTimeSpanHours(10);
            TimeSpan ts30mi = GetTimeSpanMinutes(30);

            Shifts = new List<Models.Shift>();
            Shifts.Add(new Models.Shift(name: "A", start: GetTimeSpanHours(7), duration: ts8h));
            Shifts.Add(new Models.Shift(name: "B", start: GetTimeSpanHours(9), duration: ts8h));
            Shifts.Add(new Models.Shift(name: "C", start: GetTimeSpanHours(11), duration: ts8h));
            Shifts.Add(new Models.Shift(name: "D", start: GetTimeSpanHours(13), duration: ts8h));
            Shifts.Add(new Models.Shift(name: "E", start: GetTimeSpanHours(15), duration: ts8h));
            Shifts.Add(new Models.Shift(name: "F", start: GetTimeSpanHours(17), duration: ts8h));
            //Shifts.Add(new Shift(name: "G", start: GetTimeSpanHours(19), duration: ts8h));

            ShiftsForce = null;// Models.ShiftForce.FromShits(Shifts);

            var halfTimes = Models.Shift.GetHalfTimes(Shifts);

            TimeSpan start = Shifts.Min(x => x.Start);
            TimeSpan end = Shifts.Max(x => x.End);
            int[] liveData = new int[] { 6, 4, 4, 3, 2, 2, 1, 1, 1, 1, 1, 1, 1, 2, 3, 5, 10, 20, 29, 45, 51, 57, 61, 61, 61, 58, 58, 56, 54, 51, 48, 50, 43, 43, 41, 38, 37, 37, 35, 31, 27, 29, 24, 23, 18, 14, 13, 9 };
            HalfHourRequirements = new List<Models.HalfHourRequirement>();

            TimeSpan ts = GetTimeSpanHours(0);
            for (int i=0;i<liveData.Length;i++)
            {
                if (ts >= start && ts < end)
                {
                    HalfHourRequirements.Add(new Models.HalfHourRequirement(start: ts, requiredForce: liveData[i]));
                }
                ts = ts.Add(ts30mi);
            }
        }

        protected TimeSpan GetTimeSpanHours(int hours)
        {
            return new TimeSpan(hours: hours, minutes: 0, seconds: 0);
        }
        protected TimeSpan GetTimeSpanMinutes(int minutes)
        {
            return new TimeSpan(hours: 0, minutes: minutes, seconds: 0);
        }

        //public class Shift
        //{
        //    #region Properties
        //    public string Name { get; set; }
        //    public TimeSpan Start { get; set; }
        //    public TimeSpan Duration { get; set; }

        //    #region Readonly
        //    public TimeSpan End
        //    {
        //        get
        //        {
        //            return Start.Add(Duration);
        //        }
        //    }
        //    #endregion
        //    #endregion Properties

        //    public Shift(string name, TimeSpan start, TimeSpan duration)
        //    {
        //        Name = name;
        //        Start = start;
        //        Duration = duration;

        //        if (Start.Minutes != 0 && Start.Minutes != 30)
        //            throw new ArgumentException("Start time must be xx:00 or xx:30");
        //    }

        //    public bool IncludesHalfHour(TimeSpan halfHour)
        //    {
        //        return halfHour >= Start && halfHour < End;
        //    }

        //    public static List<TimeSpan> GetHalfTimes(List<Shift> shifts)
        //    {
        //        if (shifts == null) return null;
        //        List<TimeSpan> items = new List<TimeSpan>();

        //        TimeSpan min30 = new TimeSpan(hours: 0, minutes: 30, seconds: 0);
        //        foreach (var shift in shifts)
        //        {
        //            TimeSpan? ts = shift.Start;
        //            while (ts.HasValue)
        //            {
        //                if (!items.Contains(ts.Value))
        //                    items.Add(ts.Value);
        //                ts = ts.Value.Add(min30);
        //                if (ts > shift.End) ts = null;
        //            }
        //        }
        //        items.Sort();
        //        return items;
        //    }

        //    /// <summary>
        //    /// Use multiple shift to build a dicitonary to denote on which halfhour span each shift is active on
        //    /// </summary>
        //    /// <param name="shifts">The shifts to examine</param>
        //    /// <returns>A dictionary indexed by [halfhour start, shift no] with presence or not</returns>
        //    public static Dictionary<Tuple<TimeSpan, int>, bool> GetShiftsPresence(List<Shift> shifts)
        //    {
        //        //Find unique halfhours from the given shifts
        //        List<TimeSpan> spans = GetHalfTimes(shifts);
        //        if (spans == null) return null;

        //        Dictionary<Tuple<TimeSpan, int>, bool> presence = new Dictionary<Tuple<TimeSpan, int>, bool>();
        //        foreach (var span in spans)
        //        {
        //            Shift shift;
        //            for(var shiftNo=0;shiftNo<shifts.Count;shiftNo++)
        //            {
        //                shift = shifts[shiftNo];
        //                presence.Add(new Tuple<TimeSpan, int>(span, shiftNo), span >= shift.Start && span < shift.End);
        //            }
        //        }
        //        return presence;
        //    }

        //    public override string ToString()
        //    {
        //        return string.Format("{3} {0:hh}:{0:mm}-{1:hh}:{1:mm} {2:hh}h", Start, End, Duration, Name);
        //    }
        //}



    }
}
