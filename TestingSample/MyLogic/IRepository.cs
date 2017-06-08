using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyLogic
{
    public interface IRepository
    {
        double LoadFactor(string type);
        Task<double> LoadFactorAsync(string type);
    }
}
