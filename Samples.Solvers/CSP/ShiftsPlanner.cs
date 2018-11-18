using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SolverFoundation.Common;
using Microsoft.SolverFoundation.Services;
using Microsoft.SolverFoundation.Solvers;

namespace Samples.Solvers.CSP
{
    public class ShiftsPlanner
    {
        public List<Models.Shift> Shifts { get; set; }
        public List<Models.Shift> OvertimeShifts { get; set; }
        public Dictionary<int, List<Models.ShiftForce>> ShiftsForce { get; protected set; }
        public List<Models.HalfHourRequirement> HalfHourRequirements { get; set; }
        public int MaxAgents { get; private set; }
        protected Dictionary<Models.Shift, CspTerm> ShiftsX { get; set; }

        public ShiftsPlanner(int maxAgents = 100)
        {
            MaxAgents = maxAgents;
            //Init();
        }
        
        public ConstraintSolverSolution Solve(int maxSolutions = 100)
        {
            ConstraintSystem S = ConstraintSystem.CreateSolver();

            //Define how many agents may work per shift
            CspDomain cspShiftsForceDomain = S.CreateIntegerInterval(first: 0, last: MaxAgents);

            //Decision variables, one per shift, that will hold the result of how may agents must work per shift to fullfil requirements
            CspTerm[] cspShiftsForce = S.CreateVariableVector(domain: cspShiftsForceDomain, key: "force", length: Shifts.Count);
            
            //index shifts, their variable CspTerm by the shift's relative no (0=first, 1=second, etc)
            ShiftsX = new Dictionary<Models.Shift, CspTerm>();
            int i = 0;
            Shifts.ForEach(x => { ShiftsX.Add(x, cspShiftsForce[i]); i++; });

            //Constraint - Agents on every half hour must be >= requirement for that half hour
            foreach (var halfHourRq in HalfHourRequirements)
            {
                //find shifts including that halftime, and calculate their sum of force
                List<CspTerm> cspActive = new List<CspTerm>();
                foreach (var entry in ShiftsX)
                {
                    if (entry.Key.IncludesHalfHour(halfHourRq.Start))
                        cspActive.Add(entry.Value);
                }

                //add constraint for sum of force of active shifts on that halfhour
                //if we need agents but no shifts exists for a halfhour, do not add a constraint
                if (cspActive.Count > 0)
                    S.AddConstraints(
                      S.LessEqual(constant: halfHourRq.RequiredForce, inputs: S.Sum(cspActive.ToArray()))
                    );
            }

            var goal = S.Sum(ShiftsX.Values.ToArray());
            bool ok = S.TryAddMinimizationGoals(goal);

            ConstraintSolverSolution solution = S.Solve();

            Console.WriteLine();
            GetSolutionsAll(solution: solution, maxSolutions: maxSolutions);
            if (ShiftsForce == null || ShiftsForce.Count == 0)
                Console.WriteLine("No solution found");

            if (ShiftsForce != null)
                foreach (var shiftForceEntry in ShiftsForce)
                    ShowSolution(no: shiftForceEntry.Key, shiftsForce: shiftForceEntry.Value, showAgents: true, showSlots: false);

            return solution;
        }

