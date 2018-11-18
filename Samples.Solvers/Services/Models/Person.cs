using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Solvers.Services.Models
{
    public class Person
    {
        public enum LeaveModes
        {
            Days5,
            Days10,
            Days10_5,
            Days5_5,
            Days5_5_5
        }
        //protected List<Slot> preassignedSlots;
        protected Dictionary<int, bool> slotsMode;
        protected Dictionary<int, bool> slotsPreaasigned;
        protected Dictionary<int, Slot> slots;

        #region Properties
        public int Id { get; protected set; }
        public string Name { get; protected set; }
        public LeaveModes LeaveMode { get; protected set; }

        #region Calculated Properties
        public string LeaveModeText
        {
            get
            {
                return LeaveMode.ToString().Replace("Days", "").Replace("_", "+");
            }
        }
        
        public int PendingFiveDaysSlotsNo
        {
            get
            {
                int available = 0;
                if (slotsMode.Count > 0 && slotsMode[1] && slots[1] == null) available++;
                if (slotsMode.Count > 1 && slotsMode[2] && slots[2] == null) available++;
                if (slotsMode.Count > 2 && slotsMode[3] && slots[3] == null) available++;
                return available;
                //(bool? slotA, bool? slotB, bool? slotC) = SlotTypes(leaveMode: LeaveMode);
                //int available5 = 0;
                //if (slotA.HasValue && slotA.Value) available5++;
                //if (slotB.HasValue && slotB.Value) available5++;
                //if (slotC.HasValue && slotC.Value) available5++;

                //int countDays5 = preassignedSlots.Count(x => x.Duration.TotalDays == 5);
                ////int available5 = 0;
                ////if (LeaveMode == LeaveModes.Days10_5 || LeaveMode == LeaveModes.Days5 || LeaveMode == LeaveModes.Days5_5 || LeaveMode == LeaveModes.Days5_5_5)
                ////    available5 = 1 - countDays5;
                //return available5 - countDays5;
            }
        }
           
        public int PendingTenDaysSlotsNo
        {
            get
            {
                int available = 0;
                if (slotsMode.Count > 0 && !slotsMode[1] && slots[1] == null) available++;
                if (slotsMode.Count > 1 && !slotsMode[2] && slots[2] == null) available++;
                if (slotsMode.Count > 2 && !slotsMode[3] && slots[3] == null) available++;
                return available;
                //(bool? slotA, bool? slotB, bool? slotC) = SlotTypes(leaveMode: LeaveMode);
                ////int available10 = 0;
                //if (slotA.HasValue && !slotA.Value) available10++;
                //if (slotB.HasValue && !slotB.Value) available10++;
                //if (slotC.HasValue && !slotC.Value) available10++;

                //int countDays10 = preassignedSlots.Count(x => x.Duration.TotalDays == 10);
                ////int available5 = 0;
                ////if (LeaveMode == LeaveModes.Days10_5 || LeaveMode == LeaveModes.Days5 || LeaveMode == LeaveModes.Days5_5 || LeaveMode == LeaveModes.Days5_5_5)
                ////    available5 = 1 - countDays5;
                //return available10 - countDays10;


                ////int countDays10 = preassignedSlots.Count(x => x.Duration.TotalDays == 10);
                ////int available10 = 0;
                ////if (LeaveMode == LeaveModes.Days10 || LeaveMode == LeaveModes.Days10_5)
                ////    available10 = 1 - countDays10;
                ////return available10;
            }
        }
    
        //public List<Slot> PreassignedSlots
        //{
        //    get
        //    {
        //        return preassignedSlots.OrderBy(x => x.Start).ToList();
        //    }
        //}

        public Slot SlotA
        {
            get
            {
                if (slots.Count > 0) return slots[1];
                else return null;
                //if (preassignedSlots.Count == 0) return null;
                ////Slot A is a 10days slot, if the leaveMode requires 10days, else it is the first 5days slot
                //int fiveDays, tenDays;
                //SlotTypesNo(leaveMode: LeaveMode, fiveDaysSlotsNo: out fiveDays, tenDaysSlotsNo: out tenDays);
                //if (tenDays > 0) return preassignedSlots.FirstOrDefault(x => x.Duration.TotalDays == 10);
                //else return preassignedSlots.FirstOrDefault(x => x.Duration.TotalDays == 5);
            }
        }

        public Slot SlotB
        {
            get
            {
                if (slots.Count > 1) return slots[2];
                else return null;
                //if (preassignedSlots.Count == 0) return null;
                //int fiveDays, tenDays;
                //SlotTypesNo(leaveMode: LeaveMode, fiveDaysSlotsNo: out fiveDays, tenDaysSlotsNo: out tenDays);

                ////if the leave mode includes 10days, slotB is the first 5days slot
                //if (tenDays > 0) return preassignedSlots.FirstOrDefault(x => x.Duration.TotalDays == 5);
                ////else it is the second 5days slot
                //else return preassignedSlots.Where(x => x.Duration.TotalDays == 5).Skip(1).FirstOrDefault();
            }
        }

        public Slot SlotC
        {
            get
            {
                if (slots.Count > 2) return slots[3];
                else return null;
                //if (preassignedSlots.Count == 0) return null;
                //int fiveDays, tenDays;
                //SlotTypesNo(leaveMode: LeaveMode, fiveDaysSlotsNo: out fiveDays, tenDaysSlotsNo: out tenDays);

                ////if the leave mode includes 10days, slotC is always null (10+5 is the maximum)
                //if (tenDays > 0) return null;
                ////else it is the third 5days slot
                //else return preassignedSlots.Where(x => x.Duration.TotalDays == 5).Skip(2).FirstOrDefault();
            }
        }

        public bool? SlotTypeA
        {
            get
            {
                if (slotsMode.Count > 0) return slotsMode[1];
                else return null;
                //(bool? slotA, bool? slotB, bool? slotC) = SlotTypes(leaveMode: LeaveMode);
                //return slotA;
            }
        }
        public bool? SlotTypeB
        {
            get
            {
                if (slotsMode.Count > 1) return slotsMode[2];
                else return null;
                //(bool? slotA, bool? slotB, bool? slotC) = SlotTypes(leaveMode: LeaveMode);
                //return slotB;
            }
        }
        public bool? SlotTypeC
        {
            get
            {
                if (slotsMode.Count > 2) return slotsMode[3];
                else return null;
                //(bool? slotA, bool? slotB, bool? slotC) = SlotTypes(leaveMode: LeaveMode);
                //return slotC;
            }
        }

        public bool IsSlotAPending
        {
            get
            {
                if (slots.Count > 0) return slots[1] == null;
                else return false;
                //int fiveDays, tenDays;
                //SlotTypesNo(leaveMode: LeaveMode, fiveDaysSlotsNo: out fiveDays, tenDaysSlotsNo: out tenDays);
                //return (tenDays + fiveDays > 0) && SlotA == null;
            }
        }
        public bool IsSlotBPending
        {
            get
            {
                if (slots.Count > 1) return slots[2] == null;
                else return false;
                //int fiveDays, tenDays;
                //SlotTypesNo(leaveMode: LeaveMode, fiveDaysSlotsNo: out fiveDays, tenDaysSlotsNo: out tenDays);
                //return (tenDays + fiveDays > 1) && SlotB == null;
            }
        }
        public bool IsSlotCPending
        {
            get
            {
                if (slots.Count > 2) return slots[3] == null;
                else return false;
                //int fiveDays, tenDays;
                //SlotTypesNo(leaveMode: LeaveMode, fiveDaysSlotsNo: out fiveDays, tenDaysSlotsNo: out tenDays);
                //return (tenDays + fiveDays > 2) && SlotC == null;
            }
        }

        public List<Slot> Slots
        {
            get
            {
                return slots.Values.Where(x=>x != null).ToList();
            }
        }
        #endregion Calculated Properties
        #endregion Properties

        public Person(int id, LeaveModes leaveMode) : this(id: id, name: "Person " + id.ToString(), leaveMode: leaveMode)
        {

        }

        public Person(int id, string name, LeaveModes leaveMode)
        {
            Id = id;
            Name = name;
            LeaveMode = leaveMode;
            SetupSlots(leaveMode);
            //preassignedSlots = new List<Slot>();
        }

        protected void SetupSlots(LeaveModes leaveMode)
        {
            slotsMode = new Dictionary<int, bool>();
            slotsPreaasigned = new Dictionary<int, bool>();
            slots = new Dictionary<int, Slot>();

            (bool? slotA, bool? slotB, bool? slotC) = SlotTypes(leaveMode: LeaveMode);
            if (slotA.HasValue)
            {
                slotsMode.Add(1, slotA.Value);
                slotsPreaasigned.Add(1, false);
                slots.Add(1, null);
            }
            if (slotB.HasValue)
            {
                slotsMode.Add(2, slotB.Value);
                slotsPreaasigned.Add(2, false);
                slots.Add(2, null);
            }
            if (slotC.HasValue)
            {
                slotsMode.Add(3, slotC.Value);
                slotsPreaasigned.Add(3, false);
                slots.Add(3, null);
            }
        }

        public void AddSlot(Slot slot, bool preassigned)
        {
            if (slot == null)
                throw new ArgumentNullException("slot", "Slot is required");

            //check if slot already added
            if (slots.Values.Any(x => x != null &&  (x.Name == slot.Name || (x.Start == slot.Start && x.Duration == slot.Duration))))
                throw new ArgumentException(message: "Slot has already been added", paramName: "slot");

            bool isFiveDays = slot.Duration.TotalDays == 5;
            bool isTenDays = slot.Duration.TotalDays == 10;

            if (!isFiveDays && !isTenDays)
                throw new ArgumentException(message: "Slot must have a duration of 5 or 10 days", paramName: "slot");

            //check if slot may be added
            if (isFiveDays && PendingFiveDaysSlotsNo <= 0)
                throw new ArgumentException(message: "You may not add another 5days slot to this person", paramName: "slot");
            else if (slot.Duration.TotalDays == 10 && PendingTenDaysSlotsNo <= 0)
                throw new ArgumentException(message: "You may not add another 10days slot to this person", paramName: "slot");

            foreach (var pair in slotsMode)
            {
                if (pair.Value == isFiveDays && slots[pair.Key] == null)
                {
                    slots[pair.Key] = slot;
                    slotsPreaasigned[pair.Key] = preassigned;
                    break;
                }
            }
            //preassignedSlots.Add(slot);
        }

        public static void SlotTypesNo(LeaveModes leaveMode, out int fiveDaysSlotsNo, out int tenDaysSlotsNo)
        {
            var (slotA, slotB, slotC) = SlotTypes(leaveMode);
            
            fiveDaysSlotsNo = 0;
            tenDaysSlotsNo = 0;

            if (slotA.HasValue && slotA.Value) fiveDaysSlotsNo++;
            if (slotB.HasValue && slotB.Value) fiveDaysSlotsNo++;
            if (slotC.HasValue && slotC.Value) fiveDaysSlotsNo++;

            if (slotA.HasValue && !slotA.Value) tenDaysSlotsNo++;
            if (slotB.HasValue && !slotB.Value) tenDaysSlotsNo++;
            if (slotC.HasValue && !slotC.Value) tenDaysSlotsNo++;
        }

        public static Tuple<bool?, bool?, bool?> SlotTypes(LeaveModes leaveMode)
        {
            bool? slotA = null;
            bool? slotB = null;
            bool? slotC = null;

            switch (leaveMode)
            {
                case LeaveModes.Days10:
                    slotA = false;
                    break;
                case LeaveModes.Days10_5:
                    slotA = false;
                    slotB = true;
                    break;
                case LeaveModes.Days5:
                    slotA = true;
                    break;
                case LeaveModes.Days5_5:
                    slotA = true;
                    slotB = true;
                    break;
                case LeaveModes.Days5_5_5:
                    slotA = true;
                    slotB = true;
                    slotC = true;
                    break;
            }

            return new Tuple<bool?, bool?, bool?>(slotA, slotB, slotC);
        }
    }
}
