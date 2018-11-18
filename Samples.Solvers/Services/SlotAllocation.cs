using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SolverFoundation.Services;
using Microsoft.SolverFoundation.Common;
using SolverFoundation.Plugin.LpSolve;

namespace Samples.Solvers.Services
{
    public class SlotAllocation
    {
        protected Models.Season season;
        //public class Print
        //{
        //    protected static char[] lineChars = new char[]{'─','│', '┌', '┐', '└', '┘', '├', '┤', '┬', '┴', '┼' };
        //    /// <summary>
        //    /// Horizontal Line
        //    /// </summary>
        //    public char HZ { get { return lineChars[0]; } }
        //    /// <summary>
        //    /// Vertical Line
        //    /// </summary>
        //    public char VR { get { return lineChars[1]; } }
        //    /// <summary>
        //    /// Corner Upper Right
        //    /// </summary>
        //    public char UR { get { return lineChars[2]; } }
        //    /// <summary>
        //    /// Corner Upper Left
        //    /// </summary>
        //    public char UL { get { return lineChars[3]; } }
        //    /// <summary>
        //    /// Corner Lower Right
        //    /// </summary>
        //    public char LR { get { return lineChars[4]; } }
        //    /// <summary>
        //    /// Corner Lower Left
        //    /// </summary>
        //    public char LL { get { return lineChars[5]; } }
        //    /// <summary>
        //    /// Cross Right
        //    /// </summary>
        //    public char CR { get { return lineChars[6]; } }
        //    /// <summary>
        //    /// Cross Left
        //    /// </summary>
        //    public char CL { get { return lineChars[7]; } }
        //    /// <summary>
        //    /// Cross Down
        //    /// </summary>
        //    public char CD { get { return lineChars[8]; } }
        //    /// <summary>
        //    /// Cross Up
        //    /// </summary>
        //    public char CU { get { return lineChars[9]; } }
        //    /// <summary>
        //    /// Start
        //    /// </summary>
        //    public char SR { get { return lineChars[10]; } }
        //}

        public SlotAllocation()
        {
            season = Init();
            PrintSeason();
        }

        public class PersonPending
        {
            public int PersonId { get; set; }
            public string Name { get; set; }
            public int Pending { get; set; }

            public override string ToString()
            {
                return string.Format("{0}. {1} Pending: {2}", PersonId, Name, Pending);
            }
        }
        public class SlotAvailable
        {
            public int SlotNo { get; set; }
            public string Slot { get; set; }
            public int Available { get; set; }

            public override string ToString()
            {
                return string.Format("{0}. {1} Available: {2}", SlotNo, Slot, Available);
            }
        }
        public class PersonSlot
        {
            public int SlotNo { get; set; }
            public string Slot { get; set; }
            public int PersonId { get; set; }
            public string Name { get; set; }
            public bool Assign { get; set; }

            public override string ToString()
            {
                return string.Format("{1}=>{3} {4:Y;-;n} | [{0},{2}]", SlotNo, Slot, PersonId, Name, Assign.GetHashCode());
            }
        }