        public ConstraintSolverSolution SolveB(int maxSolutions = 100)
        {
            ConstraintSystem S = ConstraintSystem.CreateSolver();
            
            //Define how many agents may work per shift
            CspDomain cspShiftsForceDomain = S.CreateIntegerInterval(first: 0, last: MaxAgents);

            //Decision variables, one per shift, that will hold the result of how may agents must work per shift to fullfil requirements
            CspTerm[] cspShiftsForce = S.CreateVariableVector(domain: cspShiftsForceDomain, key: "force", length: Shifts.Count);

            //index shifts, their variable CspTerm by the shift's relative no (0=first, 1=second, etc)
            ShiftsX = new Dictionary<Models.Shift, CspTerm>();
            int i = 0;
            Shifts.ForEach(x => { ShiftsX.Add(x, cspShiftsForce[i]); i++; });

            //Constraint - Agents on every half hour must be >= requirement for that half hour
            List<CspTerm> cspHalfHourExcess = new List<CspTerm>();
            foreach (var halfHourRq in HalfHourRequirements)
            {
                //find shifts including that halftime, and calculate their sum of force
                List<CspTerm> cspActive = new List<CspTerm>();
                foreach (var entry in ShiftsX)
                {
                    if (entry.Key.IncludesHalfHour(halfHourRq.Start))
                        cspActive.Add(entry.Value);
                }

                //add constraint for sum of force of active shifts on that halfhour
                //if we need agents but no shifts exists for a halfhour, do not add a constraint
                if (cspActive.Count > 0)
                {
                    var s = S.Sum(cspActive.ToArray()) - S.Constant(halfHourRq.RequiredForce);
                    S.AddConstraints(
                      S.LessEqual(constant: 0, inputs: s)
                    );
                    cspHalfHourExcess.Add(s);
                }
            }

            //var goal = S.Sum(shiftsX.Values.ToArray());
            //bool ok = S.TryAddMinimizationGoals(goal);

            var goalMinExcess = S.Sum(cspHalfHourExcess.ToArray());
            bool ok = S.TryAddMinimizationGoals(goalMinExcess);

            ConstraintSolverSolution solution = S.Solve();

            Console.WriteLine();
            GetSolutionsAll(solution: solution, maxSolutions: maxSolutions);
            if (ShiftsForce == null || ShiftsForce.Count == 0)
                Console.WriteLine("No solution found");

            if (ShiftsForce != null)
                foreach (var shiftForceEntry in ShiftsForce)
                    ShowSolution(no: shiftForceEntry.Key, shiftsForce: shiftForceEntry.Value, showAgents: true, showSlots: false);

            return solution;
        }

        public SimplexSolver PrepareModelledSolver(int maxAgents, out Dictionary<Models.Shift, int> shiftsExt, out int vidGoal)
        {
            var SX = new SimplexSolver();

            var maxRq = HalfHourRequirements.Max(x => x.RequiredForce);
            var sumRq = HalfHourRequirements.Sum(x => x.RequiredForce);

            SolverContext context = new SolverContext();
            var model = context.CreateModel();

            //model.AddGoal(name: "totalAgents", direction: GoalKind.Minimize, goal: null);


            shiftsExt = null;

            ////goal: total no of agents
            //int ridgoal;
            //SX.AddRow(key: "goal", vid: out ridgoal);
            //SX.SetBounds(ridgoal, lower: 0, upper: maxAgents); //total agents must range from 0 to max agents available
            //SX.AddGoal(vid: ridgoal, pri: 1, fMinimize: true); //minimize total number of agents for that day

            //goal: total no of agents, minimize overtime
            int ridgoal;
            SX.AddRow(key: "goal", vid: out ridgoal);
            SX.SetBounds(ridgoal, lower: 0, upper: Rational.PositiveInfinity);// upper: maxAgents); //total agents must range from 0 to max agents available
            SX.AddGoal(vid: ridgoal, pri: 2, fMinimize: true); //minimize total number of agents for that day

            //goal2: Minimize excess force
            int ridexcess;
            SX.AddRow(key: "excess", vid: out ridexcess);
            SX.SetBounds(ridexcess, lower: 0, upper: sumRq); //allow excess from 0 to sum of all requirements
            SX.AddGoal(vid: ridexcess, pri: 1, fMinimize: true); //try to minimize excess time

            var shiftsX = new Dictionary<Models.Shift, int>();
            int i = 0;
            Shifts.ForEach(x => {
                int vid;
                SX.AddVariable(key: string.Format("{0}. {1} {2:hh}:{2:mm} - {3:hh}:{3:mm}", i + 1, x.Name, x.Start, x.End), vid: out vid);
                //the no of agents on each shift may not exceed to max requirements
                SX.SetBounds(vid: vid, lower: 0, upper: maxRq);
                shiftsX.Add(x, vid);
                //SX.SetCoefficient(vidRow: ridgoal, vidVar: vid, num: 1); //normal weight: all shifts are the same
                SX.SetCoefficient(vidRow: ridgoal, vidVar: vid, num: x.Duration.Hours <= 8 ? 1 : 10); //weights per shift: overtime = *10 more expensive than normal shifts
                i++;
            });

            //Constraint - Agents from every active shift on every half hour must be >= requirement for that half hour
            HalfHourRequirements.ForEach(hh =>
            {
                List<int> vidShiftsActive = new List<int>();
                foreach (var entry in shiftsX)
                {
                    if (entry.Key.IncludesHalfHour(hh.Start))
                        vidShiftsActive.Add(entry.Value);
                }

                //add constraint for sum of force of active shifts on that halfhour
                //if we need agents but no shifts exists for a halfhour, do not add a constraint
                if (vidShiftsActive.Count > 0)
                {

                    int ridHalf;
                    SX.AddRow(key: string.Format("{0:hh}:{0:mm} [{1}]", hh.Start, hh.RequiredForce), vid: out ridHalf);
                    //specify whichs shifts contributes to half hour's total force
                    vidShiftsActive.ForEach(vidShift =>
                    {
                        SX.SetCoefficient(vidRow: ridHalf, vidVar: vidShift, num: 1);
                        SX.SetCoefficient(vidRow: ridexcess, vidVar: vidShift, num: 1);
                    });
                    //each 30' span, must have at least as many required agents
                    SX.SetBounds(vid: ridHalf, lower: hh.RequiredForce, upper: Rational.PositiveInfinity);
                }
            });

            shiftsExt = shiftsX;
            vidGoal = ridgoal;

            return SX;
        }

