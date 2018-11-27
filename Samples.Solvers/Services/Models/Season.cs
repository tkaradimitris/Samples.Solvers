using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Solvers.Services.Models
{
    public class Season
    {
        public string Name { get; protected set; }
        protected Dictionary<bool, Dictionary<string, SeasonSlot>> slots;
        protected Dictionary<int, Person> people;

        public List<SeasonSlot> FiveDaysSlots
        {
            get
            {
                return slots[true].Values.ToList();
            }
        }
        public List<SeasonSlot> TenDaysSlots
        {
            get
            {
                return slots[false].Values.ToList();
            }
        }
        public Dictionary<int, Person> People
        {
            get
            {
                return people;
            }
        }

        public Dictionary<string, SeasonSlot> Slots(bool f)
        {
            return slots[f];
        }

        private static TimeSpan ts7days = new TimeSpan(days: 5, hours: 0, minutes: 0, seconds: 0);
        private static TimeSpan ts30days = new TimeSpan(days: 30, hours: 0, minutes: 0, seconds: 0);

        public Season() : this("Season")
        {

        }

        public Season(string name)
        {
            Name = name;
            slots = new Dictionary<bool, Dictionary<string, SeasonSlot>>();
            slots.Add(true, new Dictionary<string, SeasonSlot>());
            slots.Add(false, new Dictionary<string, SeasonSlot>());
            people = new Dictionary<int, Person>();
        }

        public bool AddSlot(string name, DateTime start, bool isFiveDays, int available )
        {
            //check if slot already added
            if (slots[isFiveDays].ContainsKey(name) || slots[isFiveDays].Values.Any(x=>x.Start == start))
                return false;

            SeasonSlot seasonSlot = new SeasonSlot(name: name, start: start, isFiveDays: isFiveDays, available: available);

            slots[isFiveDays].Add(name, seasonSlot);
            return true;
        }

        public Person AddPerson(int id, string name, Person.LeaveModes leaveMode)
        {
            //check if slot already added
            if (people.ContainsKey(id))
                throw new ArgumentException(message:  "A person with the same id has already been added", paramName: "id");

            var person = new Person(id: id, name: name, leaveMode: leaveMode);
            people.Add(person.Id, person);

            return person;
        }

        public void AssingSlotToPerson(bool isFiveDays, string slotName, int personId)
        {
            var modeSlots = slots[isFiveDays];

            if (modeSlots.ContainsKey(slotName) && people.ContainsKey(personId))
            {
                var slot = modeSlots[slotName];
                slot.DecreaseAvailability();
                people[personId].AddSlot(slot, preassigned: false);
            }
            else if (!modeSlots.ContainsKey(slotName))
                throw new ArgumentException(message: "Slot " + slotName + " not found", paramName: "slotName");
            else if (!people.ContainsKey(personId))
                throw new ArgumentException(message: "Person with Id " + personId.ToString() + " not found", paramName: "personId");

        }

        public SeasonSlot GetSlotByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(message: "The slot's name is required", paramName: "name");

            if (slots[true].ContainsKey(name))
                return slots[true][name];
            else if (slots[false].ContainsKey(name))
                return slots[false][name];

            return null;
        }

        //public Dictionary<Tuple<DateTime, DateTime>, bool> GetSlotCompatibility(bool forFiveDays)
        //{
        //    Dictionary<Tuple<DateTime, DateTime>, bool> compatibility = new Dictionary<Tuple<DateTime, DateTime>, bool>();

        //    var list = forFiveDays ? FiveDaysSlots.OrderBy(x=>x.Start).ToList() : TenDaysSlots.OrderBy(x=>x.Start).ToList();

        //    //var items = list.Select(x => x.Start).ToArray();
        //    SeasonSlot itemFirst, itemSecond;
        //    bool ok;
        //    TimeSpan ts7days = new TimeSpan(days: 5, hours: 0, minutes: 0, seconds: 0);
        //    //loop all dates and find compatibility with following dates (source is sorted)
        //    for (var i1=0;i1< list.Count;i1++)
        //    {
        //        itemFirst = list[i1];

        //        //each item is not compatible to itself (person cannot take the same slot twice)
        //        compatibility.Add(new Tuple<DateTime, DateTime>(itemFirst.Start, itemFirst.Start), false);

        //        //check compatibility with following slots only
        //        for(var i2=i1+1;i2< list.Count;i2++)
        //        {
        //            itemSecond = list[i2];
        //            ok = true;

        //            //slots are of the same duration. 

        //            //-later slot (2nd) starts before the first one has completed
        //            if (itemFirst.End > itemSecond.Start)
        //                ok = false;
        //            //-second slot is too close the end of the first one
        //            else if (itemSecond.Start - itemFirst.End  < ts7days)
        //                ok = false;

        //            compatibility.Add(new Tuple<DateTime, DateTime>(itemFirst.Start, itemSecond.Start), ok);
        //        }
        //    }

        //    return compatibility;
        //}

        /// <summary>
        /// Check if the same person can use these two slots
        /// </summary>
        /// <param name="slotA">The first slot to examine</param>
        /// <param name="slotB">The second slot to examine</param>
        /// <returns>true if the slots are compatible, false if not</returns>
        public static bool AreSlotsCompatible(Slot slotA, Slot slotB)
        {
            if (slotA == null)
                throw new ArgumentNullException(paramName: "slotA", message: "Slot A is required");
            if (slotB == null)
                throw new ArgumentNullException(paramName: "slotB", message: "Slot B is required");

            bool compatible = true;

            //Same start
            if (slotA.Start == slotB.Start)
                compatible = false;
            //Slot A starts within Slot B's duration
            else if (slotA.Start >= slotB.Start && slotA.Start <= slotB.End)
                compatible = false;
            //Slot B starts within Slot C's duration
            else if (slotB.Start >= slotA.Start && slotB.Start <= slotA.End)
                compatible = false;
            //Slot B follows slot A really closely
            else if (slotA.End < slotB.Start && slotB.Start - slotA.End < ts30days)
                compatible = false;
            //Slot A follows slot B really closely
            else if (slotB.End < slotA.Start && slotA.Start - slotB.End < ts30days)
                compatible = false;

            return compatible;
        }
    }
}