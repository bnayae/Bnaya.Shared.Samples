using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyLogic
{
    public interface ILogger
    {
        void Log(SeverityLevel level, string message);
    }
}