        public SimplexSolver PrepareSimplexSolver(int maxAgents, out Dictionary<Models.Shift, int> shiftsExt, out int vidGoal)
        {
            var SX = new SimplexSolver();

            var maxRq = HalfHourRequirements.Max(x => x.RequiredForce);
            var sumRq = HalfHourRequirements.Sum(x => x.RequiredForce);

            shiftsExt = null;

            ////goal: total no of agents
            //int ridgoal;
            //SX.AddRow(key: "goal", vid: out ridgoal);
            //SX.SetBounds(ridgoal, lower: 0, upper: maxAgents); //total agents must range from 0 to max agents available
            //SX.AddGoal(vid: ridgoal, pri: 1, fMinimize: true); //minimize total number of agents for that day

            //goal: total no of agents, minimize overtime
            int ridgoal;
            SX.AddRow(key: "goal", vid: out ridgoal);
            SX.SetBounds(ridgoal, lower: 0, upper: Rational.PositiveInfinity);// upper: maxAgents); //total agents must range from 0 to max agents available
            SX.AddGoal(vid: ridgoal, pri: 2, fMinimize: true); //minimize total number of agents for that day

            //goal2: Minimize excess force
            int ridexcess;
            SX.AddRow(key: "excess", vid: out ridexcess);
            SX.SetBounds(ridexcess, lower: 0, upper: sumRq); //allow excess from 0 to sum of all requirements
            SX.AddGoal(vid: ridexcess, pri: 1, fMinimize: true); //try to minimize excess time

            var shiftsX = new Dictionary<Models.Shift, int>();
            int i = 0;
            Shifts.ForEach(x => {
                int vid;
                SX.AddVariable(key: string.Format("{0}. {1} {2:hh}:{2:mm} - {3:hh}:{3:mm}", i+1, x.Name, x.Start, x.End), vid: out vid);
                //the no of agents on each shift may not exceed to max requirements
                SX.SetBounds(vid: vid, lower: 0, upper: maxRq);
                shiftsX.Add(x, vid);
                //SX.SetCoefficient(vidRow: ridgoal, vidVar: vid, num: 1); //normal weight: all shifts are the same
                SX.SetCoefficient(vidRow: ridgoal, vidVar: vid, num: x.Duration.Hours <= 8 ? 1 : 10); //weights per shift: overtime = *10 more expensive than normal shifts
                i++;
            });

            //Constraint - Agents from every active shift on every half hour must be >= requirement for that half hour
            HalfHourRequirements.ForEach( hh =>
            {
                List<int> vidShiftsActive = new List<int>();
                foreach (var entry in shiftsX)
                {
                    if (entry.Key.IncludesHalfHour(hh.Start))
                        vidShiftsActive.Add(entry.Value);
                }

                //add constraint for sum of force of active shifts on that halfhour
                //if we need agents but no shifts exists for a halfhour, do not add a constraint
                if (vidShiftsActive.Count > 0)
                {

                    int ridHalf;
                    SX.AddRow(key: string.Format("{0:hh}:{0:mm} [{1}]", hh.Start, hh.RequiredForce), vid: out ridHalf);
                    //specify whichs shifts contributes to half hour's total force
                    vidShiftsActive.ForEach(vidShift =>
                    {
                        SX.SetCoefficient(vidRow: ridHalf, vidVar: vidShift, num: 1);
                        SX.SetCoefficient(vidRow: ridexcess, vidVar: vidShift, num: 1);
                    });
                    //each 30' span, must have at least as many required agents
                    SX.SetBounds(vid: ridHalf, lower: hh.RequiredForce, upper: Rational.PositiveInfinity);
                }
            });

            shiftsExt = shiftsX;
            vidGoal = ridgoal;

            return SX;
        }