        public void Solve(bool forFiveDays)
        {
            var context = SolverContext.GetContext();
            context.ClearModel();
            Model model = context.CreateModel();

            #region Data
            //how many pending slots each person has (for contraint, to avoid assigning more)
            var pendingPerson = season.People
                .Where(x=> x.Value.PendingFiveDaysSlotsNo>0)
                .Select(x => new PersonPending() { PersonId = x.Key, Name = x.Value.Name, Pending = x.Value.PendingFiveDaysSlotsNo })
                .ToList();

            //how many places each slot has available (for contraint, to avoid assigning more people to each one)
            var slots = forFiveDays ? season.FiveDaysSlots : season.TenDaysSlots;
            var slotAvailability = slots
                .Select(x => new SlotAvailable() { SlotNo = slots.IndexOf(x), Slot=x.Name, Available = x.Available })
                .ToList();
            //here we will also add other preassigned slots, of any duration, to use for compatibility checks


            //all the possible combinations of people * slots
            //this will 
            List<PersonSlot> personSlots = new List<PersonSlot>();
            slotAvailability.ForEach(slot =>
            {
                pendingPerson.ForEach(person =>
                {
                    personSlots.Add(new PersonSlot()
                    {
                        SlotNo = slot.SlotNo,
                        Slot = slot.Slot,
                        PersonId = person.PersonId,
                        Name = person.Name,
                        Assign = false
                    });
                });
            });

            //slots already assigned, that we should not auto-assign again
            List<PersonSlot> preassignedSlots = new List<PersonSlot>();
            foreach (var person in season.People.Values.Where(x=>x.Slots.Count > 0))
            {
                foreach (var preSlot in person.Slots) {
                    var slot = slots.FirstOrDefault(x => x.Name == preSlot.Name);
                    if (slot != null) {
                        preassignedSlots.Add(new PersonSlot()
                        {
                            SlotNo = slots.IndexOf(slot),
                            Slot = slot.Name,
                            PersonId = person.Id,
                            Name = person.Name,
                            Assign = true
                        });
                        //add new slots in generic slots, with availability 0, for compatibility checks
                        if (!slotAvailability.Any(x=>x.Slot == slot.Name))
                        {
                            slotAvailability.Add(new SlotAvailable()
                            {
                                SlotNo = slotAvailability.Count,
                                Slot = slot.Name,
                                Available = 0
                            });
                        }
                    }
                }
            }
            #endregion Data

            //Creating the sets
            Set setSlotNo = new Set(Domain.IntegerNonnegative, "SetSlotNo");
            Set setPersonId = new Set(Domain.IntegerNonnegative, "SetPersonId");

            //parameter for available places per slot
            Parameter paramSlotAvailability = new Parameter(domain: Domain.IntegerNonnegative, name: "ParamSlotAvailability", indexSets: setSlotNo);
            paramSlotAvailability.SetBinding(binding: slotAvailability, valueField: "Available", indexFields: "SlotNo");

            //parameter for demanded sizes
            Parameter paramPendingSlots = new Parameter(domain: Domain.IntegerNonnegative, name: "ParamPendingSlots", indexSets: setPersonId);
            paramPendingSlots.SetBinding(binding: pendingPerson, valueField: "Pending", indexFields: "PersonId");

            model.AddParameters(paramSlotAvailability, paramPendingSlots);

            //Decision: Created, bind data and add to the model
            //This is where the solver will place values (which slot(s) each person will be assigned)
            Decision decisionPersonSlots = new Decision(domain: Domain.Boolean, name: "DecisionPersonSlots", indexSets: new Set[] { setSlotNo, setPersonId });
            decisionPersonSlots.SetBinding(binding: personSlots, valueField: "Assign", indexFields: new string[] { "SlotNo", "PersonId" });
            model.AddDecision(decision: decisionPersonSlots);

            //Adding a constraint for not assigning more slots than a person needs
            model.AddConstraint("cPersonTotalSlots", Model.ForEach
                                                            (
                                                              setPersonId, person => //from setPersonId, run for each person
                                                                  //sum of assigned slots
                                                                  Model.Sum
                                                                  (
                                                                    Model.ForEach
                                                                      (
                                                                        setSlotNo, slot => //from setSlotNo, run for each slot
                                                                           decisionPersonSlots[slot, person]
                                                                       )
                                                                  )
                                                              <= paramPendingSlots[person]
                                                            ));

            //Adding a constraint for not assigning more people to a slot that its availability
            model.AddConstraint("cSlotTotalAssignments", Model.ForEach
                                                            (
                                                              setSlotNo, slot => //from setSlotNo, run for each slot
                                                                  //sum of assignment to people
                                                                  Model.Sum
                                                                  (
                                                                    Model.ForEach
                                                                      (
                                                                           setPersonId, person => //from setPersonId, run for each person
                                                                           decisionPersonSlots[slot, person]
                                                                       )
                                                                  )
                                                              <= paramSlotAvailability[slot]
                                                            ));

            //add contraint to avoid reassigning a slot to a person who has it preassigned
            if (preassignedSlots.Count > 0)
            {
                foreach (var preSlot in preassignedSlots)
                {
                    var name = string.Format("cPreassignedSlot_{0}_{1}", preSlot.Slot, preSlot.PersonId);
                    model.AddConstraint(name, decisionPersonSlots[preSlot.SlotNo, preSlot.PersonId] == Rational.Zero);
                }
            }

            //add constraints for incompatible slots
            //this is virtual solution, assuming each slot is incompatible with the -1/+1 slots, just to test results
            //problem is that SFS puts limits on variables and throws an exception if we add too many of them
            //so i tried another approach with simpler incompatibities, to stay lower than the SFS limit
            //it appears that when lpSolve is being used, no limits are enforced
            if (true)
            {
                var minSlot = 0;
                var maxSlot = slotAvailability.Count;
                for (var i = minSlot; i < maxSlot; i++)
                {
                    if (i == 0)
                    {

                        model.AddConstraint("cIncompatible" + i.ToString(), Model.ForEach(setPersonId, person =>
                                                                    decisionPersonSlots[i, person] +
                                                                    decisionPersonSlots[i + 1, person]
                                                                    <= Rational.One));
                    }
                    else if (i <= maxSlot - 2)
                    {

                        model.AddConstraint("cIncompatible" + i.ToString(), Model.ForEach(setPersonId, person =>
                                                                    decisionPersonSlots[i - 1, person] +
                                                                    decisionPersonSlots[i, person] +
                                                                    decisionPersonSlots[i + 1, person]
                                                                    <= Rational.One));
                    }
                    else if (i < maxSlot - 2)
                    {
                        model.AddConstraint("cIncompatible" + i.ToString(), Model.ForEach(setPersonId, person =>
                                                                    decisionPersonSlots[i - 2, person] +
                                                                    decisionPersonSlots[i - 1, person] +
                                                                    decisionPersonSlots[i, person] +
                                                                    decisionPersonSlots[i + 1, person] +
                                                                    decisionPersonSlots[i + 2, person]
                                                                    <= Rational.One));

                    }
                    else if (i == maxSlot - 2)
                    {
                        model.AddConstraint("cIncompatible" + i.ToString(), Model.ForEach(setPersonId, person =>
                                                                    decisionPersonSlots[i - 2, person] +
                                                                    decisionPersonSlots[i - 1, person] +
                                                                    decisionPersonSlots[i, person] +
                                                                    decisionPersonSlots[i + 1, person]
                                                                    <= Rational.One));

                    }
                    else if (i == maxSlot - 1)
                    {
                        model.AddConstraint("cIncompatible" + i.ToString(), Model.ForEach(setPersonId, person =>
                                                                    decisionPersonSlots[i - 2, person] +
                                                                    decisionPersonSlots[i - 1, person] +
                                                                    decisionPersonSlots[i, person]
                                                                    <= Rational.One));

                    }
                }
            }
            if (false)
            {
                var minSlot = 0;
                var maxSlot = slotAvailability.Count;
                for (var i = minSlot; i < maxSlot; i++)
                {
                    if (i%2 == 1 && i <= maxSlot - 2)
                    {

                        model.AddConstraint("cIncompatible" + i.ToString(), Model.ForEach(setPersonId, person =>
                                                                    decisionPersonSlots[i - 1, person] +
                                                                    decisionPersonSlots[i, person] +
                                                                    decisionPersonSlots[i + 1, person]
                                                                    <= Rational.One));
                    }
                }
            }

            //Minize the number of assigned slots (more people happy :) )
            //model.AddGoal("TotalRolls", GoalKind.Maximize, Model.Sum(Model.ForEach(setSlotNo, slot => Model.Sum(Model.ForEach(setPersonId, person => decisionPersonSlots[slot, person])))));
            model.AddGoal("TotalRolls", GoalKind.Maximize, Model.Sum(Model.ForEach(setSlotNo, slot => Model.ForEach(setPersonId, person => decisionPersonSlots[slot, person]))));

            //if we do not set any directive, SFS tries to use Gurobi, which enforces really low limits on variables/decisions
            //if we use SFS Simplex, we may get an exception if we add "too many" vars/decisions.
            //open source lpSolve does not implement any limitations

            //SimplexDirective simplex = new SimplexDirective();
            //simplex.GetSensitivity = false;
            LpSolveDirective simplex = new LpSolveDirective();
            Solution solution = context.Solve(simplex);
            //Gurobi throttle exceeded
            //Solution solution = context.Solve();
            //CSP: Too slow
            //var csp = new ConstraintProgrammingDirective();
            //Solution solution = context.Solve(csp);

            //var report = solution.GetReport() as Report;
            //Console.WriteLine(report);


            var decision = model.Decisions.First();
            foreach (var personSlot in personSlots)
            {
                personSlot.Assign = Convert.ToBoolean(decision.GetDouble(personSlot.SlotNo, personSlot.PersonId));
            }
            var assigned = personSlots.Where(x => x.Assign).OrderBy(x=>x.PersonId);
            //assigned.ToList().ForEach(x =>
            //    Console.WriteLine(x)
            //);
            //Console.WriteLine("Assigned: {0}", assigned.Count());

            assigned.ToList().ForEach(aSlot =>
            {
                season.AssingSlotToPerson(isFiveDays: forFiveDays, slotName: aSlot.Slot, personId: aSlot.PersonId);
            });

            PrintSeason();
        }

