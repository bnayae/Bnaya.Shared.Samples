using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallCenterContracts
{
    public class CallData
    {
        public CallData(
            CustomerType type,
            TimeSpan duration)
        {
            Type = type;
            Duration = duration;
        }
        public CustomerType Type { get; }
        public TimeSpan Duration { get; }
    }
}