        public Solution alternative_solve(out Dictionary<Models.Shift, Decision> shiftsN, out Dictionary<Models.Shift, Decision> shiftsO)
        {
            SolverContext SC = SolverContext.GetContext();
            Model model = SC.CreateModel();

            var maxRq = HalfHourRequirements.Max(x => x.RequiredForce);
            shiftsN = shiftsO = null;

            //split shifts into two Dictionaries (each dict holds each Shift as key and the Decision variable as Value)
            var normal_shifts = new Dictionary<Models.Shift, Decision>(); //for normal shifts
            int i = 0;
            Shifts.ForEach(x => {
                Decision des = new Decision(Domain.IntegerRange(0, maxRq), string.Format("{0}_{1}", x.Name, i));
                normal_shifts.Add(x, des);
                model.AddDecision(des);
                i++;
            });

            var overtime_shifts = new Dictionary<Models.Shift, Decision>(); //for overtime shifts
            i = 0;
            OvertimeShifts.ForEach(x => {
                Decision des = new Decision(Domain.IntegerRange(0, maxRq), string.Format("{0}_{1}", x.Name, i));
                overtime_shifts.Add(x, des);
                model.AddDecision(des);
                i++;
            });

            List<Decision> excess = new List<Decision>(); //used to calculate sum of excess values
            HalfHourRequirements.ForEach(hh =>
            {
                var ShiftsActive = new List<Decision>();
                foreach (var entry in normal_shifts)
                {
                    if (entry.Key.IncludesHalfHour(hh.Start))
                    {
                        ShiftsActive.Add(entry.Value);
                        excess.Add(entry.Value);
                    }
                }
                foreach (var entry in overtime_shifts)
                {
                    if (entry.Key.IncludesHalfHour(hh.Start))
                    {
                        ShiftsActive.Add(entry.Value);
                        excess.Add(entry.Value);
                    }
                }

                //add constraint for sum of force of active shifts on that halfhour
                //if we need agents but no shifts exists for a halfhour, do not add a constraint
                if (ShiftsActive.Count > 0)
                {
                    var sum_constr = new SumTermBuilder(ShiftsActive.Count);
                    ShiftsActive.ForEach(s => sum_constr.Add(s));

                    var constr = sum_constr.ToTerm() >= hh.RequiredForce;
                    model.AddConstraint(string.Format("_{0:hh}{0:mm}", hh.Start), constr);

                    //constr = sum_constr.ToTerm() <= hh.RequiredForce * 2.0;
                    //model.AddConstraint(string.Format("limitconst_for_{0:hh}{0:mm}", hh.Start), constr);
                }
            });

            //Constrain maximum number of agents working 8hour shifts
            var max_agents_constraint = new SumTermBuilder(Shifts.Count);
            foreach (var entry in normal_shifts)
            {
                max_agents_constraint.Add(entry.Value);
            }
            var ma_constr = max_agents_constraint.ToTerm() <= MaxAgents;
            model.AddConstraint("Max_agents_constraint", ma_constr);

            //1st goal: Minimize overtime shifts (if work manageable by normal shifts, no overtime shifts will be used)
            var overtime_obj = new SumTermBuilder(OvertimeShifts.Count);
            foreach (var entry in overtime_shifts)
            {
                overtime_obj.Add(entry.Value);
            }
            model.AddGoal("Minimize_overtime", GoalKind.Minimize, overtime_obj.ToTerm());

            //2st goal: Minimize normal shifts
            var normal_obj = new SumTermBuilder(Shifts.Count);
            foreach (var entry in normal_shifts)
            {
                normal_obj.Add(entry.Value);
            }
            model.AddGoal("Minimize_normal", GoalKind.Minimize, normal_obj.ToTerm());

            //3rd goal: Try to minimize excess
            var sumReqs = HalfHourRequirements.Sum(x => x.RequiredForce);
            model.AddGoal("Minimize_excess", GoalKind.Minimize, Model.Sum(excess.ToArray()) - sumReqs);

            SimplexDirective simplex = new SimplexDirective();
            Solution solution = SC.Solve(simplex);

            shiftsN = normal_shifts;
            shiftsO = overtime_shifts;

            return solution;
        }