        protected Models.Season Init()
        {
            Models.Season season = new Models.Season("Winter A");

            #region Slots
            #region Slots-5
            //DAYS 5
            //JAN
            season.AddSlot(name: "JAN2A", start: new DateTime(2019, 1, 11), isFiveDays: true, available: 1);
            season.AddSlot(name: "JAN2B", start: new DateTime(2019, 1, 16), isFiveDays: true, available: 2);
            season.AddSlot(name: "JAN3A", start: new DateTime(2019, 1, 21), isFiveDays: true, available: 1);
            season.AddSlot(name: "JAN3B", start: new DateTime(2019, 1, 26), isFiveDays: true, available: 3);
            //FEB
            season.AddSlot(name: "FEB1A", start: new DateTime(2019, 2, 1), isFiveDays: true, available: 1);
            season.AddSlot(name: "FEB1B", start: new DateTime(2019, 2, 6), isFiveDays: true, available: 1);
            season.AddSlot(name: "FEB2A", start: new DateTime(2019, 2, 11), isFiveDays: true, available: 2);
            season.AddSlot(name: "FEB2B", start: new DateTime(2019, 2, 16), isFiveDays: true, available: 3);
            season.AddSlot(name: "FEB3A", start: new DateTime(2019, 2, 21), isFiveDays: true, available: 2);
            //MAR
            season.AddSlot(name: "MAR1A", start: new DateTime(2019, 3, 1), isFiveDays: true, available: 1);
            season.AddSlot(name: "MAR1B", start: new DateTime(2019, 3, 6), isFiveDays: true, available: 1);
            season.AddSlot(name: "MAR2A", start: new DateTime(2019, 3, 11), isFiveDays: true, available: 2);
            season.AddSlot(name: "MAR2B", start: new DateTime(2019, 3, 16), isFiveDays: true, available: 2);
            season.AddSlot(name: "MAR3A", start: new DateTime(2019, 3, 21), isFiveDays: true, available: 2);
            season.AddSlot(name: "MAR3B", start: new DateTime(2019, 3, 26), isFiveDays: true, available: 1);
            //APR
            season.AddSlot(name: "APR1A", start: new DateTime(2019, 4, 1), isFiveDays: true, available: 3);
            season.AddSlot(name: "APR1B", start: new DateTime(2019, 4, 6), isFiveDays: true, available: 2);
            season.AddSlot(name: "APR2A", start: new DateTime(2019, 4, 11), isFiveDays: true, available: 3);
            season.AddSlot(name: "APR2B", start: new DateTime(2019, 4, 16), isFiveDays: true, available: 2);
            season.AddSlot(name: "APR3A", start: new DateTime(2019, 4, 21), isFiveDays: true, available: 1);
            season.AddSlot(name: "APR3B", start: new DateTime(2019, 4, 26), isFiveDays: true, available: 2);
            //MAY
            season.AddSlot(name: "MAY1A", start: new DateTime(2019, 5, 1), isFiveDays: true, available: 1);
            season.AddSlot(name: "MAY1B", start: new DateTime(2019, 5, 6), isFiveDays: true, available: 2);
            season.AddSlot(name: "MAY2A", start: new DateTime(2019, 5, 11), isFiveDays: true, available: 2);
            season.AddSlot(name: "MAY2B", start: new DateTime(2019, 5, 16), isFiveDays: true, available: 1);
            season.AddSlot(name: "MAY3A", start: new DateTime(2019, 5, 21), isFiveDays: true, available: 3);
            season.AddSlot(name: "MAY3B", start: new DateTime(2019, 5, 26), isFiveDays: true, available: 2);
            #endregion Slots-5

            #region Slots-10
            //DAYS 10
            //JAN
            season.AddSlot(name: "JAN2", start: new DateTime(2019, 1, 11), isFiveDays: false, available: 1);
            season.AddSlot(name: "JAN3", start: new DateTime(2019, 1, 21), isFiveDays: false, available: 1);
            //FEB
            season.AddSlot(name: "FEB1", start: new DateTime(2019, 2, 1), isFiveDays: false, available: 1);
            season.AddSlot(name: "FEB2", start: new DateTime(2019, 2, 11), isFiveDays: false, available: 1);
            //MAR
            season.AddSlot(name: "MAR1", start: new DateTime(2019, 3, 1), isFiveDays: false, available: 1);
            season.AddSlot(name: "MAR2", start: new DateTime(2019, 3, 11), isFiveDays: false, available: 1);
            season.AddSlot(name: "MAR3", start: new DateTime(2019, 3, 21), isFiveDays: false, available: 1);
            //APR
            season.AddSlot(name: "APR1", start: new DateTime(2019, 4, 1), isFiveDays: false, available: 1);
            season.AddSlot(name: "APR2", start: new DateTime(2019, 4, 11), isFiveDays: false, available: 1);
            season.AddSlot(name: "APR3", start: new DateTime(2019, 4, 21), isFiveDays: false, available: 1);
            //MAY
            season.AddSlot(name: "MAY1", start: new DateTime(2019, 5, 1), isFiveDays: false, available: 1);
            season.AddSlot(name: "MAY2", start: new DateTime(2019, 5, 11), isFiveDays: false, available: 1);
            season.AddSlot(name: "MAY3", start: new DateTime(2019, 5, 21), isFiveDays: false, available: 1);
            #endregion Slots-10
            #endregion Slots

            var slots5 = season.FiveDaysSlots;
            var slots10 = season.TenDaysSlots;

            #region People
            int cnt5 = slots5.Count;

            for (var i=1;i<= cnt5; i++)
                season.AddPerson(id: i, name: "Person " + i.ToString(), leaveMode: Models.Person.LeaveModes.Days5_5);

            var p10 = season.AddPerson(id: cnt5 + 1, name: "Person " + (cnt5 + 1).ToString(), leaveMode: Models.Person.LeaveModes.Days10);
            var p10_5 = season.AddPerson(id: cnt5 + 2, name: "Person " + (cnt5 + 2).ToString(), leaveMode: Models.Person.LeaveModes.Days10_5);
            var p5_5_5 = season.AddPerson(id: cnt5 + 3, name: "Person " + (cnt5 + 3).ToString(), leaveMode: Models.Person.LeaveModes.Days5_5_5);
            var p5 = season.AddPerson(id: cnt5 + 4, name: "Person " + (cnt5 + 4).ToString(), leaveMode: Models.Person.LeaveModes.Days5);
            var p10_5_2 = season.AddPerson(id: cnt5 + 5, name: "Person " + (cnt5 + 5).ToString(), leaveMode: Models.Person.LeaveModes.Days10_5);

            p10.AddSlot(season.GetSlotByName("APR2"), true);
            p10_5.AddSlot(season.GetSlotByName("APR1"), true);
            p5_5_5.AddSlot(season.GetSlotByName("MAR2A"), true);
            p10_5_2.AddSlot(season.GetSlotByName("MAR2B"), true);
            #endregion People

            return season;
        }

