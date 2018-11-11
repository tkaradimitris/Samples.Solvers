using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Solvers.CSP.Models
{

    public class Shift
    {
        #region Properties
        public string Name { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan Duration { get; set; }

        #region Readonly
        public TimeSpan End
        {
            get
            {
                return Start.Add(Duration);
            }
        }
        #endregion
        #endregion Properties

        public Shift(string name, TimeSpan start, TimeSpan duration)
        {
            Name = name;
            Start = start;
            Duration = duration;

            if (Start.Minutes != 0 && Start.Minutes != 30)
                throw new ArgumentException("Start time must be xx:00 or xx:30");
            else if (Name.Length > 4)
                throw new ArgumentException("Name must be 1-4 chars");
        }

        /// <summary>
        /// Check if a given HalfHour is in the duration of the Shift
        /// </summary>
        /// <param name="halfHour">The start time of the half hour time span</param>
        /// <returns>true if it does, false otehrwise</returns>
        public bool IncludesHalfHour(TimeSpan halfHour)
        {
            return halfHour >= Start && halfHour < End;
        }

        /// <summary>
        /// Get the distinct half hours (by start) included in the given list of Shifts
        /// </summary>
        /// <param name="shifts"></param>
        /// <returns>A list of the timespans that match the start of the included half hours</returns>
        public static List<TimeSpan> GetHalfTimes(List<Shift> shifts)
        {
            if (shifts == null) return null;
            List<TimeSpan> items = new List<TimeSpan>();

            TimeSpan min30 = new TimeSpan(hours: 0, minutes: 30, seconds: 0);
            foreach (var shift in shifts)
            {
                TimeSpan? ts = shift.Start;
                while (ts.HasValue)
                {
                    if (!items.Contains(ts.Value))
                        items.Add(ts.Value);
                    ts = ts.Value.Add(min30);
                    if (ts > shift.End) ts = null;
                }
            }
            items.Sort();
            return items;
        }

        ///// <summary>
        ///// Use multiple shift to build a dicitonary to denote on which halfhour span each shift is active on
        ///// </summary>
        ///// <param name="shifts">The shifts to examine</param>
        ///// <returns>A dictionary indexed by [halfhour start, shift no] with presence or not</returns>
        //public static Dictionary<Tuple<TimeSpan, int>, bool> GetShiftsPresence(List<Shift> shifts)
        //{
        //    //Find unique halfhours from the given shifts
        //    List<TimeSpan> spans = GetHalfTimes(shifts);
        //    if (spans == null) return null;

        //    Dictionary<Tuple<TimeSpan, int>, bool> presence = new Dictionary<Tuple<TimeSpan, int>, bool>();
        //    foreach (var span in spans)
        //    {
        //        Shift shift;
        //        for (var shiftNo = 0; shiftNo < shifts.Count; shiftNo++)
        //        {
        //            shift = shifts[shiftNo];
        //            presence.Add(new Tuple<TimeSpan, int>(span, shiftNo), span >= shift.Start && span < shift.End);
        //        }
        //    }
        //    return presence;
        //}

        public override string ToString()
        {
            return string.Format("{3} {0:hh}:{0:mm}-{1:hh}:{1:mm} {2:hh}h", Start, End, Duration, Name);
        }
    }
}