        public ConstraintSystem PrepareCspSolver()
        {
            ConstraintSystem S = ConstraintSystem.CreateSolver();

            //Define how many agents may work per shift
            var maxRq = HalfHourRequirements.Max(x => x.RequiredForce);
            CspDomain cspShiftsForceDomain = S.CreateIntegerInterval(first: 0, last: maxRq);
            //var cspShiftsForceDomain = S.CreateIntegerSet(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40 });

            //Decision variables, one per shift, that will hold the result of how may agents must work per shift to fullfil requirements
            CspTerm[] cspShiftsForce = S.CreateVariableVector(domain: cspShiftsForceDomain, key: "force", length: Shifts.Count);

            //index shifts, their variable CspTerm by the shift's relative no (0=first, 1=second, etc)
            ShiftsX = new Dictionary<Models.Shift, CspTerm>();
            int i = 0;
            Shifts.ForEach(x => { ShiftsX.Add(x, cspShiftsForce[i]); i++; });

            //Constraint - Agents from every active shift on every half hour must be >= requirement for that half hour
            List<CspTerm> cspHalfHourExcess = new List<CspTerm>();
            foreach (var halfHourRq in HalfHourRequirements)
            {
                //find shifts including that halftime, and calculate their sum of force
                List<CspTerm> cspActive = new List<CspTerm>();
                foreach (var entry in ShiftsX)
                {
                    if (entry.Key.IncludesHalfHour(halfHourRq.Start))
                        cspActive.Add(entry.Value);
                }

                //add constraint for sum of force of active shifts on that halfhour
                //if we need agents but no shifts exists for a halfhour, do not add a constraint
                if (cspActive.Count > 0)
                {
                    //var s = S.Sum(cspActive.ToArray()) - S.Constant(halfHourRq.RequiredForce);
                    S.AddConstraints(
                      S.LessEqual(constant: halfHourRq.RequiredForce, inputs: S.Sum(cspActive.ToArray()))
                    );
                    //cspHalfHourExcess.Add(s);
                }
            }

            //if (false && cspHalfHourExcess.Count > 0)
            //    S.AddConstraints(
            //      S.LessEqual(constant: 0, inputs: S.Sum(cspHalfHourExcess.ToArray()))
            //    );

            bool xx = true;
            if (xx)
            {
                var goal = S.Sum(ShiftsX.Values.ToArray());
                bool ok = S.TryAddMinimizationGoals(goal);
            }
            else
            {
                //S.RemoveAllMinimizationGoals();
                var goalMinExcess = S.Sum(cspHalfHourExcess.ToArray());
                bool ok = S.TryAddMinimizationGoals(goalMinExcess);
            }
            return S;
        }

        public void GetSolutionsAll(ConstraintSolverSolution solution, int maxSolutions)
        {
            if (solution == null) return;

            if (maxSolutions < 1) maxSolutions = 1;

            if (solution.HasFoundSolution)
            {
                int no = 1;
                ShiftsForce = new Dictionary<int, List<Models.ShiftForce>>();
                while (solution.HasFoundSolution)
                {
                    if (no > maxSolutions) break;
                    var shiftsForce = GetSolutionResults(solution: solution);
                    if (shiftsForce != null)
                        ShiftsForce.Add(no, shiftsForce);
                    solution.GetNext();
                    no++;
                }
            }
            else
            {
                ShiftsForce = null;
            }
        }

