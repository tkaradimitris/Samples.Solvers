using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Solvers.Services.Models
{
    public class Slot
    {
        public string Name { get; protected set; }
        public DateTime Start { get; protected set; }
        public TimeSpan Duration { get; protected set; }
        public DateTime End
        {
            get
            {
                return Start.Add(Duration);
            }
        }

        /// <summary>
        /// Create a new slot
        /// </summary>
        /// <param name="start">The 1st day of the slot</param>
        /// <param name="isFiveDays">true for 5days long slots, false for 10days long slots</param>
        public Slot(string name, DateTime start, bool isFiveDays)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException("name", "Name is required");

            Start = start;
            Duration = new TimeSpan(days: isFiveDays ? 5 : 10, hours: 0, minutes: 0, seconds: 0);
            Name = name;
        }
    }
}
