using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallCenterContracts
{
    public interface IRepository
    {
        void Save(Deal deal);
        Task SaveAsync(Deal deal);
        Deal Load(string phoneNumber);
        Task<Deal> LoadAsync(string phoneNumber);
    }
}