        protected List<Models.ShiftForce> GetSolutionResults(ConstraintSolverSolution solution)
        {
            if (solution == null || !solution.HasFoundSolution ||  ShiftsX == null || ShiftsX.Count == 0) return null;

            int force;
            List<Models.ShiftForce> shiftsForce = new List<Models.ShiftForce>();
            foreach (var entry in ShiftsX)
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
                Console.WriteLine(" ├────────┬─────┬─────┬──────┬─────┤");
                Console.WriteLine(" │    30' │ Req │ Act │ Excs │ ERR │");
                Console.WriteLine(" ├────────┼─────┼─────┼──────┼─────┤");
                int totExcess = 0;
                int excess;
                HalfHourRequirements.ForEach(hh =>
                {
                    var totalForce = shiftsForce.Sum(sf => sf.IncludesHalfHour(hh.Start) ? sf.Force : 0);
                    if (hh.RequiredForce > 0 || totalForce > 0)
                    {
                        excess = totalForce - hh.RequiredForce;
                        totExcess += excess;
                        Console.WriteLine(" │  {0:hh}:{0:mm} │ {1,3} │ {2,3} │ {4,4} │ {3} │",
                            hh.Start,
                            hh.RequiredForce,
                            totalForce,
                            totalForce >= hh.RequiredForce ? "   " : "ERR",
                            excess
                            );
                    }
                });
                Console.WriteLine(" ├────────┴─────┼─────┼──────┼─────┤");
                Console.WriteLine(" │ Total Agents │ {0,3} │ {1,4} │     │", shiftsForce.Sum(sf => sf.Force), totExcess);
                Console.WriteLine(" └──────────────┴─────┴──────┴─────┘");
                Console.WriteLine();
            }
        }

        //protected void Init()//int maxAgents = 40)
        //{
        //    TimeSpan ts8h = GetTimeSpanHours(8);
        //    TimeSpan ts10h = GetTimeSpanHours(10);
        //    TimeSpan ts30mi = GetTimeSpanMinutes(30);

        //    Shifts = new List<Models.Shift>();
        //    Shifts.Add(new Models.Shift(name: "A", start: GetTimeSpanHours(7), duration: ts8h));
        //    //Shifts.Add(new Models.Shift(name: "G", start: GetTimeSpanHours(8), duration: ts8h));
        //    Shifts.Add(new Models.Shift(name: "B", start: GetTimeSpanHours(9), duration: ts8h));
        //    //Shifts.Add(new Models.Shift(name: "I", start: GetTimeSpanHours(10), duration: ts8h));
        //    Shifts.Add(new Models.Shift(name: "E", start: GetTimeSpanHours(11), duration: ts8h));
        //    Shifts.Add(new Models.Shift(name: "D", start: GetTimeSpanHours(13), duration: ts8h));
        //    Shifts.Add(new Models.Shift(name: "C", start: GetTimeSpanHours(15), duration: ts8h));
        //    //Shifts.Add(new Models.Shift(name: "CL", start: GetTimeSpanHours(16), duration: ts8h));
        //    Shifts.Add(new Models.Shift(name: "H", start: GetTimeSpanHours(17), duration: ts8h));

        //    ShiftsForce = null;// Models.ShiftForce.FromShits(Shifts);

        //    var halfTimes = Models.Shift.GetHalfTimes(Shifts);

        //    TimeSpan start = Shifts.Min(x => x.Start);
        //    TimeSpan end = Shifts.Max(x => x.End);
        //    int[] liveData = new int[] { 6, 4, 4, 3, 2, 2, 1, 1, 1, 1, 1, 1, 1, 2, 3, 5, 10, 20, 29, 45, 51, 57, 61, 61, 61, 58, 58, 56, 54, 51, 48, 50, 43, 43, 41, 38, 37, 37, 35, 31, 27, 29, 24, 23, 18, 14, 13, 9 };
        //    HalfHourRequirements = new List<Models.HalfHourRequirement>();

        //    TimeSpan ts = GetTimeSpanHours(0);
        //    for (int i=0;i<liveData.Length;i++)
        //    {
        //        if (ts >= start && ts < end)
        //        {
        //            HalfHourRequirements.Add(new Models.HalfHourRequirement(start: ts, requiredForce: liveData[i]));
        //        }
        //        ts = ts.Add(ts30mi);
        //    }
        //}

        //protected TimeSpan GetTimeSpanHours(int hours)
        //{
        //    return new TimeSpan(hours: hours, minutes: 0, seconds: 0);
        //}
        //protected TimeSpan GetTimeSpanMinutes(int minutes)
        //{
        //    return new TimeSpan(hours: 0, minutes: minutes, seconds: 0);
        //}
    }
}
