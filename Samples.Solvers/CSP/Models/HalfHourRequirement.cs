using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Solvers.CSP.Models
{

    public class HalfHourRequirement
    {
        /// <summary>
        /// The start of the half time (eg 13:00, 13:30, 14:00) for which the requirement is set
        /// </summary>
        public TimeSpan Start { get; set; }
        /// <summary>
        /// The number of staff required to handled expected traffic
        /// </summary>
        public int RequiredForce { get; set; }

        /// <summary>
        /// Create a new half hour requirement, to indicate how many agents are needed for that 30' span starting at start
        /// Callcenter generated requirements for staff per half hour
        /// </summary>
        /// <param name="start">The start of the half time (eg 13:00, 13:30, 14:00) for which the requirement is set</param>
        /// <param name="requiredForce">The number of staff required to handled expected traffic</param>
        public HalfHourRequirement(TimeSpan start, int requiredForce)
        {
            Start = start;
            RequiredForce = requiredForce;
        }

        public override string ToString()
        {
            return string.Format("{0:hh}:{0:mm} {1} agents", Start, RequiredForce);
        }
    }
}
