using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Samples.Solvers.Services.Models
{
    public class SeasonSlot: Slot
    {
        //public Slot Slot { get; private set; }
        public int Available { get; protected set; }

        public SeasonSlot(string name, DateTime start, bool isFiveDays, int available) : base(name: name, start: start, isFiveDays: isFiveDays)
        {
            if (available < 0)
                throw new ArgumentOutOfRangeException(paramName: "available", message: "Slot availability must be a positive integer");

            Available = available;
        }

        public bool DecreaseAvailability()
        {
            if (Available > 0)
            {
                Available--;
                return true;
            }
            return false;
        }
    }
}
