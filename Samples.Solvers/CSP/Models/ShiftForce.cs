using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Solvers.CSP.Models
{
    public class ShiftForce : Shift
    {
        public int Force { get; set; }
        public ShiftForce(Shift shift) : base(name: shift.Name, start: shift.Start, duration: shift.Duration)
        {
            Force = 0;
        }
        public ShiftForce(Shift shift, int force) : base(name: shift.Name, start: shift.Start, duration: shift.Duration)
        {
            Force = force;
        }

        public static List<ShiftForce> FromShits(List<Shift> shifts)
        {
            if (shifts == null) return null;
            List<ShiftForce> shiftsForce = new List<ShiftForce>();
            foreach (var shift in shifts)
            {
                shiftsForce.Add(new ShiftForce(shift));
            }
            return shiftsForce;
        }

        public static List<TimeSpan> GetHalfTimes(List<ShiftForce> shifts)
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

        /// <summary>
        /// Get the number of the force for the given halfhour.
        /// </summary>
        /// <param name="start">The start of the halfhour</param>
        /// <returns>The ShiftForce if the halfhour is in the duration of the shift or 0 if not</returns>
        public int ForceForHalfTime(TimeSpan start)
        {
            return (start >= Start && start < End) ? Force : 0;
        }
    }
}
