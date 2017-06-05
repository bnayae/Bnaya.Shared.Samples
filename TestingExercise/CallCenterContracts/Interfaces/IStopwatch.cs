
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallCenterContracts
{
    public interface IStopwatch
    {
        void Start();
        TimeSpan Duration { get; }
        TimeSpan Stop();
    }
}