        protected void PrintSeason()
        {
            Console.WriteLine("┌─────────────────────────────────────────────────┐");
            Console.WriteLine("│  Season: {0,-10}                             │", season.Name);
            Console.WriteLine("├─────────────────┬───────┬───────┬───────┬───────┤");

            //people
            var people = season.People;
            //protected static char[] lineChars = new char[] { '─', '│', '┌', '┐', '└', '┘', '├', '┤', '┬', '┴', '┼' };
            Console.WriteLine("│  Id. Name       │ Leave │ Slot1 │ Slot2 │ Slot3 │");
            Console.WriteLine("├─────────────────┼───────┼───────┼───────┼───────┤");
            foreach (var person in people.Values)
            {
                var pSlots = person.Slots;
                Console.WriteLine("│ {0,3}. {1,-10} │ {2,-5} │ {3,-5} │ {4,-5} │ {5,-5} │",
                    person.Id,                              //0
                    person.Name,                            //1
                    person.LeaveModeText,//2
                    person.SlotA != null ? person.SlotA?.Name : person.IsSlotAPending ? string.Format("{0:5?;0;10?}", person.SlotTypeA.Value.GetHashCode()) : "xxx",    //3
                    person.SlotB != null ? person.SlotB?.Name : person.IsSlotBPending ? string.Format("{0:5?;0;10?}", person.SlotTypeB.Value.GetHashCode()) : "xxx",    //4
                    person.SlotC != null ? person.SlotC?.Name : person.IsSlotCPending ? string.Format("{0:5?;0;10?}", person.SlotTypeC.Value.GetHashCode()) : "xxx"     //5
                    );
            }
            var totalPending5 = people.Values.Sum(x => x.PendingFiveDaysSlotsNo);
            var totalPending10 = people.Values.Sum(x => x.PendingTenDaysSlotsNo);
            Console.WriteLine("├─────────────────┴───────┴───────┴───────┴───────┤");
            Console.WriteLine("│                             10d: {0,-3}  5d: {1,-5} │",
                totalPending10,                              //0
                totalPending5
                );

            Console.WriteLine("├─────────────────────────────────────────────────┤");
            PrintSlots(isFiveDays: false, slots: season.TenDaysSlots);
            Console.WriteLine("├────────────────────┴────────────────────────────┤");
            PrintSlots(isFiveDays: true, slots: season.FiveDaysSlots);
            Console.WriteLine("└────────────────────┴────────────────────────────┘");
        }

        protected void PrintSlots(bool isFiveDays, List<Models.SeasonSlot> slots)
        {
            Console.WriteLine("│  {0,2} days availability                           │", isFiveDays ? 5 : 10);
            Console.WriteLine("├───────┬────────────┬────────────────────────────┤");
            foreach (var slot in slots)
            {
                Console.WriteLine("│ {0,-5} │ {1:dd/MM/yyyy} │ {2,3} slot{3}                  │",
                    slot.Name,                              //0
                    slot.Start,                            //1
                    slot.Available,//2
                    slot.Available > 1 ? "s" : " "
                    );
            }
            Console.WriteLine("├───────┴────────────┼────────────────────────────┤");
            Console.WriteLine("│              Total │ {0,4} slot{1}                 │",
                slots.Sum(x=>x.Available),                              //0
                slots.Sum(x => x.Available) > 1 ? "s" : " "
                );
        }
    }
}
